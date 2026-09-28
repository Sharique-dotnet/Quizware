using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using ValidationException = Quizware.Application.Common.Exceptions.ValidationException;
using Quizware.Application.Gameplay.Dtos;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;

namespace Quizware.Application.Gameplay.Commands;

/// <summary>Shared by the setup route (programs/{programId}/matches) and the
/// live route (matches/{matchId}/live), which has no programId — the
/// tenant query filter scopes the match lookup there instead.</summary>
public sealed record ReorderMatchSegmentsCommand(
    Guid? ProgramId, Guid MatchId, IReadOnlyList<Guid> OrderedSegmentIds, string? Reason)
    : IRequest<IReadOnlyList<MatchSegmentDto>>;

public sealed class ReorderMatchSegmentsCommandValidator : AbstractValidator<ReorderMatchSegmentsCommand>
{
    public ReorderMatchSegmentsCommandValidator()
    {
        RuleFor(x => x.OrderedSegmentIds).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

/// <summary>Takes every segment exactly once. A segment that cannot move —
/// locked (D-024), or already opened/completed/skipped during play — keeps
/// its slot wherever it appears in the list; the movable segments fill the
/// remaining slots in the order given. During play this is only allowed when
/// the stage permits it (SEGMENT_NOT_REORDERABLE otherwise).</summary>
public sealed class ReorderMatchSegmentsCommandHandler
    : IRequestHandler<ReorderMatchSegmentsCommand, IReadOnlyList<MatchSegmentDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly MatchEventLog _eventLog;

    public ReorderMatchSegmentsCommandHandler(IAppDbContext db, ICurrentUser currentUser, MatchEventLog eventLog)
    {
        _db = db;
        _currentUser = currentUser;
        _eventLog = eventLog;
    }

    public async Task<IReadOnlyList<MatchSegmentDto>> Handle(
        ReorderMatchSegmentsCommand request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, request.ProgramId, request.MatchId, cancellationToken);

        if (match.State is MatchState.Completed or MatchState.Abandoned)
        {
            throw new InvalidStateTransitionException($"Match {match.MatchNumber} is {match.State}; its segments can no longer be reordered.");
        }

        if (!match.IsInSetup)
        {
            var stage = await _db.Stages.SingleAsync(s => s.Id == match.StageId, cancellationToken);
            if (!stage.AllowSegmentReorderDuringMatch)
            {
                throw new SegmentNotReorderableException($"Stage '{stage.Name}' does not allow segment reordering during a match.");
            }
        }

        var segments = await MatchSetup.LoadSegmentsAsync(_db, match.Id, cancellationToken);
        var requested = request.OrderedSegmentIds;
        if (segments.Count != requested.Count
            || requested.Distinct().Count() != requested.Count
            || requested.Any(id => segments.All(s => s.Id != id)))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["orderedSegmentIds"] = ["Must list every segment of this match exactly once."],
            });
        }

        var byId = segments.ToDictionary(s => s.Id);
        static bool IsMovable(Domain.Gameplay.MatchSegment s) => !s.IsOrderLocked && s.State == MatchSegmentState.Pending;

        var movableSlots = segments.Where(IsMovable).Select(s => s.OrderIndex).Order().ToList();
        var movableInRequestedOrder = requested.Select(id => byId[id]).Where(IsMovable).ToList();
        var finalOrder = movableInRequestedOrder.Select((s, i) => (s, movableSlots[i])).ToList();

        await MatchSetup.ApplySegmentOrderAsync(_db, finalOrder, cancellationToken);

        if (!match.IsInSetup)
        {
            await _eventLog.AppendAsync(match, MatchEventTypes.SegmentsReordered, new
            {
                orderedSegmentIds = segments.OrderBy(s => s.OrderIndex).Select(s => s.Id).ToList(),
                reason = request.Reason,
            }, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return segments.OrderBy(s => s.OrderIndex).Select(MatchSetup.ToDto).ToList();
    }
}
