using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BusinessToolsSuite.Infrastructure.Services.Parsers;

namespace BusinessToolsSuite.WPF.Helpers;

public static class DictionaryLoader
{
    public static List<DictionaryItem> LoadItemsFromJs(string jsPath)
    {
        var items = new List<DictionaryItem>();
        if (!File.Exists(jsPath)) return items;
        var js = File.ReadAllText(jsPath);
        var match = Regex.Match(js, @"""items"":\s*\[(.*?)\],\s*""stores"":", RegexOptions.Singleline);
        if (!match.Success) return items;
        var itemsBlock = match.Groups[1].Value;
        var itemRegex = new Regex(@"\{\s*""number"":\s*""([^""]+)"",\s*""desc"":\s*""([^""]+)"",\s*""sku"":\s*\[(.*?)\]\s*\}", RegexOptions.Singleline);
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

    public static List<StoreEntry> LoadStoresFromJs(string jsPath)
    {
        var stores = new List<StoreEntry>();
        if (!File.Exists(jsPath)) return stores;
        var js = File.ReadAllText(jsPath);
        var match = Regex.Match(js, @"""stores"":\s*\[(.*?)\]", RegexOptions.Singleline);
        if (!match.Success) return stores;
        var storesBlock = match.Groups[1].Value;
        var storeRegex = new Regex(@"\{\s*""id"":\s*(\d+),\s*""name"":\s*""([^""]+)"",\s*""rank"":\s*""([^""]+)""\s*\}", RegexOptions.Singleline);
        foreach (Match m in storeRegex.Matches(storesBlock))
        {
            var id = m.Groups[1].Value;
            var name = m.Groups[2].Value;
            var rank = m.Groups[3].Value;
            stores.Add(new StoreEntry { Code = id, Name = name, Rank = rank });
        }
        return stores;
    }

    // Legacy compatibility
    public static List<DictionaryItem> LoadFromJs(string jsPath) => LoadItemsFromJs(jsPath);
}
