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

        // Load stores, printers, and paper sizes
        LoadStores();
        LoadPrinters();
        LoadPaperSizes();
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
        
        for (int i = 1; i <= TotalBoxes; i++)
        {
            PreviewLabels.Add(new LabelPreviewItem
            {
                StoreLine = $"{store.Code} - {store.Name}",
                TransferLine = $"Transfer: {transfer}",
                BoxLine = $"Box {i} of {TotalBoxes}",
                DateLine = dateStr
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
                
                var success = await Task.Run(() =>
                {
                    var zpl = GenerateZplLabels(storeCode, storeName, boxCount, transfer);
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
    private static string GenerateZplLabels(string storeCode, string storeName, int totalBoxes, string transferNumber)
    {
        var sb = new StringBuilder();
        var dateStr = DateTime.Now.ToString("MMM dd, yyyy");

        for (int box = 1; box <= totalBoxes; box++)
        {
            // ZPL for a 4x6 inch label (standard shipping label size)
            // Adjust dimensions and positions based on your label size
            sb.AppendLine("^XA"); // Start format

            // Set label size (4x6 inches at 203 DPI = 812x1218 dots)
            sb.AppendLine("^PW812");  // Print width
            sb.AppendLine("^LL1218"); // Label length

            // Store code and name - large, bold, centered at top
            sb.AppendLine("^FO20,50^A0N,80,80^FB772,1,0,C,0^FD" + $"{storeCode} - {storeName}" + "^FS");

            // Horizontal line
            sb.AppendLine("^FO20,150^GB772,3,3^FS");

            // Transfer number
            sb.AppendLine("^FO20,200^A0N,50,50^FB772,1,0,C,0^FD" + $"Transfer: {transferNumber}" + "^FS");

            // Box number - extra large and bold
            sb.AppendLine("^FO20,320^A0N,120,120^FB772,1,0,C,0^FD" + $"Box {box} of {totalBoxes}" + "^FS");

            // Horizontal line
            sb.AppendLine("^FO20,480^GB772,3,3^FS");

            // Date at bottom
            sb.AppendLine("^FO20,530^A0N,40,40^FB772,1,0,C,0^FD" + $"Date: {dateStr}" + "^FS");

            // Barcode of transfer number (optional, useful for scanning)
            sb.AppendLine("^FO156,620^BY3^BCN,100,Y,N,N^FD" + transferNumber + "^FS");

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
