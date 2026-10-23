using Quizware.Application.Reporting;

namespace Quizware.Application.Abstractions;

public enum ReportFormat
{
    Xlsx = 1,
    Csv = 2,
    Pdf = 3,
}

public sealed record ExportedFile(byte[] Content, string ContentType, string FileExtension);

/// <summary>P13-06: renders a <see cref="ReportDocument"/> as a file. Lives in
/// Infrastructure, which owns the Excel library.</summary>
public interface IReportExporter
{
    ExportedFile Export(ReportDocument document, ReportFormat format);
}
