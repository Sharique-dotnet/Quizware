using System.Globalization;
using FluentValidation;
using MediatR;
using Quizware.Application.Abstractions;

namespace Quizware.Application.Reporting;

public sealed record ExportedReportDto(string FileName, string ContentType, byte[] Content);

/// <summary>P13-06: exports one report as Excel, CSV or PDF. <see cref="Id"/>
/// is the match, stage or team the report is about (for question-usage, an
/// optional stage); the audit export takes the same filters as the audit query.</summary>
public sealed record ExportReportQuery(
    Guid ProgramId,
    string ReportType,
    string Format,
    Guid? Id,
    bool FlaggedOnly,
    AuditLogFilter? AuditFilter) : IRequest<ExportedReportDto>;

public static class ReportTypes
{
    public const string Match = "match";
    public const string Stage = "stage";
    public const string Team = "team";
    public const string QuestionUsage = "question-usage";
    public const string Audit = "audit";

    public static readonly IReadOnlyList<string> All = [Match, Stage, Team, QuestionUsage, Audit];

    public static bool RequiresId(string reportType) => reportType is Match or Stage or Team;
}

public sealed class ExportReportQueryValidator : AbstractValidator<ExportReportQuery>
{
    /// <summary>An audit export is one file, not a page: up to this many rows, newest first.</summary>
    public const int MaxAuditRows = 10_000;

    public ExportReportQueryValidator()
    {
        RuleFor(x => x.ReportType)
            .Must(t => ReportTypes.All.Contains(t))
            .WithMessage($"Report type must be one of: {string.Join(", ", ReportTypes.All)}.");
        RuleFor(x => x.Format)
            .Must(f => Enum.TryParse<ReportFormat>(f, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
            .WithMessage("Format must be xlsx, csv or pdf.");
        RuleFor(x => x.Id)
            .NotNull()
            .When(x => ReportTypes.RequiresId(x.ReportType))
            .WithMessage("This report needs the id of the match, stage or team it is about.");
        RuleFor(x => x.AuditFilter!.Action)
            .Must(a => a is null || a is "Insert" or "Update" or "Delete")
            .When(x => x.AuditFilter is not null)
            .WithMessage("Action must be Insert, Update or Delete.");
    }
}

public sealed class ExportReportQueryHandler : IRequestHandler<ExportReportQuery, ExportedReportDto>
{
    private readonly IReportingService _reports;
    private readonly IAuditLogReader _audit;
    private readonly IReportExporter _exporter;
    private readonly IClock _clock;

    public ExportReportQueryHandler(IReportingService reports, IAuditLogReader audit, IReportExporter exporter, IClock clock)
    {
        _reports = reports;
        _audit = audit;
        _exporter = exporter;
        _clock = clock;
    }

    public async Task<ExportedReportDto> Handle(ExportReportQuery request, CancellationToken cancellationToken)
    {
        var document = request.ReportType switch
        {
            ReportTypes.Match => ReportDocuments.Match(await _reports.MatchAsync(request.ProgramId, request.Id!.Value, cancellationToken)),
            ReportTypes.Stage => ReportDocuments.Stage(await _reports.StageAsync(request.ProgramId, request.Id!.Value, cancellationToken)),
            ReportTypes.Team => ReportDocuments.Team(await _reports.TeamAsync(request.ProgramId, request.Id!.Value, cancellationToken)),
            ReportTypes.QuestionUsage => ReportDocuments.QuestionUsage(
                await _reports.QuestionUsageAsync(request.ProgramId, request.Id, request.FlaggedOnly, cancellationToken)),
            _ => ReportDocuments.Audit(await _audit.QueryAsync(
                (request.AuditFilter ?? new AuditLogFilter(request.ProgramId, null, null, null, null, null, null, 1, 1))
                    with { ProgramId = request.ProgramId, Page = 1, PageSize = ExportReportQueryValidator.MaxAuditRows },
                cancellationToken)),
        };

        var format = Enum.Parse<ReportFormat>(request.Format, ignoreCase: true);
        var file = _exporter.Export(document, format);
        var subject = request.Id is { } id ? $"-{id.ToString("N")[..8]}" : string.Empty;
        var stamp = _clock.UtcNow.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
        return new ExportedReportDto($"{request.ReportType}{subject}-{stamp}.{file.FileExtension}", file.ContentType, file.Content);
    }
}
