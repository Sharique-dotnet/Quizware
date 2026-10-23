using MediatR;
using Quizware.Application.Reporting.Dtos;

namespace Quizware.Application.Reporting;

public sealed record GetMatchReportQuery(Guid ProgramId, Guid MatchId) : IRequest<MatchReportDto>;

public sealed record GetStageSummaryReportQuery(Guid ProgramId, Guid StageId) : IRequest<StageSummaryReportDto>;

public sealed record GetTeamPerformanceReportQuery(Guid ProgramId, Guid TeamId) : IRequest<TeamPerformanceReportDto>;

public sealed record GetQuestionUsageReportQuery(Guid ProgramId, Guid? StageId, bool FlaggedOnly) : IRequest<QuestionUsageReportDto>;

public sealed class ReportQueriesHandler :
    IRequestHandler<GetMatchReportQuery, MatchReportDto>,
    IRequestHandler<GetStageSummaryReportQuery, StageSummaryReportDto>,
    IRequestHandler<GetTeamPerformanceReportQuery, TeamPerformanceReportDto>,
    IRequestHandler<GetQuestionUsageReportQuery, QuestionUsageReportDto>
{
    private readonly IReportingService _reports;

    public ReportQueriesHandler(IReportingService reports)
    {
        _reports = reports;
    }

    public Task<MatchReportDto> Handle(GetMatchReportQuery request, CancellationToken cancellationToken) =>
        _reports.MatchAsync(request.ProgramId, request.MatchId, cancellationToken);

    public Task<StageSummaryReportDto> Handle(GetStageSummaryReportQuery request, CancellationToken cancellationToken) =>
        _reports.StageAsync(request.ProgramId, request.StageId, cancellationToken);

    public Task<TeamPerformanceReportDto> Handle(GetTeamPerformanceReportQuery request, CancellationToken cancellationToken) =>
        _reports.TeamAsync(request.ProgramId, request.TeamId, cancellationToken);

    public Task<QuestionUsageReportDto> Handle(GetQuestionUsageReportQuery request, CancellationToken cancellationToken) =>
        _reports.QuestionUsageAsync(request.ProgramId, request.StageId, request.FlaggedOnly, cancellationToken);
}
