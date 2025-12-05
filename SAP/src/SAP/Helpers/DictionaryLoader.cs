using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SAP.Infrastructure.Services.Parsers;

namespace SAP.Helpers;

/// <summary>
/// Helper class for loading dictionary data from JavaScript files.
/// </summary>
/// <remarks>
/// <para>
/// This class parses the dictionaries.js file that contains item and store
/// data used for matching and lookups across all modules.
/// </para>
/// <para>
/// The JavaScript file format uses JSON-like structure embedded in a JS object.
/// </para>
/// </remarks>
public static class DictionaryLoader
{
    /// <summary>
    /// Loads item dictionary entries from a JavaScript file.
    /// </summary>
    /// <param name="jsPath">Path to the dictionaries.js file.</param>
    /// <returns>List of dictionary items with numbers, descriptions, and SKUs.</returns>
    public static List<DictionaryItem> LoadItemsFromJs(string jsPath)
    {
        var items = new List<DictionaryItem>();
        if (!File.Exists(jsPath)) return items;
        
        var js = File.ReadAllText(jsPath);
        var match = Regex.Match(js, @"""items"":\s*\[(.*?)\],\s*""stores"":", RegexOptions.Singleline);
        if (!match.Success) return items;
        
        var itemsBlock = match.Groups[1].Value;
        var itemRegex = new Regex(
            @"\{\s*""number"":\s*""([^""]+)"",\s*""desc"":\s*""([^""]+)"",\s*""sku"":\s*\[(.*?)\]\s*\}",
            RegexOptions.Singleline);
            
        foreach (Match m in itemRegex.Matches(itemsBlock))
        {
            var number = m.Groups[1].Value;
            var desc = m.Groups[2].Value;
            var skusRaw = m.Groups[3].Value;
            var skus = Regex.Matches(skusRaw, "\"([^\"]+)\"")
                .Select(x => x.Groups[1].Value)
                .ToList();
            items.Add(new DictionaryItem { Number = number, Description = desc, Skus = skus });
        }
        
        return items;
    }

    /// <summary>
    /// Loads store entries from a JavaScript file.
    /// </summary>
    /// <param name="jsPath">Path to the dictionaries.js file.</param>
    /// <returns>List of store entries with codes, names, and ranks.</returns>
    public static List<StoreEntry> LoadStoresFromJs(string jsPath)
    {
        var stores = new List<StoreEntry>();
        if (!File.Exists(jsPath)) return stores;
        
        var js = File.ReadAllText(jsPath);
        var match = Regex.Match(js, @"""stores"":\s*\[(.*?)\]", RegexOptions.Singleline);
        if (!match.Success) return stores;
        
        var storesBlock = match.Groups[1].Value;
        var storeRegex = new Regex(
            @"\{\s*""id"":\s*(\d+),\s*""name"":\s*""([^""]+)"",\s*""rank"":\s*""([^""]+)""\s*\}",
            RegexOptions.Singleline);
            
        foreach (Match m in storeRegex.Matches(storesBlock))
        {
            var id = m.Groups[1].Value;
            var name = m.Groups[2].Value;
            var rank = m.Groups[3].Value;
            stores.Add(new StoreEntry { Code = id, Name = name, Rank = rank });
        }
        
        return stores;
    }

    /// <summary>
    /// Loads item dictionary entries from a JavaScript file.
    /// </summary>
    /// <param name="jsPath">Path to the dictionaries.js file.</param>
    /// <returns>List of dictionary items.</returns>
    /// <remarks>Legacy compatibility alias for <see cref="LoadItemsFromJs"/>.</remarks>
    public static List<DictionaryItem> LoadFromJs(string jsPath) => LoadItemsFromJs(jsPath);
}
