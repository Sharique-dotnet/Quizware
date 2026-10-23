using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay;
using Quizware.Application.Gameplay.Commands;
using Quizware.Application.Qualification.Dtos;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;
using Quizware.Domain.Gameplay;
using Quizware.Domain.Qualification;
using Quizware.Domain.Tournament;

namespace Quizware.Application.Qualification.Commands;

public sealed record CreateTieBreakMatchCommand(
    Guid ProgramId, Guid TieId, string? FormatCodeOverride, int? QuestionCountOverride, bool? SuddenDeathOverride, string? Reason)
    : IRequest<TieBreakMatchCreatedDto>;

public sealed class CreateTieBreakMatchCommandValidator : AbstractValidator<CreateTieBreakMatchCommand>
{
    public CreateTieBreakMatchCommandValidator()
    {
        RuleFor(x => x.FormatCodeOverride)
            .Must(f => f is null || Enum.TryParse<QuestionFormatCode>(f, ignoreCase: true, out _))
            .WithMessage($"FormatCodeOverride must be one of: {string.Join(", ", Enum.GetNames<QuestionFormatCode>())}.");
        RuleFor(x => x.QuestionCountOverride).InclusiveBetween(1, 50).When(x => x.QuestionCountOverride is not null);
    }
}

/// <summary>P11-04: the tie-break is an ordinary match (ADR-007) in the same
/// stage, with <c>MatchKind = TieBreak</c>, only the still-contested teams,
/// and one segment built from the tie-break rule — MCQ by default, any field
/// overridable for this one tie. From here the existing engine plays it on the
/// normal console; its result comes back through <see cref="TieBreakMatches"/>
/// on completion. Questions are reserved when it starts, like every match
/// (P9-02); the ready check runs straight away.</summary>
public sealed class CreateTieBreakMatchCommandHandler : IRequestHandler<CreateTieBreakMatchCommand, TieBreakMatchCreatedDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly MatchEventLog _eventLog;
    private readonly ISender _sender;

    public CreateTieBreakMatchCommandHandler(IAppDbContext db, ICurrentUser currentUser, MatchEventLog eventLog, ISender sender)
    {
        _db = db;
        _currentUser = currentUser;
        _eventLog = eventLog;
        _sender = sender;
    }

    public async Task<TieBreakMatchCreatedDto> Handle(CreateTieBreakMatchCommand request, CancellationToken cancellationToken)
    {
        var actor = _currentUser.Email ?? "unknown";
        var tie = await TieViews.LoadAsync(_db, request.ProgramId, request.TieId, cancellationToken);
        var teams = await _db.TieBreakParticipants.Where(p => p.TieBreakEventId == tie.Id).ToListAsync(cancellationToken);
        var rule = await _db.TieBreakRules.IgnoreQueryFilters().SingleAsync(r => r.Id == tie.TieBreakRuleId, cancellationToken);
        EnsurePlayable(tie, teams, rule);

        var stage = await _db.Stages.SingleAsync(s => s.Id == tie.StageId, cancellationToken);
        var contested = teams.Where(t => t.IsContested).ToList();
        var names = await _db.Teams
            .Where(t => contested.Select(c => c.TeamId).Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.DisplayName, cancellationToken);
        var format = request.FormatCodeOverride is { } f ? Enum.Parse<QuestionFormatCode>(f, ignoreCase: true) : rule.TieBreakFormatCode;
        var questionCount = request.QuestionCountOverride ?? rule.QuestionCount;
        var suddenDeath = request.SuddenDeathOverride ?? rule.SuddenDeath;

        var matchNumber = (await _db.Matches.Where(m => m.StageId == stage.Id).Select(m => (int?)m.MatchNumber).MaxAsync(cancellationToken) ?? 0) + 1;
        var round = tie.RoundsPlayed > 0 ? $", round {tie.RoundsPlayed + 1}" : string.Empty;
        var match = Match.Create(
            request.ProgramId, stage.Id, matchNumber, Random.Shared.NextInt64(1, long.MaxValue), actor,
            MatchKind.TieBreak, tie.Id, $"{stage.Name} — Tie-break for {Ordinal(tie.ContestedRank)} place{round}");
        _db.Matches.Add(match);

        var seated = contested.OrderBy(t => names.GetValueOrDefault(t.TeamId)).ToList();
        for (var i = 0; i < seated.Count; i++)
        {
            _db.MatchParticipants.Add(MatchParticipant.Create(request.ProgramId, match.Id, seated[i].TeamId, i + 1, i + 1, actor));
        }

        // The stage's own template for the format, if it has one, lends its
        // play settings (time limit, passing); otherwise defaults apply.
        var template = await _db.StageSegmentTemplates
            .Where(t => t.StageId == stage.Id && t.FormatCode == format)
            .OrderBy(t => t.OrderIndex)
            .FirstOrDefaultAsync(cancellationToken);
        var segment = MatchSegment.Create(request.ProgramId, match.Id, template?.Id ?? Guid.Empty, format, 0, questionCount, actor);
        if (suddenDeath)
        {
            segment.MakeSuddenDeath();
        }

        _db.MatchSegments.Add(segment);
        tie.StartPlaying(match.Id);
        await _eventLog.AppendAsync(match, "TieBreakMatchCreated", new
        {
            tieBreakEventId = tie.Id,
            contestedRank = tie.ContestedRank,
            teamIds = seated.Select(t => t.TeamId),
            format = format.ToString(),
            questionCount,
            suddenDeath,
            overridden = request.FormatCodeOverride is not null || request.QuestionCountOverride is not null || request.SuddenDeathOverride is not null,
            reason = request.Reason,
        }, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var ready = await _sender.Send(new MarkMatchReadyCommand(request.ProgramId, match.Id), cancellationToken);
        var nextStep = ready.IsReady
            ? "Run this match on the operator console, then re-run the qualification preview."
            : $"Fix before starting: {string.Join(" ", ready.Blockers)}";

        return new TieBreakMatchCreatedDto(
            tie.Id,
            match.Id,
            match.MatchKind.ToString(),
            stage.Id,
            match.MatchNumber,
            match.Name!,
            match.State.ToString(),
            seated.Select((t, i) => new TieBreakParticipantEntryDto(t.TeamId, names.GetValueOrDefault(t.TeamId, string.Empty), i + 1, i + 1)).ToList(),
            [new TieBreakSegmentEntryDto(segment.Id, format.ToString(), segment.OrderIndex, segment.PlannedQuestionCount, segment.IsSuddenDeath)],
            0,
            rule.ScoreCountsTowardStage,
            nextStep);
    }

    private static void EnsurePlayable(TieBreakEvent tie, IReadOnlyList<TieBreakParticipant> teams, TieBreakRule rule)
    {
        if (!TieViews.AffectsQualification(tie, teams.Count))
        {
            throw new TieBreakNotApplicableException("This tie does not affect a qualifying place, so there is nothing to play for.");
        }

        if (tie.State is not (TieBreakEventState.Detected or TieBreakEventState.AwaitingPlay))
        {
            throw new TieBreakNotApplicableException(tie.State == TieBreakEventState.InProgress
                ? $"A tie-break match ({tie.TieBreakMatchId}) is already set up for this tie."
                : $"This tie is already {tie.State}.");
        }

        if (!rule.PlayTieBreakSegment)
        {
            throw new TieBreakNotApplicableException("The tie-break rule does not play a tie-break segment; resolve this tie manually.");
        }

        if (!TieViews.MayPlayAnotherRound(tie, rule))
        {
            throw new TieBreakNotApplicableException(
                $"{tie.RoundsPlayed} tie-break round(s) ended level and the rule allows {rule.MaxExtraRounds} extra; apply {rule.OnStillTied} via resolve-manually.");
        }

        if (teams.Count(t => t.IsContested) < 2)
        {
            throw new TieBreakNotApplicableException("Fewer than two teams are still contested.");
        }
    }

    private static string Ordinal(int n) => (n % 100) switch
    {
        11 or 12 or 13 => $"{n}th",
        _ => (n % 10) switch { 1 => $"{n}st", 2 => $"{n}nd", 3 => $"{n}rd", _ => $"{n}th" },
    };
}
