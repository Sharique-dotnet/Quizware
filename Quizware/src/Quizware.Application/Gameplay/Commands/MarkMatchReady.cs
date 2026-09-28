using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;
using Quizware.Application.Rules.Services;
using Quizware.Application.Selection;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;

namespace Quizware.Application.Gameplay.Commands;

public sealed record MarkMatchReadyCommand(Guid ProgramId, Guid MatchId) : IRequest<MatchReadyDto>;

/// <summary>Checks everything start would otherwise fail on — team count,
/// segments, question supply per format, a scoring rule per format — and
/// reports every blocker at once. Only a match with no blockers becomes
/// Ready; one that no longer passes drops back to Draft.</summary>
public sealed class MarkMatchReadyCommandHandler : IRequestHandler<MarkMatchReadyCommand, MatchReadyDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IQuestionSelector _selector;
    private readonly IRuleService _ruleService;

    public MarkMatchReadyCommandHandler(
        IAppDbContext db, ICurrentUser currentUser, IQuestionSelector selector, IRuleService ruleService)
    {
        _db = db;
        _currentUser = currentUser;
        _selector = selector;
        _ruleService = ruleService;
    }

    public async Task<MatchReadyDto> Handle(MarkMatchReadyCommand request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, request.ProgramId, request.MatchId, cancellationToken);
        var actor = _currentUser.Email ?? "unknown";
        if (!match.IsInSetup)
        {
            throw new InvalidStateTransitionException($"Match {match.MatchNumber} is {match.State}; readiness only applies before it starts.");
        }

        var stage = await _db.Stages.SingleAsync(s => s.Id == match.StageId, cancellationToken);
        var participants = await MatchSetup.LoadParticipantsAsync(_db, match.Id, cancellationToken);
        var segments = await MatchSetup.LoadSegmentsAsync(_db, match.Id, cancellationToken);
        var blockers = new List<string>();

        var activeCount = participants.Count(p => p.Status == ParticipantStatus.Active);
        var minimum = Math.Max(2, stage.MinTeamsPerMatch);
        if (activeCount < minimum)
        {
            blockers.Add($"INSUFFICIENT_PARTICIPANTS: needs at least {minimum} teams; has {activeCount}.");
        }

        if (activeCount > stage.MaxTeamsPerMatch)
        {
            blockers.Add($"TOO_MANY_PARTICIPANTS: stage allows at most {stage.MaxTeamsPerMatch} teams; has {activeCount}.");
        }

        if (segments.Count == 0)
        {
            blockers.Add("MATCH_HAS_NO_SEGMENTS: add at least one segment.");
        }

        // Segments of the same format draw from the same pool, so supply is
        // checked against their combined count, not segment by segment.
        foreach (var group in segments.GroupBy(s => s.FormatCode))
        {
            var first = group.First();
            var needed = group.Sum(s => s.PlannedQuestionCount);
            var preview = await _selector.PreviewAsync(
                new SelectionRequest(match.ProgramId, match.StageId, first.SegmentTemplateId, group.Key, needed, match.RandomSeed, match.Id),
                cancellationToken);
            if (!preview.CanSatisfy)
            {
                blockers.Add($"QUESTION_POOL_EXHAUSTED: {group.Key} needs {needed} questions; {preview.Questions.Count} available.");
            }

            try
            {
                await _ruleService.ResolveScoringRuleAsync(
                    match.ProgramId, group.Key, AnswerOutcome.Correct, match.StageId, first.SegmentTemplateId, null, cancellationToken);
            }
            catch (ScoringRuleNotFoundException)
            {
                blockers.Add($"SCORING_RULE_MISSING: no scoring rule for a correct {group.Key} answer.");
            }
        }

        if (blockers.Count == 0)
        {
            match.MarkReady(actor);
        }
        else if (match.State == MatchState.Ready)
        {
            match.TouchSetup(actor);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new MatchReadyDto(blockers.Count == 0, blockers);
    }
}
