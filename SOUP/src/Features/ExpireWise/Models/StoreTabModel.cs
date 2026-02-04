namespace SOUP.Features.ExpireWise.Models;

/// <summary>
/// Model for store tabs in ExpireWise, wrapping StoreEntity with item count.
/// </summary>
public partial class StoreTabModel : ObservableObject
{
    /// <summary>
    /// Gets or sets the store code.
    /// </summary>
    public string Code { get; set; } = "";

    /// <summary>
    /// Gets or sets the store name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the store rank.
    /// </summary>
    public string Rank { get; set; } = "";

    /// <summary>
    /// Gets or sets the count of expiring items at this store.
    /// </summary>
    [ObservableProperty]
    private int _itemCount;

    /// <summary>
    /// Gets or sets whether this store tab is currently selected.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Gets the display name for dropdowns (Code - Name format).
    /// </summary>
    public string DisplayName => string.IsNullOrEmpty(Code) ? Name : $"{Code} - {Name}";

    /// <summary>
    /// Creates a StoreTabModel from a StoreEntity.
    /// </summary>
    public static StoreTabModel FromEntity(Data.Entities.StoreEntity entity, int itemCount = 0)
    {
        return new StoreTabModel
        {
            Code = entity.Code,
            Name = entity.Name,
            Rank = entity.Rank,
            ItemCount = itemCount
        };
    }
}

/// <summary>
/// Model for store dropdown items, including the "All Stores" option.
/// </summary>
public partial class StoreDropdownItem : ObservableObject
{
    /// <summary>
    /// Gets or sets the store code (empty for "All Stores").
    /// </summary>
    public string Code { get; set; } = "";

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string DisplayName { get; set; } = "";

    /// <summary>
    /// Gets or sets the count of expiring items.
    /// </summary>
    [ObservableProperty]
    private int _itemCount;

    /// <summary>
    /// Gets whether this is the "All Stores" option.
    /// </summary>
    public bool IsAllStores => string.IsNullOrEmpty(Code);

    /// <summary>
    /// Creates an "All Stores" dropdown item.
    /// </summary>
    public static StoreDropdownItem CreateAllStores(int itemCount = 0)
    {
        return new StoreDropdownItem
        {
            Code = "",
            DisplayName = "All Stores",
            ItemCount = itemCount
        };
    }

    /// <summary>
    /// Creates a store dropdown item from a StoreTabModel.
    /// </summary>
    public static StoreDropdownItem FromStoreTab(StoreTabModel tab)
    {
        return new StoreDropdownItem
        {
            Code = tab.Code,
            DisplayName = tab.DisplayName,
            ItemCount = tab.ItemCount
        };
    }
}
