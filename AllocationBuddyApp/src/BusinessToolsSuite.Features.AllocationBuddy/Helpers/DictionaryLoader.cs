using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using BusinessToolsSuite.Infrastructure.Services.Parsers;

namespace BusinessToolsSuite.Features.AllocationBuddy.Helpers
{
    public static class DictionaryLoader
    {
        public static List<DictionaryItem> LoadFromJs(string jsPath)
        {
            var items = new List<DictionaryItem>();
            if (!File.Exists(jsPath)) return items;
            var js = File.ReadAllText(jsPath);
                var match = Regex.Match(js, @"items:\s*\[(.*?)\]\s*,", RegexOptions.Singleline);
            if (!match.Success) return items;
            var itemsBlock = match.Groups[1].Value;
                var itemRegex = new Regex(@"\{\s*number:\s*['""]?([^'""]+)['""]?\s*,\s*desc:\s*['""]?([^'""]+)['""]?\s*,\s*sku:\s*\[(.*?)\]\s*\}", RegexOptions.Singleline);
            foreach (Match m in itemRegex.Matches(itemsBlock))
            {
                var number = m.Groups[1].Value;
                var desc = m.Groups[2].Value;
                var skusRaw = m.Groups[3].Value;
                var skus = Regex.Matches(skusRaw, "\"([^\"]+)\"")
                    .Cast<Match>()
                    .Select(x => x.Groups[1].Value)
                    .ToList();
                items.Add(new DictionaryItem { Number = number, Description = desc, Skus = skus });
            }
            return items;
        }
    }
}
