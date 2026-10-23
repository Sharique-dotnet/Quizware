using Quizware.Application.Abstractions;
using Quizware.Application.Reporting;

namespace Quizware.Infrastructure.Reporting;

public sealed class ReportExporter : IReportExporter
{
    public ExportedFile Export(ReportDocument document, ReportFormat format) => format switch
    {
        ReportFormat.Xlsx => new ExportedFile(
            XlsxReportWriter.Write(document), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx"),
        ReportFormat.Csv => new ExportedFile(CsvReportWriter.Write(document), "text/csv; charset=utf-8", "csv"),
        ReportFormat.Pdf => new ExportedFile(PdfReportWriter.Write(document), "application/pdf", "pdf"),
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported report format."),
    };
}
