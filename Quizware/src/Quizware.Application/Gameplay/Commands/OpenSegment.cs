using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;
using Quizware.Domain.Common.Exceptions;

namespace Quizware.Application.Gameplay.Commands;

public sealed record OpenSegmentCommand(Guid MatchId, Guid SegmentId) : IRequest<LiveMatchStateDto>;

public sealed class OpenSegmentCommandHandler : IRequestHandler<OpenSegmentCommand, LiveMatchStateDto>
{
    private readonly IAppDbContext _db;
    private readonly MatchEventLog _eventLog;
    private readonly LiveStateBuilder _state;

    public OpenSegmentCommandHandler(IAppDbContext db, MatchEventLog eventLog, LiveStateBuilder state)
    {
        _db = db;
        _eventLog = eventLog;
        _state = state;
    }

    public async Task<LiveMatchStateDto> Handle(OpenSegmentCommand request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, null, request.MatchId, cancellationToken);
        LiveRules.RequireInProgress(match);

        var stage = await _db.Stages.SingleAsync(s => s.Id == match.StageId, cancellationToken);
        var segments = await MatchSetup.LoadSegmentsAsync(_db, match.Id, cancellationToken);
        var segment = segments.SingleOrDefault(s => s.Id == request.SegmentId)
            ?? throw new KeyNotFoundException($"Segment '{request.SegmentId}' was not found in this match.");

        if (LiveRules.OpenableSegments(stage, segments).All(s => s.Id != segment.Id))
        {
            throw new InvalidStateTransitionException(
                $"Segment {segment.OrderIndex} ({segment.FormatCode}) cannot be opened now — close the open segment first, " +
                "or open the next segment in order.");
        }

        segment.Open(segments);
        match.SetCurrentSegment(segment.Id);
        await _eventLog.AppendAsync(match, MatchEventTypes.SegmentOpened, new { segmentId = segment.Id, format = segment.FormatCode.ToString() }, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return await _state.BuildAsync(match, cancellationToken);
    }
}
