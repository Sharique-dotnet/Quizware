using ClosedXML.Excel;

namespace Quizware.Infrastructure.Imports;

/// <summary>P6-19: "column template differs per format" — MCQ is built as
/// the reference implementation (the same build-one-format-first strategy
/// this project already uses for the match engine in Phase 9); extending
/// to the other 9 formats is mechanical repetition of this exact pattern
/// once needed. Format-only: no business validation here.</summary>
public sealed record McqImportRawRow(
    int RowNumber,
    string? QuestionText,
    string? DifficultyLevelId,
    string? Language,
    string? Option1,
    string? Option1Correct,
    string? Option2,
    string? Option2Correct,
    string? Option3,
    string? Option3Correct,
    string? Option4,
    string? Option4Correct);

public static class McqQuestionExcelParser
{
    private static readonly string[] Headers =
    [
        "QuestionText", "DifficultyLevelId", "Language",
        "Option1", "Option1Correct", "Option2", "Option2Correct",
        "Option3", "Option3Correct", "Option4", "Option4Correct",
    ];

    public static IReadOnlyList<McqImportRawRow> Parse(Stream excelFile)
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

        var rows = new List<McqImportRawRow>();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var row = sheet.Row(rowNumber);
            if (row.IsEmpty())
            {
                continue;
            }

            rows.Add(new McqImportRawRow(
                rowNumber,
                ReadCell(row, columnIndexByHeader, "QuestionText"),
                ReadCell(row, columnIndexByHeader, "DifficultyLevelId"),
                ReadCell(row, columnIndexByHeader, "Language"),
                ReadCell(row, columnIndexByHeader, "Option1"),
                ReadCell(row, columnIndexByHeader, "Option1Correct"),
                ReadCell(row, columnIndexByHeader, "Option2"),
                ReadCell(row, columnIndexByHeader, "Option2Correct"),
                ReadCell(row, columnIndexByHeader, "Option3"),
                ReadCell(row, columnIndexByHeader, "Option3Correct"),
                ReadCell(row, columnIndexByHeader, "Option4"),
                ReadCell(row, columnIndexByHeader, "Option4Correct")));
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
