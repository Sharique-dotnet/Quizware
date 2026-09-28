using MediatR;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;
using Quizware.Domain.Common.Exceptions;

namespace Quizware.Application.Gameplay.Commands;

public sealed record CloseSegmentCommand(Guid MatchId, Guid SegmentId) : IRequest<LiveMatchStateDto>;

/// <summary>Completes the open segment. A question on screen must be answered
/// or skipped first; any question never served goes back to the pool.</summary>
public sealed class CloseSegmentCommandHandler : IRequestHandler<CloseSegmentCommand, LiveMatchStateDto>
{
    private readonly IAppDbContext _db;
    private readonly MatchEventLog _eventLog;
    private readonly LiveStateBuilder _state;

    public CloseSegmentCommandHandler(IAppDbContext db, MatchEventLog eventLog, LiveStateBuilder state)
    {
        _db = db;
        _eventLog = eventLog;
        _state = state;
    }

    public async Task<LiveMatchStateDto> Handle(CloseSegmentCommand request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, null, request.MatchId, cancellationToken);
        LiveRules.RequireInProgress(match);
        var segment = await LiveRules.LoadSegmentAsync(_db, match.Id, request.SegmentId, cancellationToken);

        var active = await LiveRules.ActiveQuestionAsync(_db, match.Id, cancellationToken);
        if (active is not null && active.MatchSegmentId == segment.Id)
        {
            throw new InvalidStateTransitionException("A question is still on screen — answer or skip it before closing the segment.");
        }

        segment.Complete();
        var unserved = await LiveRules.ReservedInSegmentAsync(_db, segment.Id, cancellationToken);
        foreach (var question in unserved)
        {
            question.Release();
        }

        await _eventLog.AppendAsync(match, MatchEventTypes.SegmentCompleted, new
        {
            segmentId = segment.Id,
            served = segment.ServedQuestionCount,
            released = unserved.Count,
        }, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return await _state.BuildAsync(match, cancellationToken);
    }
}
