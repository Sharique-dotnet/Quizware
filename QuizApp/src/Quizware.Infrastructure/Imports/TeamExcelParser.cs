using ClosedXML.Excel;

namespace Quizware.Infrastructure.Imports;

/// <summary>The Code/SchoolName/DisplayName/ShortName/ContactName/
/// ContactPhone/ContactEmail column template isn't documented anywhere —
/// derived directly from the Team schema. Column matching is
/// case-insensitive; unknown columns are ignored. Format-only: no business
/// validation (uniqueness, required fields) happens here, since that needs
/// database access the caller has and this parser deliberately doesn't.</summary>
public sealed record TeamImportRawRow(
    int RowNumber,
    string? Code,
    string? SchoolName,
    string? DisplayName,
    string? ShortName,
    string? ContactName,
    string? ContactPhone,
    string? ContactEmail);

public static class TeamExcelParser
{
    private static readonly string[] Headers =
        ["Code", "SchoolName", "DisplayName", "ShortName", "ContactName", "ContactPhone", "ContactEmail"];

    public static IReadOnlyList<TeamImportRawRow> Parse(Stream excelFile)
    {
        using var workbook = new XLWorkbook(excelFile);
        var sheet = workbook.Worksheets.First();
        var headerRow = sheet.Row(1);

        var columnIndexByHeader = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.CellsUsed())
        {
            var header = cell.GetString().Trim();
            if (Headers.Contains(header, StringComparer.OrdinalIgnoreCase))
            {
                columnIndexByHeader[header] = cell.Address.ColumnNumber;
            }
        }

        var rows = new List<TeamImportRawRow>();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var row = sheet.Row(rowNumber);
            if (row.IsEmpty())
            {
                continue;
            }

            rows.Add(new TeamImportRawRow(
                rowNumber,
                ReadCell(row, columnIndexByHeader, "Code"),
                ReadCell(row, columnIndexByHeader, "SchoolName"),
                ReadCell(row, columnIndexByHeader, "DisplayName"),
                ReadCell(row, columnIndexByHeader, "ShortName"),
                ReadCell(row, columnIndexByHeader, "ContactName"),
                ReadCell(row, columnIndexByHeader, "ContactPhone"),
                ReadCell(row, columnIndexByHeader, "ContactEmail")));
        }

        return rows;
    }

    private static string? ReadCell(IXLRow row, IReadOnlyDictionary<string, int> columnIndexByHeader, string header)
    {
        if (!columnIndexByHeader.TryGetValue(header, out var columnNumber))
        {
            return null;
        }

        var value = row.Cell(columnNumber).GetString().Trim();
        return value.Length == 0 ? null : value;
    }
}
