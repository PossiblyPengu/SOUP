using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SOUP.Data;
using SOUP.Infrastructure.Services.Parsers;

namespace SOUP.ViewModels;

public partial class SwiftLabelViewModel : ObservableObject
{
    private readonly ILogger<SwiftLabelViewModel>? _logger;

    public ObservableCollection<StoreEntry> Stores { get; } = new();

    [ObservableProperty]
    private StoreEntry? _selectedStore;

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

    public IAsyncRelayCommand GenerateLabelsCommand { get; }

    public SwiftLabelViewModel(ILogger<SwiftLabelViewModel>? logger = null)
    {
        _logger = logger;

        GenerateLabelsCommand = new AsyncRelayCommand(GenerateLabelsAsync, CanGenerateLabels);

        // Load stores
        LoadStores();
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

    partial void OnSelectedStoreChanged(StoreEntry? value)
    {
        GenerateLabelsCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(StorePreviewText));
    }

    partial void OnTotalBoxesChanged(int value)
    {
        GenerateLabelsCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(BoxPreviewText));
    }

    partial void OnTransferNumberChanged(string value)
    {
        GenerateLabelsCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(TransferPreviewText));
    }

    private async Task GenerateLabelsAsync()
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
            StatusMessage = "Generating labels...";

            // Show save dialog
            var saveDialog = new SaveFileDialog
            {
                Filter = "Word Document (*.docx)|*.docx",
                DefaultExt = ".docx",
                FileName = $"Labels_{store.Code}_{TransferNumber}_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (saveDialog.ShowDialog() != true)
            {
                StatusMessage = "Label generation cancelled";
                return;
            }

            var filePath = saveDialog.FileName;

            // Generate the Word document (capture store for thread safety)
            var storeCode = store.Code;
            var storeName = store.Name;
            var boxCount = TotalBoxes;
            var transfer = TransferNumber;
            await Task.Run(() => CreateLabelDocument(filePath, storeCode, storeName, boxCount, transfer)).ConfigureAwait(false);

            StatusMessage = $"Successfully created {boxCount} labels!";
            _logger?.LogInformation("Generated {Count} labels for store {Store}, transfer {Transfer}",
                boxCount, storeCode, transfer);

            // Open the document
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to generate labels");
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
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
