using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BusinessToolsSuite.Infrastructure.Services.Parsers;

namespace BusinessToolsSuite.WPF.Data;

public static class InternalStoreDictionary
{
    public static List<StoreEntry> GetStores()
    {
        // Try to load from saved custom stores first
        try
        {
            var storesPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BusinessToolsSuite",
                "AllocationBuddy",
                "stores.json"
            );

            if (File.Exists(storesPath))
            {
                var json = File.ReadAllText(storesPath);
                var savedStores = JsonSerializer.Deserialize<List<StoreEntry>>(json);
                if (savedStores != null && savedStores.Count > 0)
                {
                    return savedStores;
                }
            }
        }
        catch
        {
            // Fall through to default stores
        }

        // Return default stores if no custom stores file exists
        return GetDefaultStores();
    }

    public static List<StoreEntry> GetDefaultStores()
    {
        return new List<StoreEntry>
        {
            new() { Code = "101", Name = "WATERLOO 1", Rank = "C" },
            new() { Code = "102", Name = "KITCHENER 1", Rank = "B" },
            new() { Code = "103", Name = "CAMBRIDGE", Rank = "B" },
            new() { Code = "104", Name = "LONDON 1", Rank = "B" },
            new() { Code = "105", Name = "LONDON 2", Rank = "B" },
            new() { Code = "106", Name = "HAMILTON 1", Rank = "C" },
            new() { Code = "107", Name = "MISSISSAUGA", Rank = "A" },
            new() { Code = "108", Name = "ST CATHARINES", Rank = "B" },
            new() { Code = "109", Name = "HAMILTON 2", Rank = "A" },
            new() { Code = "110", Name = "TORONTO 1", Rank = "A" },
            new() { Code = "111", Name = "SCARBOROUGH", Rank = "A" },
            new() { Code = "112", Name = "BRAMPTON", Rank = "A" },
            new() { Code = "113", Name = "BARRIE", Rank = "B" },
            new() { Code = "114", Name = "OSHAWA", Rank = "B" },
            new() { Code = "115", Name = "OTTAWA 1", Rank = "A" },
            new() { Code = "116", Name = "KINGSTON", Rank = "C" },
            new() { Code = "117", Name = "PETERBOROUGH", Rank = "C" },
            new() { Code = "118", Name = "GUELPH", Rank = "B" },
            new() { Code = "119", Name = "BRANTFORD", Rank = "C" },
            new() { Code = "120", Name = "WINDSOR", Rank = "B" },
            new() { Code = "121", Name = "SARNIA", Rank = "C" },
            new() { Code = "122", Name = "SUDBURY", Rank = "C" },
            new() { Code = "123", Name = "THUNDER BAY", Rank = "C" },
            new() { Code = "124", Name = "NORTH BAY", Rank = "C" },
            new() { Code = "125", Name = "SAULT STE MARIE", Rank = "C" },
            new() { Code = "126", Name = "OTTAWA 2", Rank = "A" },
            new() { Code = "127", Name = "TORONTO 2", Rank = "A" },
            new() { Code = "128", Name = "VAUGHAN", Rank = "A" },
            new() { Code = "129", Name = "MARKHAM", Rank = "A" },
            new() { Code = "130", Name = "BURLINGTON", Rank = "B" },
            new() { Code = "131", Name = "OAKVILLE", Rank = "A" },
            new() { Code = "132", Name = "AJAX", Rank = "B" },
            new() { Code = "133", Name = "PICKERING", Rank = "B" },
            new() { Code = "134", Name = "WHITBY", Rank = "B" },
            new() { Code = "135", Name = "ETOBICOKE", Rank = "A" }
        };
    }
}
