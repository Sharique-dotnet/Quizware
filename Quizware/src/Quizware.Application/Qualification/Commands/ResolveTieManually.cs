using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Qualification.Dtos;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;
using ValidationException = Quizware.Application.Common.Exceptions.ValidationException;

namespace Quizware.Application.Qualification.Commands;

public sealed record ResolveTieManuallyCommand(
    Guid ProgramId, Guid TieId, IReadOnlyList<Guid> RankedTeamIds, string Method, string Reason, Guid ApprovedByUserId) : IRequest<TieEntryDto>;

public sealed class ResolveTieManuallyCommandValidator : AbstractValidator<ResolveTieManuallyCommand>
{
    public static readonly string[] Methods = ["ManualDecision", "CoinToss", "ShareTheSlot"];

    public ResolveTieManuallyCommandValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("A manual decision must give its reason.");
        RuleFor(x => x.ApprovedByUserId).NotEmpty();
        RuleFor(x => x.RankedTeamIds).NotEmpty();
        RuleFor(x => x.RankedTeamIds).Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("A team can only be ranked once.");
        RuleFor(x => x.Method).Must(m => Methods.Contains(m, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Method must be one of: {string.Join(", ", Methods)}.");
    }
}

/// <summary>P11-06: an audited manual settlement — ProgramAdmin/SuperAdmin
/// only (CanResolveTie), always with a reason. <c>RankedTeamIds</c> orders the
/// still-contested teams best first and the open slots go to the top of it.
/// CoinToss records a physical toss with the same audit trail; ShareTheSlot
/// qualifies every contested team and is only allowed when the rule's
/// OnStillTied says so. An unstarted tie-break match for the tie is withdrawn.</summary>
public sealed class ResolveTieManuallyCommandHandler : IRequestHandler<ResolveTieManuallyCommand, TieEntryDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly TieBreakMatches _tieBreaks;
    private readonly TieViews _views;

    public ResolveTieManuallyCommandHandler(IAppDbContext db, ICurrentUser currentUser, TieBreakMatches tieBreaks, TieViews views)
    {
        _db = db;
        _currentUser = currentUser;
        _tieBreaks = tieBreaks;
        _views = views;
    }

    public async Task<TieEntryDto> Handle(ResolveTieManuallyCommand request, CancellationToken cancellationToken)
    {
        var actor = _currentUser.Email ?? "unknown";
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("An authenticated user is required to resolve a tie.");
        var tie = await TieViews.LoadAsync(_db, request.ProgramId, request.TieId, cancellationToken);
        var teams = await _db.TieBreakParticipants.Where(p => p.TieBreakEventId == tie.Id).ToListAsync(cancellationToken);
        var rule = await _db.TieBreakRules.IgnoreQueryFilters().SingleAsync(r => r.Id == tie.TieBreakRuleId, cancellationToken);

        if (!TieViews.AffectsQualification(tie, teams.Count) || !tie.IsOpen)
        {
            throw new TieBreakNotApplicableException(tie.IsOpen
                ? "This tie does not affect a qualifying place."
                : $"This tie is already {tie.State}.");
        }

        var contested = teams.Where(t => t.IsContested).ToList();
        if (!contested.Select(t => t.TeamId).ToHashSet().SetEquals(request.RankedTeamIds))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["rankedTeamIds"] = ["Rank exactly the teams still contested in this tie: " + string.Join(", ", contested.Select(t => t.TeamId)) + "."],
            });
        }

        var method = request.Method.ToLowerInvariant() switch
        {
            "cointoss" => TieBreakResolutionMethod.CoinToss,
            "sharetheslot" => TieBreakResolutionMethod.Shared,
            _ => TieBreakResolutionMethod.Manual,
        };
        if (method == TieBreakResolutionMethod.Shared && rule.OnStillTied != OnStillTiedPolicy.ShareTheSlot)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["method"] = [$"The tie-break rule's OnStillTied is {rule.OnStillTied}; it does not allow sharing the slot."],
            });
        }

        await _tieBreaks.WithdrawMatchAsync(tie, actor, cancellationToken);

        var alreadyIn = teams.Count(t => !t.IsContested && t.Qualified);
        var slots = tie.ContestedSlots - alreadyIn;
        for (var i = 0; i < request.RankedTeamIds.Count; i++)
        {
            var team = contested.Single(t => t.TeamId == request.RankedTeamIds[i]);
            team.RecordResult(team.TieBreakPoints, alreadyIn + i + 1, method == TieBreakResolutionMethod.Shared || i < slots);
        }

        tie.Resolve(method, userId, notes: request.Reason, approvedByUserId: request.ApprovedByUserId);
        await _db.SaveChangesAsync(cancellationToken);
        return await _views.BuildAsync(tie, cancellationToken);
    }
}
