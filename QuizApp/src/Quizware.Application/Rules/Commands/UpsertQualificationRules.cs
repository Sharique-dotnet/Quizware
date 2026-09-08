using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Rules.Dtos;
using Quizware.Domain.Qualification;

namespace Quizware.Application.Rules.Commands;

public sealed record UpsertQualificationRulesCommand(Guid ProgramId, IReadOnlyList<QualificationRuleAppDto> Rules) : IRequest<IReadOnlyList<QualificationRuleAppDto>>;

public sealed class UpsertQualificationRulesCommandValidator : AbstractValidator<UpsertQualificationRulesCommand>
{
    public UpsertQualificationRulesCommandValidator()
    {
        RuleForEach(x => x.Rules).ChildRules(rule =>
        {
            rule.RuleFor(r => r.StageId).NotEmpty();
            rule.RuleFor(r => r.WinnersPerMatch).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class UpsertQualificationRulesCommandHandler : IRequestHandler<UpsertQualificationRulesCommand, IReadOnlyList<QualificationRuleAppDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public UpsertQualificationRulesCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<QualificationRuleAppDto>> Handle(UpsertQualificationRulesCommand request, CancellationToken cancellationToken)
    {
        var existing = await _db.QualificationRules
            .Where(r => r.ProgramId == request.ProgramId)
            .ToListAsync(cancellationToken);
        var existingById = existing.ToDictionary(r => r.Id);

        // UX_QualificationRule is a unique (ProgramId, FromStageId) index —
        // a "create" for a stage that already has a rule must update it
        // instead of inserting a duplicate, or the unique index throws an
        // unhandled 500 (see L-007, same class of bug hit and fixed for
        // UpsertScoringRulesCommandHandler).
        var actor = _currentUser.Email ?? "unknown";
        foreach (var dto in request.Rules)
        {
            if (dto.Id != Guid.Empty && existingById.TryGetValue(dto.Id, out var ruleById))
            {
                ruleById.Update(dto.WinnersPerMatch, dto.BestRemainingAcrossStage, dto.ManualWildcardSlots, actor);
                continue;
            }

            var ruleByStage = existing.SingleOrDefault(r => r.FromStageId == dto.StageId);
            if (ruleByStage is not null)
            {
                ruleByStage.Update(dto.WinnersPerMatch, dto.BestRemainingAcrossStage, dto.ManualWildcardSlots, actor);
                continue;
            }

            _db.QualificationRules.Add(QualificationRule.Create(
                request.ProgramId, dto.StageId, actor,
                winnersPerMatch: dto.WinnersPerMatch,
                bestRemainingAcrossStage: dto.BestRemainingAcrossStage,
                manualWildcardSlots: dto.ManualWildcardSlots));
        }

        await _db.SaveChangesAsync(cancellationToken);

        var all = await _db.QualificationRules
            .Where(r => r.ProgramId == request.ProgramId)
            .ToListAsync(cancellationToken);
        return all.Select(r => r.ToDto()).ToList();
    }
}
