using System.Text;
using Microsoft.Extensions.Logging;
using SOUP.Core.Entities.AllocationBuddy;

namespace SOUP.Services.AllocationBuddy;

/// <summary>
/// Service responsible for clipboard operations in Allocation Buddy.
/// Handles copying allocation data to clipboard in various formats.
/// </summary>
public class AllocationClipboardService
{
    private readonly ItemDictionaryService _dictionaryService;
    private readonly AllocationBuddyConfiguration _configuration;
    private readonly ILogger<AllocationClipboardService>? _logger;

    public AllocationClipboardService(
        ItemDictionaryService dictionaryService,
        AllocationBuddyConfiguration configuration,
        ILogger<AllocationClipboardService>? logger = null)
    {
        _dictionaryService = dictionaryService ?? throw new ArgumentNullException(nameof(dictionaryService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;
    }

    /// <summary>
    /// Validates that clipboard text length doesn't exceed the maximum allowed size.
    /// </summary>
    /// <param name="text">The text to validate.</param>
    /// <returns>True if the text is within the size limit.</returns>
    public bool ValidateClipboardTextLength(string text)
    {
        if (string.IsNullOrEmpty(text)) return true;

        var maxLength = _configuration.MaxClipboardTextLengthBytes;
        if (text.Length > maxLength)
        {
            _logger?.LogWarning("Clipboard text rejected: {Length} bytes exceeds maximum of {Max}",
                text.Length, maxLength);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Builds clipboard text from a location's items.
    /// </summary>
    /// <param name="location">The location to copy.</param>
    /// <param name="includeDescriptions">Whether to include item descriptions.</param>
    /// <param name="clipboardFormat">The clipboard format (TabSeparated or CommaSeparated).</param>
    /// <returns>The formatted clipboard text, or null if location has no items.</returns>
    public string? BuildLocationClipboardText(
        LocationAllocation? location,
        bool includeDescriptions,
        string clipboardFormat)
    {
        if (location == null || location.Items.Count == 0)
        {
            return null;
        }

        var separator = clipboardFormat == AllocationBuddyConstants.ClipboardFormats.CommaSeparated
            ? ","
            : "\t";

        var sb = new StringBuilder();

        foreach (var item in location.Items)
        {
            if (includeDescriptions)
            {
                var desc = string.IsNullOrWhiteSpace(item.Description)
                    ? _dictionaryService.GetDescription(item.ItemNumber)
                    : item.Description;
                sb.AppendLine($"{item.ItemNumber}{separator}{item.Quantity}{separator}{desc}");
            }
            else
            {
                sb.AppendLine($"{item.ItemNumber}{separator}{item.Quantity}");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds clipboard text for an item's redistribution data (all location allocations).
    /// Format: Quantity, Location for each store allocation.
    /// Suitable for pasting into Business Central for redistribution/transfer orders.
    /// </summary>
    /// <param name="item">The item allocation view to copy.</param>
    /// <param name="clipboardFormat">The clipboard format (TabSeparated or CommaSeparated).</param>
    /// <returns>The formatted clipboard text, or null if item has no allocations.</returns>
    public string? BuildItemRedistributionClipboardText(
        ItemAllocationView? item,
        string clipboardFormat)
    {
        if (item == null || item.StoreAllocations.Count == 0)
        {
            return null;
        }

        var separator = clipboardFormat == AllocationBuddyConstants.ClipboardFormats.CommaSeparated
            ? ","
            : "\t";

        var sb = new StringBuilder();

        // Add each store allocation
        foreach (var allocation in item.StoreAllocations)
        {
            sb.AppendLine($"{allocation.Quantity}{separator}{allocation.StoreCode}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Copies location data to the system clipboard.
    /// </summary>
    /// <param name="location">The location to copy.</param>
    /// <param name="includeDescriptions">Whether to include item descriptions.</param>
    /// <param name="clipboardFormat">The clipboard format (TabSeparated or CommaSeparated).</param>
    /// <returns>Success message or error message.</returns>
    public string CopyLocationToClipboard(
        LocationAllocation? location,
        bool includeDescriptions,
        string clipboardFormat)
    {
        var text = BuildLocationClipboardText(location, includeDescriptions, clipboardFormat);

        if (text == null)
        {
            return "No items to copy";
        }

        try
        {
            System.Windows.Clipboard.SetText(text);
            _logger?.LogInformation("Copied {Count} items from location '{Location}' to clipboard",
                location!.Items.Count, location.Location);
            return $"Copied {location.Items.Count} items for '{location.Location}' to clipboard";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to copy location to clipboard");
            return $"Failed to copy: {ex.Message}";
        }
    }

    /// <summary>
    /// Copies item redistribution data to the system clipboard.
    /// </summary>
    /// <param name="item">The item allocation view to copy.</param>
    /// <param name="clipboardFormat">The clipboard format (TabSeparated or CommaSeparated).</param>
    /// <returns>Success message or error message.</returns>
    public string CopyItemRedistributionToClipboard(
        ItemAllocationView? item,
        string clipboardFormat)
    {
        var text = BuildItemRedistributionClipboardText(item, clipboardFormat);

        if (text == null)
        {
            return "No allocations to copy";
        }

        try
        {
            System.Windows.Clipboard.SetText(text);
            _logger?.LogInformation("Copied item '{ItemNumber}' redistribution to clipboard ({Count} locations)",
                item!.ItemNumber, item.StoreAllocations.Count);
            return $"Copied {item.ItemNumber} redistribution to {item.StoreAllocations.Count} locations";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to copy item redistribution to clipboard");
            return $"Failed to copy: {ex.Message}";
        }
    }
}
