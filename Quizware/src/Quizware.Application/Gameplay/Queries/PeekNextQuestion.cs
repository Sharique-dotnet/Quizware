using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;

namespace Quizware.Application.Gameplay.Queries;

public sealed record PeekNextQuestionQuery(Guid MatchId) : IRequest<CurrentQuestionDto>;

/// <summary>The operator's preview of the question that serve would put on
/// screen next, without serving it.</summary>
public sealed class PeekNextQuestionQueryHandler : IRequestHandler<PeekNextQuestionQuery, CurrentQuestionDto>
{
    private readonly IAppDbContext _db;
    private readonly LiveStateBuilder _state;

    public PeekNextQuestionQueryHandler(IAppDbContext db, LiveStateBuilder state)
    {
        _db = db;
        _state = state;
    }

    public async Task<CurrentQuestionDto> Handle(PeekNextQuestionQuery request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, null, request.MatchId, cancellationToken);
        var openSegment = await _db.MatchSegments
            .SingleOrDefaultAsync(s => s.MatchId == match.Id && s.State == MatchSegmentState.Open, cancellationToken)
            ?? throw new InvalidStateTransitionException("No segment is open.");

        var next = (await LiveRules.ReservedInSegmentAsync(_db, openSegment.Id, cancellationToken)).FirstOrDefault()
            ?? throw new InvalidStateTransitionException($"Segment {openSegment.OrderIndex} has no questions left to serve.");

        return await _state.PresentAsync(next, cancellationToken);
    }
}
