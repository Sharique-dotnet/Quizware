using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Rules.Dtos;
using Quizware.Domain.Qualification;

namespace Quizware.Application.Rules.Commands;

/// <summary>Resets the program-wide tie-break rules (StageId == null) to the
/// three recommended rows from <see cref="DefaultTieBreakValues"/> — one per
/// <c>TieBreakScope</c>. Stage-specific overrides (StageId set) are left
/// untouched.</summary>
public sealed record ResetTieBreakDefaultsCommand(Guid ProgramId) : IRequest<IReadOnlyList<TieBreakRuleAppDto>>;

public sealed class ResetTieBreakDefaultsCommandHandler : IRequestHandler<ResetTieBreakDefaultsCommand, IReadOnlyList<TieBreakRuleAppDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ResetTieBreakDefaultsCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<TieBreakRuleAppDto>> Handle(ResetTieBreakDefaultsCommand request, CancellationToken cancellationToken)
    {
        var actor = _currentUser.Email ?? "unknown";
        var existing = await _db.TieBreakRules
            .Where(r => r.ProgramId == request.ProgramId && r.StageId == null)
            .ToListAsync(cancellationToken);
        foreach (var rule in existing)
        {
            rule.Delete(actor);
        }

        foreach (var value in DefaultTieBreakValues.All)
        {
            var created = TieBreakRule.Create(request.ProgramId, value.Name, value.Scope, value.Criteria, actor);
            created.Update(
                value.Criteria, value.TieBreakFormatCode, value.QuestionCount, value.SuddenDeath,
                value.MaxExtraRounds, value.ScoreCountsTowardStage, value.OnStillTied, actor);
            _db.TieBreakRules.Add(created);
        }

        await _db.SaveChangesAsync(cancellationToken);

        var all = await _db.TieBreakRules
            .Where(r => r.ProgramId == request.ProgramId && r.StageId == null)
            .ToListAsync(cancellationToken);
        return all.Select(r => r.ToDto()).ToList();
    }
}
