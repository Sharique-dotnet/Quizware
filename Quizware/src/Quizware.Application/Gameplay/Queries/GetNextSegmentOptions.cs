using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;

namespace Quizware.Application.Gameplay.Queries;

public sealed record GetNextSegmentOptionsQuery(Guid MatchId) : IRequest<IReadOnlyList<MatchSegmentDto>>;

/// <summary>The segments the operator may open next (see LiveRules.OpenableSegments).</summary>
public sealed class GetNextSegmentOptionsQueryHandler : IRequestHandler<GetNextSegmentOptionsQuery, IReadOnlyList<MatchSegmentDto>>
{
    private readonly IAppDbContext _db;

    public GetNextSegmentOptionsQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<MatchSegmentDto>> Handle(GetNextSegmentOptionsQuery request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, null, request.MatchId, cancellationToken);
        var stage = await _db.Stages.SingleAsync(s => s.Id == match.StageId, cancellationToken);
        var segments = await MatchSetup.LoadSegmentsAsync(_db, match.Id, cancellationToken);
        return LiveRules.OpenableSegments(stage, segments).Select(MatchSetup.ToDto).ToList();
    }
}
