using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.Rules.Dtos;
using QuizApp.Domain.Enums;
using QuizApp.Domain.Qualification;

namespace QuizApp.Application.Rules.Commands;

/// <summary>The contract DTO has no Name/Scope field, so a newly created
/// rule is named from its stage and defaulted to <see cref="TieBreakScope.MatchRanking"/>
/// — the scope most tie-break rules are created for (deciding who wins a
/// tied match) — this is an implementation default, not sourced from the
/// design docs, same category of gap as D-021's media limits.</summary>
public sealed record UpsertTieBreakRulesCommand(Guid ProgramId, IReadOnlyList<TieBreakRuleAppDto> Rules) : IRequest<IReadOnlyList<TieBreakRuleAppDto>>;

public sealed class UpsertTieBreakRulesCommandValidator : AbstractValidator<UpsertTieBreakRulesCommand>
{
    public UpsertTieBreakRulesCommandValidator()
    {
        RuleForEach(x => x.Rules).ChildRules(rule =>
        {
            rule.RuleFor(r => r.StageId).NotEmpty();
            rule.RuleFor(r => r.Criteria).NotEmpty();
            rule.RuleFor(r => r.QuestionCount).GreaterThanOrEqualTo(1);
            rule.RuleFor(r => r.TieBreakFormat).Must(f => Enum.TryParse<QuestionFormatCode>(f, ignoreCase: true, out _))
                .WithMessage("TieBreakFormat is not a recognized question format.");
            rule.RuleFor(r => r.OnStillTied).Must(v => Enum.TryParse<OnStillTiedPolicy>(v, ignoreCase: true, out _))
                .WithMessage("OnStillTied is not recognized.");
        });
    }
}

public sealed class UpsertTieBreakRulesCommandHandler : IRequestHandler<UpsertTieBreakRulesCommand, IReadOnlyList<TieBreakRuleAppDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public UpsertTieBreakRulesCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<TieBreakRuleAppDto>> Handle(UpsertTieBreakRulesCommand request, CancellationToken cancellationToken)
    {
        var existing = await _db.TieBreakRules
            .Where(r => r.ProgramId == request.ProgramId)
            .ToListAsync(cancellationToken);
        var existingById = existing.ToDictionary(r => r.Id);

        var actor = _currentUser.Email ?? "unknown";
        foreach (var dto in request.Rules)
        {
            var formatCode = Enum.Parse<QuestionFormatCode>(dto.TieBreakFormat, ignoreCase: true);
            var onStillTied = Enum.Parse<OnStillTiedPolicy>(dto.OnStillTied, ignoreCase: true);

            if (dto.Id != Guid.Empty && existingById.TryGetValue(dto.Id, out var rule))
            {
                rule.Update(dto.Criteria, formatCode, dto.QuestionCount, dto.SuddenDeath, dto.MaxExtraRounds, dto.ScoreCountsTowardStage, onStillTied, actor);
                continue;
            }

            var created = TieBreakRule.Create(
                request.ProgramId, $"Tie-break for stage {dto.StageId}", TieBreakScope.MatchRanking, dto.Criteria, actor, stageId: dto.StageId);
            created.Update(dto.Criteria, formatCode, dto.QuestionCount, dto.SuddenDeath, dto.MaxExtraRounds, dto.ScoreCountsTowardStage, onStillTied, actor);
            _db.TieBreakRules.Add(created);
        }

        await _db.SaveChangesAsync(cancellationToken);

        var all = await _db.TieBreakRules
            .Where(r => r.ProgramId == request.ProgramId)
            .ToListAsync(cancellationToken);
        return all.Select(r => r.ToDto()).ToList();
    }
}
