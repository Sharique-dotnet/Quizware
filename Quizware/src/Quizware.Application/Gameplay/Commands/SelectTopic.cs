using FluentValidation;
using MediatR;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;
using Quizware.Application.Gameplay.Formats;
using Quizware.Domain.Common.Exceptions;
using ValidationException = Quizware.Application.Common.Exceptions.ValidationException;

namespace Quizware.Application.Gameplay.Commands;

public sealed record SelectTopicCommand(Guid MatchId, Guid ParticipantId, string TopicName) : IRequest<LiveMatchStateDto>;

public sealed class SelectTopicCommandValidator : AbstractValidator<SelectTopicCommand>
{
    public SelectTopicCommandValidator()
    {
        RuleFor(x => x.ParticipantId).NotEmpty();
        RuleFor(x => x.TopicName).NotEmpty().MaximumLength(150);
    }
}

/// <summary>Only the team whose turn it is may pick. The first reserved
/// question under that label is moved to the front of the segment's queue; if
/// the topic is exclusive (P9-09), the rest under that label leave the board.</summary>
public sealed class SelectTopicCommandHandler : IRequestHandler<SelectTopicCommand, LiveMatchStateDto>
{
    private readonly IAppDbContext _db;
    private readonly MatchEventLog _eventLog;
    private readonly LiveStateBuilder _state;
    private readonly QuestionFormatHandlers _formats;

    public SelectTopicCommandHandler(IAppDbContext db, MatchEventLog eventLog, LiveStateBuilder state, QuestionFormatHandlers formats)
    {
        _db = db;
        _eventLog = eventLog;
        _state = state;
        _formats = formats;
    }

    public async Task<LiveMatchStateDto> Handle(SelectTopicCommand request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, null, request.MatchId, cancellationToken);
        LiveRules.RequireInProgress(match);

        var open = await TopicPicks.OpenSegmentAsync(_db, match.Id, cancellationToken)
            ?? throw new InvalidStateTransitionException("No segment is open.");
        TopicPicks.RequireTopicPicks(open.Template);

        if (await LiveRules.ActiveQuestionAsync(_db, match.Id, cancellationToken) is not null)
        {
            throw new InvalidStateTransitionException("A question is already on screen — pick the topic before serving.");
        }

        var participants = await MatchSetup.LoadParticipantsAsync(_db, match.Id, cancellationToken);
        if (TurnRotation.NextParticipantOrNull(participants, open.Segment, _formats) != request.ParticipantId)
        {
            throw new InvalidStateTransitionException("Only the team whose turn it is may pick the topic.");
        }

        var candidates = await TopicPicks.CandidatesAsync(_db, _formats, open.Segment.Id, cancellationToken);
        var chosen = candidates.FirstOrDefault(c => string.Equals(c.TopicName, request.TopicName, StringComparison.OrdinalIgnoreCase))
            ?? throw new ValidationException(new Dictionary<string, string[]>
            {
                ["topicName"] = [$"'{request.TopicName}' is not one of the topics left in this segment."],
            });

        if (chosen.TopicId is { } topicId)
        {
            chosen.MatchQuestion.SelectTopic(topicId);
        }

        // Two-phase (D-023): the unique (MatchSegmentId, OrderIndex) index.
        var reserved = await LiveRules.ReservedInSegmentAsync(_db, open.Segment.Id, cancellationToken);
        var slots = reserved.Select(q => q.OrderIndex).ToList();
        var newOrder = reserved.Where(q => q.Id != chosen.MatchQuestion.Id).Prepend(chosen.MatchQuestion).ToList();
        if (newOrder[0].Id != reserved[0].Id)
        {
            for (var i = 0; i < newOrder.Count; i++)
            {
                newOrder[i].Renumber(100_000 + i);
            }

            await _db.SaveChangesAsync(cancellationToken);
            for (var i = 0; i < newOrder.Count; i++)
            {
                newOrder[i].Renumber(slots[i]);
            }
        }

        // An exclusive topic leaves the board the moment it is picked: every
        // other reserved question under the same label goes back to the pool.
        var removedFromBoard = chosen.IsExclusive
            ? candidates
                .Where(c => c.MatchQuestion.Id != chosen.MatchQuestion.Id
                    && string.Equals(c.TopicName, chosen.TopicName, StringComparison.OrdinalIgnoreCase))
                .Select(c => c.MatchQuestion)
                .ToList()
            : [];
        foreach (var removed in removedFromBoard)
        {
            removed.Release();
        }

        await _eventLog.AppendAsync(match, MatchEventTypes.TopicSelected, new
        {
            participantId = request.ParticipantId,
            topic = chosen.TopicName,
            matchQuestionId = chosen.MatchQuestion.Id,
            removedFromBoard = removedFromBoard.Select(q => q.Id),
        }, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return await _state.BuildAsync(match, cancellationToken);
    }
}
