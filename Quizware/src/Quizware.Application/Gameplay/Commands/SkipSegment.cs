using FluentValidation;
using MediatR;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;

namespace Quizware.Application.Gameplay.Commands;

public sealed record SkipSegmentCommand(Guid MatchId, Guid SegmentId, string Reason) : IRequest<LiveMatchStateDto>;

public sealed class SkipSegmentCommandValidator : AbstractValidator<SkipSegmentCommand>
{
    public SkipSegmentCommandValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

/// <summary>Skips a pending segment, or abandons the rest of the open one.
/// Its unserved questions return to the pool.</summary>
public sealed class SkipSegmentCommandHandler : IRequestHandler<SkipSegmentCommand, LiveMatchStateDto>
{
    private readonly IAppDbContext _db;
    private readonly MatchEventLog _eventLog;
    private readonly LiveStateBuilder _state;

    public SkipSegmentCommandHandler(IAppDbContext db, MatchEventLog eventLog, LiveStateBuilder state)
    {
        _db = db;
        _eventLog = eventLog;
        _state = state;
    }

    public async Task<LiveMatchStateDto> Handle(SkipSegmentCommand request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, null, request.MatchId, cancellationToken);
        LiveRules.RequireInProgress(match);
        var segment = await LiveRules.LoadSegmentAsync(_db, match.Id, request.SegmentId, cancellationToken);

        if (segment.State is not (MatchSegmentState.Pending or MatchSegmentState.Open))
        {
            throw new InvalidStateTransitionException($"Segment {segment.OrderIndex} is {segment.State} and cannot be skipped.");
        }

        var active = await LiveRules.ActiveQuestionAsync(_db, match.Id, cancellationToken);
        if (active is not null && active.MatchSegmentId == segment.Id)
        {
            throw new InvalidStateTransitionException("A question is still on screen — answer or skip it before skipping the segment.");
        }

        segment.Skip(request.Reason);
        foreach (var question in await LiveRules.ReservedInSegmentAsync(_db, segment.Id, cancellationToken))
        {
            question.Release();
        }

        await _eventLog.AppendAsync(match, MatchEventTypes.SegmentSkipped, new { segmentId = segment.Id, reason = request.Reason }, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return await _state.BuildAsync(match, cancellationToken);
    }
}
