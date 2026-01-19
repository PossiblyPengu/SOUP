using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Data.Sqlite;
using System.Text.Json;

class Program
{
    static void Main()
    {
        var path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\SOUP\\OrderLog\\orders.db";
        Console.WriteLine("DB: " + path);
        var cs = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Cache = SqliteCacheMode.Shared }.ToString();
        using var conn = new SqliteConnection(cs);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Data FROM Orders ORDER BY SortOrder ASC";
        using var rdr = cmd.ExecuteReader();
        var list = new List<(string json, string lg)>();
        while (rdr.Read())
        {
            var json = rdr.GetString(0);
            string lg = "(missing)";
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("LinkedGroupId", out var prop))
                {
                    lg = prop.ValueKind switch
                    {
                        JsonValueKind.String => prop.GetString() ?? "(null)",
                        JsonValueKind.Null => "(null)",
                        _ => prop.ToString()
                    };
                }
                else lg = "(absent)";
            }
            catch (Exception ex)
            {
                lg = "(json error)";
            }
            list.Add((json, lg));
        }
        Console.WriteLine($"Total rows: {list.Count}");
        var grouped = list.GroupBy(x => x.lg).OrderByDescending(g => g.Count()).ToList();
        Console.WriteLine("Top LinkedGroupId buckets:");
        foreach (var g in grouped.Take(20))
        {
            Console.WriteLine($"'{g.Key}' -> {g.Count()} items");
        }

        // Now emulate the grouping used by the app: filter to active, renderable items and group
        var parsed = new List<(string id, string? linked, bool isArchived, bool isSticky, string vendor, string transfer, string whs, string note)>();
        foreach (var (json, lg) in list)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var id = root.GetProperty("Id").GetString() ?? "";
                var linked = root.TryGetProperty("LinkedGroupId", out var p) && p.ValueKind==JsonValueKind.String ? p.GetString() : null;
                var isArchived = root.TryGetProperty("IsArchived", out var pa) && pa.GetBoolean();
                var noteType = root.TryGetProperty("NoteType", out var nt) && nt.ValueKind==JsonValueKind.Number ? nt.GetInt32() : 0;
                var isSticky = noteType==1;
                var vendor = root.TryGetProperty("VendorName", out var v) ? v.GetString() ?? string.Empty : string.Empty;
                var transfer = root.TryGetProperty("TransferNumbers", out var t) ? t.GetString() ?? string.Empty : string.Empty;
                var whs = root.TryGetProperty("WhsShipmentNumbers", out var w) ? w.GetString() ?? string.Empty : string.Empty;
                var note = root.TryGetProperty("NoteContent", out var nc) ? nc.GetString() ?? string.Empty : string.Empty;
                parsed.Add((id, linked, isArchived, isSticky, vendor, transfer, whs, note));
            }
            catch { }
        }

        var activeRenderable = parsed.Where(p => !p.isArchived && (!string.IsNullOrWhiteSpace(p.vendor) || !string.IsNullOrWhiteSpace(p.transfer) || !string.IsNullOrWhiteSpace(p.whs) || !string.IsNullOrWhiteSpace(p.note) || p.isSticky)).ToList();
        Console.WriteLine($"\nActive + renderable count: {activeRenderable.Count}");

        var groups = activeRenderable.GroupBy(p => string.IsNullOrEmpty(p.linked) ? "(null)" : p.linked).Select(g => new { Key = g.Key, Count = g.Count(), Ids = g.Select(x=>x.id).ToList() }).OrderByDescending(x=>x.Count).ToList();
        Console.WriteLine("\nGroups (active+renderable):");
        foreach (var g in groups.Take(50))
        {
            Console.WriteLine($"'{g.Key}' -> {g.Count} items (ids: {string.Join(',', g.Ids)})");
        }

        Console.WriteLine("\nGroups with exactly 1 member:");
        foreach (var g in groups.Where(x=>x.Count==1))
        {
            Console.WriteLine($"Key: '{g.Key}' Id: {g.Ids.First()}");
        }

        // Inspect specific vendor examples if present
        var lookFor = new[] { "FULL CIRCLE", "WICKED" };
        Console.WriteLine("\nSearch for example vendors:");
        foreach (var term in lookFor)
        {
            var found = parsed.Where(p => p.vendor.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            Console.WriteLine($"Term '{term}' -> {found.Count} matches");
            foreach (var f in found)
            {
                Console.WriteLine($"Id:{f.id} Linked:{f.linked} Archived:{f.isArchived} Sticky:{f.isSticky} Vendor:'{f.vendor}'");
            }
        }
    }
}
