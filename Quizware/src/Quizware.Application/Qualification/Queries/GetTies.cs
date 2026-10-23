using MediatR;
using Quizware.Application.Qualification.Dtos;

namespace Quizware.Application.Qualification.Queries;

public sealed record GetStageTiesQuery(Guid ProgramId, Guid StageId) : IRequest<StageTiesDto>;

public sealed record GetTieQuery(Guid ProgramId, Guid TieId) : IRequest<TieEntryDto>;

/// <summary>Every saved tie of a stage (abandoned ones left out), resolved and unresolved.</summary>
public sealed class GetStageTiesQueryHandler : IRequestHandler<GetStageTiesQuery, StageTiesDto>
{
    private readonly TieViews _views;

    public GetStageTiesQueryHandler(TieViews views)
    {
        _views = views;
    }

    public Task<StageTiesDto> Handle(GetStageTiesQuery request, CancellationToken cancellationToken) =>
        _views.ForStageAsync(request.ProgramId, request.StageId, cancellationToken);
}

public sealed class GetTieQueryHandler : IRequestHandler<GetTieQuery, TieEntryDto>
{
    private readonly TieViews _views;

    public GetTieQueryHandler(TieViews views)
    {
        _views = views;
    }

    public Task<TieEntryDto> Handle(GetTieQuery request, CancellationToken cancellationToken) =>
        _views.ForTieAsync(request.ProgramId, request.TieId, cancellationToken);
}
