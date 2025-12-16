using System;
using System.Collections.ObjectModel;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SOUP.Data;
using SOUP.Helpers;
using SOUP.Infrastructure.Services.Parsers;

namespace SOUP.ViewModels;

public partial class SwiftLabelViewModel : ObservableObject
{
    private readonly ILogger<SwiftLabelViewModel>? _logger;

    public ObservableCollection<StoreEntry> Stores { get; } = new();
    public ObservableCollection<string> AvailablePrinters { get; } = new();
    public ObservableCollection<PaperSizeOption> AvailablePaperSizes { get; } = new();

    [ObservableProperty]
    private StoreEntry? _selectedStore;

    [ObservableProperty]
    private string? _selectedPrinter;

    [ObservableProperty]
    private PaperSizeOption? _selectedPaperSize;

    [ObservableProperty]
    private MarginPreset? _selectedMarginPreset;

    public ObservableCollection<MarginPreset> AvailableMarginPresets { get; } = new();

    // Computed margin values from selected preset
    public double MarginTop => SelectedMarginPreset?.Top ?? 2.0;
    public double MarginBottom => SelectedMarginPreset?.Bottom ?? 2.0;
    public double MarginLeft => SelectedMarginPreset?.Left ?? 2.0;
    public double MarginRight => SelectedMarginPreset?.Right ?? 2.0;

    [ObservableProperty]
    private int _totalBoxes = 1;

    [ObservableProperty]
    private string _transferNumber = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Ready to generate labels";

    [ObservableProperty]
    private bool _isGenerating;

    // Preview text properties
    public string StorePreviewText => SelectedStore != null 
        ? $"{SelectedStore.Code} - {SelectedStore.Name}" 
        : "--- - Select Store";
    
    public string TransferPreviewText => string.IsNullOrWhiteSpace(TransferNumber) 
        ? "Transfer: TO-XXXXX" 
        : $"Transfer: {TransferNumber}";
    
    public string BoxPreviewText => $"Box 1 of {TotalBoxes}";
    
    public string DatePreviewText => $"Date: {DateTime.Now:MMM dd, yyyy}";
    
    // Print settings info for main view
    public string PrintFormatInfo
    {
        get
        {
            var format = IsZebraPrinter ? "ZPL" : "Word";
            var size = SelectedPaperSize?.Name ?? "Default";
            return $"{format} • {size}";
        }
    }
    
    private bool IsZebraPrinter => SelectedPrinter != null && 
        (SelectedPrinter.Contains("Zebra", StringComparison.OrdinalIgnoreCase) ||
         SelectedPrinter.Contains("ZDesigner", StringComparison.OrdinalIgnoreCase));

    public ObservableCollection<LabelPreviewItem> PreviewLabels { get; } = new();

    public IAsyncRelayCommand SaveLabelsCommand { get; }
    public IAsyncRelayCommand PrintLabelsCommand { get; }
    public IRelayCommand RefreshPrintersCommand { get; }

    public SwiftLabelViewModel(ILogger<SwiftLabelViewModel>? logger = null)
    {
        _logger = logger;

        SaveLabelsCommand = new AsyncRelayCommand(SaveLabelsAsync, CanGenerateLabels);
        PrintLabelsCommand = new AsyncRelayCommand(ExecutePrintAsync, CanPrintLabels);
        RefreshPrintersCommand = new RelayCommand(LoadPrinters);

        // Load stores, printers, paper sizes, and margin presets
        LoadStores();
        LoadPrinters();
        LoadPaperSizes();
        LoadMarginPresets();
    }

    private void LoadMarginPresets()
    {
        AvailableMarginPresets.Clear();
        
        AvailableMarginPresets.Add(new MarginPreset("none", "None (0mm)", 0));
        AvailableMarginPresets.Add(new MarginPreset("minimal", "Minimal (1mm)", 1));
        AvailableMarginPresets.Add(new MarginPreset("small", "Small (2mm)", 2));
        AvailableMarginPresets.Add(new MarginPreset("medium", "Medium (3mm)", 3));
        AvailableMarginPresets.Add(new MarginPreset("large", "Large (5mm)", 5));
        AvailableMarginPresets.Add(new MarginPreset("xlarge", "Extra Large (8mm)", 8));
        
        // Default to medium margins
        SelectedMarginPreset = AvailableMarginPresets[3];
    }

    private void LoadPaperSizes()
    {
        AvailablePaperSizes.Clear();
        
        // Zebra label sizes (common thermal label sizes)
        AvailablePaperSizes.Add(new PaperSizeOption("60x30mm", "6 × 3 cm", 2.36, 1.18, true));
        AvailablePaperSizes.Add(new PaperSizeOption("35x20mm", "3.5 × 2 cm", 1.38, 0.79, true));
        AvailablePaperSizes.Add(new PaperSizeOption("50x25mm", "5 × 2.5 cm", 1.97, 0.98, true));
        AvailablePaperSizes.Add(new PaperSizeOption("60x40mm", "6 × 4 cm", 2.36, 1.57, true));
        AvailablePaperSizes.Add(new PaperSizeOption("100x50mm", "10 × 5 cm", 3.94, 1.97, true));
        AvailablePaperSizes.Add(new PaperSizeOption("100x150mm", "10 × 15 cm (4×6\")", 3.94, 5.91, true));
        
        // Standard paper sizes
        AvailablePaperSizes.Add(new PaperSizeOption("Letter", "Letter (8.5\" × 11\")", 8.5, 11, false));
        AvailablePaperSizes.Add(new PaperSizeOption("A4", "A4 (210 × 297mm)", 8.27, 11.69, false));
        AvailablePaperSizes.Add(new PaperSizeOption("A5", "A5 (148 × 210mm)", 5.83, 8.27, false));
        
        // Default to 6x3cm Zebra label
        SelectedPaperSize = AvailablePaperSizes[0];
    }

    private void LoadPrinters()
    {
        try
        {
            AvailablePrinters.Clear();
            foreach (string printer in PrinterSettings.InstalledPrinters)
            {
                AvailablePrinters.Add(printer);
            }

            // Try to auto-select a Zebra printer if available
            var zebraPrinter = AvailablePrinters.FirstOrDefault(p => 
                p.Contains("Zebra", StringComparison.OrdinalIgnoreCase) ||
                p.Contains("ZDesigner", StringComparison.OrdinalIgnoreCase));
            
            if (zebraPrinter != null)
            {
                SelectedPrinter = zebraPrinter;
            }
            else if (AvailablePrinters.Count > 0)
            {
                // Fall back to default printer
                var defaultPrinter = new PrinterSettings().PrinterName;
                SelectedPrinter = AvailablePrinters.Contains(defaultPrinter) 
                    ? defaultPrinter 
                    : AvailablePrinters.First();
            }

            _logger?.LogInformation("Loaded {Count} printers, selected: {Printer}", 
                AvailablePrinters.Count, SelectedPrinter);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load printers");
            StatusMessage = "Error loading printers";
        }
    }

    private void LoadStores()
    {
        try
        {
            var stores = InternalStoreDictionary.GetStores();
            foreach (var store in stores.OrderBy(s => s.Code))
            {
                Stores.Add(store);
            }
            _logger?.LogInformation("Loaded {Count} stores for Swift Label", stores.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load stores");
            StatusMessage = "Error loading stores";
        }
    }

    private bool CanGenerateLabels()
    {
        return SelectedStore != null &&
               TotalBoxes > 0 &&
               !string.IsNullOrWhiteSpace(TransferNumber) &&
               !IsGenerating;
    }

    private bool CanPrintLabels()
    {
        return CanGenerateLabels() && !string.IsNullOrEmpty(SelectedPrinter);
    }

    partial void OnSelectedStoreChanged(StoreEntry? value)
    {
        SaveLabelsCommand.NotifyCanExecuteChanged();
        PrintLabelsCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(StorePreviewText));
        RefreshPreviewLabels();
    }

    partial void OnSelectedPrinterChanged(string? value)
    {
        PrintLabelsCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(PrintFormatInfo));
        RefreshPreviewLabels();
        
        // Auto-select appropriate paper size based on printer type
        if (IsZebraPrinter && SelectedPaperSize != null && !SelectedPaperSize.IsLabel)
        {
            // Switch to a label size for Zebra printers
            SelectedPaperSize = AvailablePaperSizes.FirstOrDefault(p => p.IsLabel);
        }
        else if (!IsZebraPrinter && SelectedPaperSize != null && SelectedPaperSize.IsLabel)
        {
            // Switch to Letter for standard printers
            SelectedPaperSize = AvailablePaperSizes.FirstOrDefault(p => !p.IsLabel);
        }
    }

    partial void OnSelectedPaperSizeChanged(PaperSizeOption? value)
    {
        OnPropertyChanged(nameof(PrintFormatInfo));
        RefreshPreviewLabels();
    }

    partial void OnTotalBoxesChanged(int value)
    {
        SaveLabelsCommand.NotifyCanExecuteChanged();
        PrintLabelsCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(BoxPreviewText));
        RefreshPreviewLabels();
    }

    partial void OnTransferNumberChanged(string value)
    {
        SaveLabelsCommand.NotifyCanExecuteChanged();
        PrintLabelsCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(TransferPreviewText));
        RefreshPreviewLabels();
    }
    
    private void RefreshPreviewLabels()
    {
        PreviewLabels.Clear();
        
        var store = SelectedStore;
        if (store == null || TotalBoxes <= 0)
            return;
            
        var transfer = string.IsNullOrWhiteSpace(TransferNumber) ? "TO-XXXXX" : TransferNumber;
        var dateStr = $"Date: {DateTime.Now:MMM dd, yyyy}";
        
        // Calculate preview dimensions based on paper size
        // Scale: 96 DPI preview, so inches * 96 gives pixels, then scale down for preview
        var paper = SelectedPaperSize;
        double previewScale = 80; // pixels per inch for preview
        double labelWidth = paper != null ? paper.WidthInches * previewScale : 260;
        double labelHeight = paper != null ? paper.HeightInches * previewScale : 120;
        
        // Clamp to reasonable preview sizes
        labelWidth = Math.Clamp(labelWidth, 100, 300);
        labelHeight = Math.Clamp(labelHeight, 60, 250);
        
        // Scale fonts based on label height
        double fontScale = labelHeight / 100.0;
        double storeFontSize = Math.Clamp(11 * fontScale, 8, 14);
        double transferFontSize = Math.Clamp(10 * fontScale, 7, 12);
        double boxFontSize = Math.Clamp(18 * fontScale, 12, 28);
        double dateFontSize = Math.Clamp(9 * fontScale, 6, 11);
        
        for (int i = 1; i <= TotalBoxes; i++)
        {
            PreviewLabels.Add(new LabelPreviewItem
            {
                StoreLine = $"{store.Code} - {store.Name}",
                TransferLine = $"Transfer: {transfer}",
                BoxLine = $"Box {i} of {TotalBoxes}",
                DateLine = dateStr,
                LabelWidth = labelWidth,
                LabelHeight = labelHeight,
                StoreFontSize = storeFontSize,
                TransferFontSize = transferFontSize,
                BoxFontSize = boxFontSize,
                DateFontSize = dateFontSize
            });
        }
    }

    private async Task SaveLabelsAsync()
    {
        // Guard clause for null safety
        var store = SelectedStore;
        if (store == null || TotalBoxes <= 0 || string.IsNullOrWhiteSpace(TransferNumber))
        {
            StatusMessage = "Please fill in all fields";
            return;
        }

        try
        {
            IsGenerating = true;
            StatusMessage = "Saving labels...";

            // Show save dialog
            var saveDialog = new SaveFileDialog
            {
                Filter = "Word Document (*.docx)|*.docx",
                DefaultExt = ".docx",
                FileName = $"Labels_{store.Code}_{TransferNumber}_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (saveDialog.ShowDialog() != true)
            {
                StatusMessage = "Save cancelled";
                return;
            }

            var filePath = saveDialog.FileName;

            // Generate the Word document (capture store for thread safety)
            var storeCode = store.Code;
            var storeName = store.Name;
            var boxCount = TotalBoxes;
            var transfer = TransferNumber;
            await Task.Run(() => CreateLabelDocument(filePath, storeCode, storeName, boxCount, transfer)).ConfigureAwait(false);

            StatusMessage = $"Saved {boxCount} labels to {Path.GetFileName(filePath)}";
            _logger?.LogInformation("Saved {Count} labels for store {Store}, transfer {Transfer} to {File}",
                boxCount, storeCode, transfer, filePath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save labels");
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    /// <summary>
    /// Execute the print operation
    /// </summary>
    private async Task ExecutePrintAsync()
    {
        // Guard clause for null safety
        var store = SelectedStore;
        var printer = SelectedPrinter;
        if (store == null || TotalBoxes <= 0 || string.IsNullOrWhiteSpace(TransferNumber) || string.IsNullOrEmpty(printer))
        {
            StatusMessage = "Please fill in all fields and select a printer";
            return;
        }

        try
        {
            IsGenerating = true;
            
            var storeCode = store.Code;
            var storeName = store.Name;
            var boxCount = TotalBoxes;
            var transfer = TransferNumber;
            var printerName = printer;

            // Check if it's a Zebra printer
            bool isZebraPrinter = printerName.Contains("Zebra", StringComparison.OrdinalIgnoreCase) ||
                                  printerName.Contains("ZDesigner", StringComparison.OrdinalIgnoreCase);

            if (isZebraPrinter)
            {
                // Use ZPL for Zebra printers
                StatusMessage = $"Printing {boxCount} ZPL labels to {printerName}...";
                
                // Get paper dimensions
                var paperSize = SelectedPaperSize;
                double widthInches = paperSize?.WidthInches ?? 2.36; // Default 6cm
                double heightInches = paperSize?.HeightInches ?? 1.18; // Default 3cm
                
                // Get margin values
                double marginT = MarginTop;
                double marginB = MarginBottom;
                double marginL = MarginLeft;
                double marginR = MarginRight;
                
                var success = await Task.Run(() =>
                {
                    var zpl = GenerateZplLabels(storeCode, storeName, boxCount, transfer, widthInches, heightInches, marginT, marginB, marginL, marginR);
                    return RawPrinterHelper.SendStringToPrinter(printerName, zpl, $"Labels_{storeCode}_{transfer}");
                }).ConfigureAwait(false);

                if (success)
                {
                    StatusMessage = $"Printed {boxCount} labels to {printerName}";
                    _logger?.LogInformation("Printed {Count} ZPL labels for store {Store}, transfer {Transfer} to {Printer}",
                        boxCount, storeCode, transfer, printerName);
                }
                else
                {
                    StatusMessage = "Failed to send labels to printer";
                    _logger?.LogWarning("Failed to print ZPL labels to {Printer}", printerName);
                }
            }
            else
            {
                // Use Word document for standard printers
                StatusMessage = $"Generating labels for {printerName}...";
                
                var tempPath = Path.Combine(Path.GetTempPath(), $"Labels_{storeCode}_{transfer}_{DateTime.Now:yyyyMMdd_HHmmss}.docx");
                
                await Task.Run(() => CreateLabelDocument(tempPath, storeCode, storeName, boxCount, transfer)).ConfigureAwait(false);

                // Print to specific printer using PrintDocument
                await Task.Run(() =>
                {
                    var printInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = tempPath,
                        Verb = "printto",
                        Arguments = $"\"{printerName}\"",
                        UseShellExecute = true,
                        CreateNoWindow = true,
                        WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                    };
                    
                    using var process = System.Diagnostics.Process.Start(printInfo);
                    process?.WaitForExit(30000); // Wait up to 30 seconds
                }).ConfigureAwait(false);

                StatusMessage = $"Sent {boxCount} labels to {printerName}";
                _logger?.LogInformation("Printed {Count} Word labels for store {Store}, transfer {Transfer} to {Printer}",
                    boxCount, storeCode, transfer, printerName);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to print labels");
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    /// <summary>
    /// Generate ZPL (Zebra Programming Language) for all labels
    /// </summary>
    private static string GenerateZplLabels(string storeCode, string storeName, int totalBoxes, string transferNumber, 
        double widthInches, double heightInches, double marginTopMm, double marginBottomMm, double marginLeftMm, double marginRightMm)
    {
        var sb = new StringBuilder();
        var dateStr = DateTime.Now.ToString("MMM dd, yyyy");
        
        // Convert inches to dots (203 DPI for standard Zebra printers)
        int dpi = 203;
        int widthDots = (int)(widthInches * dpi);
        int heightDots = (int)(heightInches * dpi);
        
        // Convert mm margins to dots (1 inch = 25.4mm)
        int marginLeft = (int)(marginLeftMm / 25.4 * dpi);
        int marginRight = (int)(marginRightMm / 25.4 * dpi);
        int marginTop = (int)(marginTopMm / 25.4 * dpi);
        int marginBottom = (int)(marginBottomMm / 25.4 * dpi);
        
        // Calculate content area
        int contentWidth = widthDots - marginLeft - marginRight;
        int contentHeight = heightDots - marginTop - marginBottom;
        
        // Calculate font sizes relative to content area
        int largeFontH = Math.Max(20, contentHeight / 5);  // Store name
        int medFontH = Math.Max(15, contentHeight / 8);    // Transfer
        int xlFontH = Math.Max(25, contentHeight / 3);     // Box number  
        int smallFontH = Math.Max(12, contentHeight / 10); // Date
        
        // Calculate vertical positions within content area
        int y1 = marginTop + (contentHeight / 20);       // Store name
        int y2 = y1 + largeFontH + 5;                    // Line
        int y3 = y2 + 10;                                // Transfer
        int y4 = y3 + medFontH + 5;                      // Box number
        int y5 = heightDots - marginBottom - smallFontH - 5; // Date at bottom

        for (int box = 1; box <= totalBoxes; box++)
        {
            sb.AppendLine("^XA"); // Start format

            // Set label size
            sb.AppendLine($"^PW{widthDots}");  // Print width
            sb.AppendLine($"^LL{heightDots}"); // Label length

            // Store code and name - centered at top
            sb.AppendLine($"^FO{marginLeft},{y1}^A0N,{largeFontH},{largeFontH}^FB{contentWidth},1,0,C,0^FD{storeCode} - {storeName}^FS");

            // Horizontal line
            sb.AppendLine($"^FO{marginLeft},{y2}^GB{contentWidth},2,2^FS");

            // Transfer number
            sb.AppendLine($"^FO{marginLeft},{y3}^A0N,{medFontH},{medFontH}^FB{contentWidth},1,0,C,0^FDTransfer: {transferNumber}^FS");

            // Box number - large and centered
            sb.AppendLine($"^FO{marginLeft},{y4}^A0N,{xlFontH},{xlFontH}^FB{contentWidth},1,0,C,0^FDBox {box} of {totalBoxes}^FS");

            // Date at bottom
            sb.AppendLine($"^FO{marginLeft},{y5}^A0N,{smallFontH},{smallFontH}^FB{contentWidth},1,0,C,0^FDDate: {dateStr}^FS");

            sb.AppendLine("^XZ"); // End format
        }

        return sb.ToString();
    }

    private void CreateLabelDocument(string filePath, string storeCode, string storeName, int totalBoxes, string transferNumber)
    {
        using var document = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document);

        // Add main document part
        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = new Body();

        // Page settings for labels (using letter size paper)
        var sectionProps = new SectionProperties(
            new PageSize { Width = 12240, Height = 15840 }, // Letter size in twentieths of a point
            new PageMargin
            {
                Top = 720,      // 0.5 inch
                Bottom = 720,
                Left = 720,
                Right = 720
            }
        );

        // Create labels - 2 per row, fitting multiple on a page
        var table = new Table();

        // Table properties for clean layout
        var tableProps = new TableProperties(
            new TableWidth { Width = "100%", Type = TableWidthUnitValues.Pct },
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 12, Color = "000000" },
                new BottomBorder { Val = BorderValues.Single, Size = 12, Color = "000000" },
                new LeftBorder { Val = BorderValues.Single, Size = 12, Color = "000000" },
                new RightBorder { Val = BorderValues.Single, Size = 12, Color = "000000" },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 12, Color = "000000" },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 12, Color = "000000" }
            )
        );
        table.AppendChild(tableProps);

        // Calculate rows needed (2 labels per row)
        var rowsNeeded = (totalBoxes + 1) / 2;

        for (int row = 0; row < rowsNeeded; row++)
        {
            var tableRow = new TableRow();

            // Row height
            tableRow.AppendChild(new TableRowProperties(
                new TableRowHeight { Val = 2880, HeightType = HeightRuleValues.Exact } // 2 inches
            ));

            for (int col = 0; col < 2; col++)
            {
                var boxNumber = row * 2 + col + 1;

                if (boxNumber <= totalBoxes)
                {
                    tableRow.AppendChild(CreateLabelCell(boxNumber, storeCode, storeName, totalBoxes, transferNumber));
                }
                else
                {
                    // Empty cell for odd number of boxes
                    tableRow.AppendChild(new TableCell(
                        new TableCellProperties(
                            new TableCellWidth { Width = "50%", Type = TableWidthUnitValues.Pct }
                        ),
                        new Paragraph()
                    ));
                }
            }

            table.AppendChild(tableRow);
        }

        body.AppendChild(table);
        body.AppendChild(sectionProps);
        mainPart.Document.Body = body;
        mainPart.Document.Save();
    }

    // Font size constants (in half-points)
    private const string FontSizeStoreName = "48";    // 24pt
    private const string FontSizeTransfer = "36";     // 18pt
    private const string FontSizeBoxNumber = "72";    // 36pt
    private const string FontSizeDate = "24";         // 12pt

    private static TableCell CreateLabelCell(int boxNumber, string storeCode, string storeName, int totalBoxes, string transferNumber)
    {
        var cell = new TableCell();

        // Cell properties
        cell.AppendChild(new TableCellProperties(
            new TableCellWidth { Width = "50%", Type = TableWidthUnitValues.Pct },
            new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }
        ));

        // Store name - large and bold
        var storePara = new Paragraph(
            new ParagraphProperties(
                new Justification { Val = JustificationValues.Center },
                new SpacingBetweenLines { After = "120" }
            ),
            new Run(
                new RunProperties(
                    new Bold(),
                    new FontSize { Val = FontSizeStoreName }
                ),
                new Text($"{storeCode} - {storeName}")
            )
        );

        // Transfer number
        var transferPara = new Paragraph(
            new ParagraphProperties(
                new Justification { Val = JustificationValues.Center },
                new SpacingBetweenLines { After = "120" }
            ),
            new Run(
                new RunProperties(
                    new Bold(),
                    new FontSize { Val = FontSizeTransfer }
                ),
                new Text($"Transfer: {transferNumber}")
            )
        );

        // Box number
        var boxPara = new Paragraph(
            new ParagraphProperties(
                new Justification { Val = JustificationValues.Center },
                new SpacingBetweenLines { After = "60" }
            ),
            new Run(
                new RunProperties(
                    new Bold(),
                    new FontSize { Val = FontSizeBoxNumber }
                ),
                new Text($"Box {boxNumber} of {totalBoxes}")
            )
        );

        // Date
        var datePara = new Paragraph(
            new ParagraphProperties(
                new Justification { Val = JustificationValues.Center }
            ),
            new Run(
                new RunProperties(
                    new FontSize { Val = FontSizeDate }
                ),
                new Text($"Date: {DateTime.Now:MMM dd, yyyy}")
            )
        );

        cell.Append(storePara, transferPara, boxPara, datePara);

        return cell;
    }
}

/// <summary>
/// Data class for label preview display
/// </summary>
public class LabelPreviewItem
{
    public string StoreLine { get; set; } = string.Empty;
    public string TransferLine { get; set; } = string.Empty;
    public string BoxLine { get; set; } = string.Empty;
    public string DateLine { get; set; } = string.Empty;
    
    // Size properties for preview scaling
    public double LabelWidth { get; set; } = 260;
    public double LabelHeight { get; set; } = 120;
    public double StoreFontSize { get; set; } = 11;
    public double TransferFontSize { get; set; } = 10;
    public double BoxFontSize { get; set; } = 18;
    public double DateFontSize { get; set; } = 9;
}

/// <summary>
/// Paper size option for label printing
/// </summary>
public class PaperSizeOption
{
    public string Name { get; }
    public string DisplayName { get; }
    public double WidthInches { get; }
    public double HeightInches { get; }
    public bool IsLabel { get; }
    
    public PaperSizeOption(string name, string displayName, double widthInches, double heightInches, bool isLabel)
    {
        Name = name;
        DisplayName = displayName;
        WidthInches = widthInches;
        HeightInches = heightInches;
        IsLabel = isLabel;
    }
    
    public override string ToString() => DisplayName;
}

/// <summary>
/// Margin preset for label printing
/// </summary>
public class MarginPreset
{
    public string Name { get; }
    public string DisplayName { get; }
    public double Top { get; }
    public double Bottom { get; }
    public double Left { get; }
    public double Right { get; }
    
    public MarginPreset(string name, string displayName, double top, double bottom, double left, double right)
    {
        Name = name;
        DisplayName = displayName;
        Top = top;
        Bottom = bottom;
        Left = left;
        Right = right;
    }
    
    public MarginPreset(string name, string displayName, double all) : this(name, displayName, all, all, all, all) { }
    
    public override string ToString() => DisplayName;
}
