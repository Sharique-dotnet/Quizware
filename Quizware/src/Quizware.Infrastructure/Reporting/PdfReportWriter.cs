using System.Globalization;
using System.Text;
using Quizware.Application.Reporting;

namespace Quizware.Infrastructure.Reporting;

/// <summary>A dependency-free PDF 1.4 writer for tabular reports: A4
/// landscape, the standard Helvetica and Courier fonts, tables in Courier so
/// columns line up exactly, long cells wrapped (up to <see cref="MaxCellLines"/>
/// lines, then cut), and the header row repeated on every page.
/// The standard fonts cover Latin-1 (WinAnsi) only — any other character
/// (e.g. Urdu) prints as '?'; the Excel and CSV exports carry the full text.</summary>
internal static class PdfReportWriter
{
    private const double PageWidth = 842;
    private const double PageHeight = 595;
    private const double Margin = 36;
    private const double Bottom = 40;
    private const double TableFontSize = 7.5;
    private const double TableLineHeight = 9.5;
    private const double CharWidth = TableFontSize * 0.6;
    private const int MaxCellLines = 4;
    private const string Gap = "  ";

    private static readonly int LineChars = (int)((PageWidth - (2 * Margin)) / CharWidth);

    public static byte[] Write(ReportDocument document)
    {
        var layout = new Layout();
        layout.Text("F1", 14, document.Title, 22);
        foreach (var section in document.Sections)
        {
            WriteSection(layout, section);
        }

        return Serialize(layout.Finish(), document.Title);
    }

    private static void WriteSection(Layout layout, ReportSection section)
    {
        layout.EnsureSpace(16 + (3 * TableLineHeight));
        layout.Gap(6);
        layout.Text("F1", 10, section.Name, 14);
        if (section.Columns.Count == 0)
        {
            return;
        }

        var widths = ColumnWidths(section);
        var header = Lines(section.Columns, widths);
        void Header()
        {
            foreach (var line in header)
            {
                layout.Text("F3", TableFontSize, line, TableLineHeight);
            }

            layout.Rule();
        }

        Header();
        if (section.Rows.Count == 0)
        {
            layout.Text("F2", TableFontSize, "(none)", TableLineHeight);
            return;
        }

        foreach (var row in section.Rows)
        {
            var lines = Lines(row, widths);
            if (layout.EnsureSpace(lines.Count * TableLineHeight))
            {
                Header();
            }

            foreach (var line in lines)
            {
                layout.Text("F2", TableFontSize, line, TableLineHeight);
            }
        }
    }

    /// <summary>Each column gets the width of its widest cell (header
    /// included); when the row is too wide for the page, every column over its
    /// fair share is cut back to it.</summary>
    private static int[] ColumnWidths(ReportSection section)
    {
        var count = section.Columns.Count;
        var natural = Enumerable.Range(0, count)
            .Select(c => Math.Max(4, Math.Max(section.Columns[c].Length, section.Rows.Select(r => c < r.Count ? r[c].Length : 0).DefaultIfEmpty(0).Max())))
            .ToArray();
        var available = LineChars - (Gap.Length * (count - 1));
        while (natural.Sum() > available)
        {
            var remaining = available - natural.Where(w => w <= available / count).Sum();
            var wide = natural.Count(w => w > available / count);
            var share = Math.Max(4, wide == 0 ? available / count : remaining / wide);
            var changed = false;
            for (var c = 0; c < count; c++)
            {
                if (natural[c] > share)
                {
                    natural[c] = share;
                    changed = true;
                }
            }

            if (!changed)
            {
                break;
            }
        }

        return natural;
    }

    private static List<string> Lines(IReadOnlyList<string> cells, int[] widths)
    {
        var wrapped = widths.Select((w, c) => Wrap(c < cells.Count ? cells[c] : string.Empty, w)).ToList();
        var height = wrapped.Max(w => w.Count);
        return Enumerable.Range(0, height)
            .Select(i => string.Join(Gap, wrapped.Select((cell, c) => (i < cell.Count ? cell[i] : string.Empty).PadRight(widths[c]))).TrimEnd())
            .ToList();
    }

    private static List<string> Wrap(string text, int width)
    {
        var lines = new List<string>();
        var rest = text.ReplaceLineEndings(" ").Trim();
        while (rest.Length > 0 && lines.Count < MaxCellLines)
        {
            if (rest.Length <= width)
            {
                lines.Add(rest);
                rest = string.Empty;
                break;
            }

            var cut = rest.LastIndexOf(' ', width);
            cut = cut <= 0 ? width : cut;
            lines.Add(rest[..cut].TrimEnd());
            rest = rest[cut..].TrimStart();
        }

        if (rest.Length > 0)
        {
            var last = lines[^1];
            lines[^1] = (last.Length >= width ? last[..(width - 1)] : last) + "~";
        }

        return lines.Count == 0 ? [string.Empty] : lines;
    }

    /// <summary>Writes the objects and the cross-reference table. Fonts: F1
    /// Helvetica-Bold (titles), F2 Courier (cells), F3 Courier-Bold (headers),
    /// F4 Helvetica (footer).</summary>
    private static byte[] Serialize(IReadOnlyList<string> pages, string title)
    {
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            string.Empty,
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Courier /Encoding /WinAnsiEncoding >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Courier-Bold /Encoding /WinAnsiEncoding >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            $"<< /Title <FEFF{Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(title))}> /Producer (Quizware) >>",
        };
        const int firstPageObject = 8;
        var kids = new List<string>();
        for (var p = 0; p < pages.Count; p++)
        {
            var footer = $"BT /F4 7 Tf {Num(Margin)} 20 Td ({Escape($"{title} - page {p + 1} of {pages.Count}")}) Tj ET\n";
            var content = pages[p] + footer;
            var pageObject = firstPageObject + (p * 2);
            kids.Add($"{pageObject} 0 R");
            objects.Add(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {Num(PageWidth)} {Num(PageHeight)}] " +
                $"/Resources << /Font << /F1 3 0 R /F2 4 0 R /F3 5 0 R /F4 6 0 R >> >> /Contents {pageObject + 1} 0 R >>");
            objects.Add($"<< /Length {Encoding.Latin1.GetByteCount(content)} >>\nstream\n{content}endstream");
        }

        objects[1] = $"<< /Type /Pages /Kids [{string.Join(' ', kids)}] /Count {pages.Count} >>";

        using var stream = new MemoryStream();
        void Emit(string text)
        {
            var bytes = Encoding.Latin1.GetBytes(text);
            stream.Write(bytes);
        }

        Emit("%PDF-1.4\n%âãÏÓ\n");
        var offsets = new List<long>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(stream.Position);
            Emit($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = stream.Position;
        var table = new StringBuilder($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            table.Append(offset.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }

        Emit(table.ToString());
        Emit($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R /Info 7 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return stream.ToArray();
    }

    /// <summary>A content-stream literal string body: WinAnsi characters only (others
    /// become '?'), with the delimiters escaped.</summary>
    internal static string Escape(string text)
    {
        var escaped = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            var mapped = ch switch
            {
                '—' => '\u0097',
                '–' => '\u0096',
                '…' => '\u0085',
                '‘' => '\u0091',
                '’' => '\u0092',
                '“' => '\u0093',
                '”' => '\u0094',
                _ when ch is >= ' ' and <= '~' => ch,
                _ when ch is >= ' ' and <= 'ÿ' => ch,
                _ => '?',
            };
            if (mapped is '(' or ')' or '\\')
            {
                escaped.Append('\\');
            }

            escaped.Append(mapped);
        }

        return escaped.ToString();
    }

    private static string Num(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Places lines top to bottom and starts a new page when one is full.</summary>
    private sealed class Layout
    {
        private readonly List<string> _pages = [];
        private StringBuilder _page = new();
        private double _y = PageHeight - Margin;

        public void Text(string font, double size, string text, double lineHeight)
        {
            EnsureSpace(lineHeight);
            _y -= lineHeight;
            _page.Append($"BT /{font} {Num(size)} Tf {Num(Margin)} {Num(_y)} Td ({Escape(text)}) Tj ET\n");
        }

        public void Rule()
        {
            _page.Append($"0.5 w {Num(Margin)} {Num(_y - 2)} m {Num(PageWidth - Margin)} {Num(_y - 2)} l S\n");
            _y -= 3;
        }

        public void Gap(double height) => _y -= height;

        /// <summary>Starts a new page when <paramref name="height"/> does not fit; returns whether it did.</summary>
        public bool EnsureSpace(double height)
        {
            if (_y - height >= Bottom)
            {
                return false;
            }

            _pages.Add(_page.ToString());
            _page = new StringBuilder();
            _y = PageHeight - Margin;
            return true;
        }

        public IReadOnlyList<string> Finish()
        {
            _pages.Add(_page.ToString());
            return _pages;
        }
    }
}
