using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.Rules.Dtos;
using QuizApp.Domain.Qualification;

namespace QuizApp.Application.Rules.Commands;

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

        var actor = _currentUser.Email ?? "unknown";
        foreach (var dto in request.Rules)
        {
            if (dto.Id != Guid.Empty && existingById.TryGetValue(dto.Id, out var rule))
            {
                rule.Update(dto.WinnersPerMatch, dto.BestRemainingAcrossStage, dto.ManualWildcardSlots, actor);
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
