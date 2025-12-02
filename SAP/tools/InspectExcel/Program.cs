using ClosedXML.Excel;

var filePath = args.Length > 0 ? args[0] : @"d:\CODE\important files\STORE_INFO\RANKS.xlsx";

Console.WriteLine($"Inspecting: {filePath}");
Console.WriteLine();

using var workbook = new XLWorkbook(filePath);
var worksheet = workbook.Worksheets.First();

Console.WriteLine($"Worksheet: {worksheet.Name}");
Console.WriteLine($"Last row used: {worksheet.LastRowUsed()?.RowNumber()}");
Console.WriteLine($"Last column used: {worksheet.LastColumnUsed()?.ColumnNumber()}");
Console.WriteLine();

Console.WriteLine("First 30 rows:");
var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
var lastCol = worksheet.LastColumnUsed()?.ColumnNumber() ?? 1;

for (int r = 1; r <= Math.Min(30, lastRow); r++)
{
    var vals = new List<string>();
    for (int c = 1; c <= Math.Min(6, lastCol); c++)
    {
        vals.Add(worksheet.Cell(r, c).GetString());
    }
    Console.WriteLine($"Row {r}: {string.Join(" | ", vals)}");
}

Console.WriteLine();
Console.WriteLine($"Total rows: {lastRow}");
