using System.Text;
using Quizware.Application.Reporting;

namespace Quizware.Infrastructure.Reporting;

/// <summary>RFC 4180 CSV, UTF-8 with a BOM so Excel opens Urdu text correctly.
/// Each section is its name on a line, its header row, its rows, then a blank
/// line. Cells that a spreadsheet would run as a formula are prefixed with an
/// apostrophe (CSV injection).</summary>
internal static class CsvReportWriter
{
    public static byte[] Write(ReportDocument document)
    {
        var csv = new StringBuilder();
        csv.Append(Escape(document.Title)).Append("\r\n\r\n");
        foreach (var section in document.Sections)
        {
            csv.Append(Escape(section.Name)).Append("\r\n");
            csv.AppendJoin(',', section.Columns.Select(Escape)).Append("\r\n");
            foreach (var row in section.Rows)
            {
                csv.AppendJoin(',', row.Select(Escape)).Append("\r\n");
            }

            csv.Append("\r\n");
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];
    }

    internal static string Escape(string value)
    {
        if (value.Length > 0 && IsFormulaStart(value))
        {
            value = "'" + value;
        }

        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }

    private static bool IsFormulaStart(string value) =>
        value[0] is '=' or '+' or '@' or '\t' or '\r'
        || (value[0] == '-' && !double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _));
}
