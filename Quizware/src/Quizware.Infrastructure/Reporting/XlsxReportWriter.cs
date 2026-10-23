using System.Globalization;
using ClosedXML.Excel;
using Quizware.Application.Reporting;

namespace Quizware.Infrastructure.Reporting;

/// <summary>One worksheet per section, a bold frozen header row, and numbers
/// stored as numbers. Values are always written as values, never formulas.</summary>
internal static class XlsxReportWriter
{
    public static byte[] Write(ReportDocument document)
    {
        using var workbook = new XLWorkbook();
        workbook.Properties.Title = document.Title;
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var section in document.Sections)
        {
            var sheet = workbook.Worksheets.Add(SheetName(section.Name, used));
            for (var c = 0; c < section.Columns.Count; c++)
            {
                sheet.Cell(1, c + 1).Value = section.Columns[c];
            }

            sheet.Row(1).Style.Font.Bold = true;
            sheet.SheetView.FreezeRows(1);
            for (var r = 0; r < section.Rows.Count; r++)
            {
                var row = section.Rows[r];
                for (var c = 0; c < row.Count; c++)
                {
                    var cell = sheet.Cell(r + 2, c + 1);
                    if (double.TryParse(row[c], NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
                    {
                        cell.Value = number;
                    }
                    else
                    {
                        cell.Value = row[c];
                    }
                }
            }

            sheet.Columns().AdjustToContents(1, Math.Min(section.Rows.Count + 1, 200), 8, 80);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>Excel sheet names: at most 31 characters, none of : \ / ? * [ ], unique.</summary>
    private static string SheetName(string name, HashSet<string> used)
    {
        var clean = new string(name.Select(ch => ":\\/?*[]".Contains(ch) ? ' ' : ch).ToArray()).Trim();
        clean = clean.Length == 0 ? "Sheet" : clean[..Math.Min(clean.Length, 31)];
        var candidate = clean;
        for (var i = 2; !used.Add(candidate); i++)
        {
            var suffix = $" ({i})";
            candidate = clean[..Math.Min(clean.Length, 31 - suffix.Length)] + suffix;
        }

        return candidate;
    }
}
