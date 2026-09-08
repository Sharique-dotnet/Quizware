using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Rules.Dtos;
using Quizware.Domain.Enums;
using Quizware.Domain.Scoring;

namespace Quizware.Application.Rules.Commands;

/// <summary>Manages program-level scoring rules only — the contract DTO
/// carries no StageId/SegmentTemplateId, so a more specific rule can only be
/// created directly against the ScoringRule table today; this endpoint is
/// scoped to the program-wide defaults P7-06 asks for. Rows whose Id is
/// empty or unknown are created; a known Id updates Points.</summary>
public sealed record UpsertScoringRulesCommand(Guid ProgramId, IReadOnlyList<ScoringRuleAppDto> Rules) : IRequest<IReadOnlyList<ScoringRuleAppDto>>;

public sealed class UpsertScoringRulesCommandValidator : AbstractValidator<UpsertScoringRulesCommand>
{
    public UpsertScoringRulesCommandValidator()
    {
        RuleForEach(x => x.Rules).ChildRules(rule =>
        {
            rule.RuleFor(r => r.FormatCode).Must(f => Enum.TryParse<QuestionFormatCode>(f, ignoreCase: true, out _))
                .WithMessage("FormatCode is not a recognized question format.");
            rule.RuleFor(r => r.Outcome).Must(o => Enum.TryParse<AnswerOutcome>(o, ignoreCase: true, out _))
                .WithMessage("Outcome is not a recognized answer outcome.");
        });
    }
}

public sealed class UpsertScoringRulesCommandHandler : IRequestHandler<UpsertScoringRulesCommand, IReadOnlyList<ScoringRuleAppDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public UpsertScoringRulesCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ScoringRuleAppDto>> Handle(UpsertScoringRulesCommand request, CancellationToken cancellationToken)
    {
        var existing = await _db.ScoringRules
            .Where(r => r.ProgramId == request.ProgramId && r.StageId == null && r.SegmentTemplateId == null)
            .ToListAsync(cancellationToken);
        var existingById = existing.ToDictionary(r => r.Id);

        // UX_ScoringRule is a unique (ProgramId, StageId, SegmentTemplateId,
        // FormatCode, Outcome, ContextKey) index — a "create" whose natural
        // key already matches a program-wide rule (e.g. one seeded by
        // Reset Defaults) must update that row instead of inserting a
        // duplicate, or the unique index throws an unhandled 500. See L-007.
        var actor = _currentUser.Email ?? "unknown";
        foreach (var dto in request.Rules)
        {
            var formatCode = Enum.Parse<QuestionFormatCode>(dto.FormatCode, ignoreCase: true);
            var outcome = Enum.Parse<AnswerOutcome>(dto.Outcome, ignoreCase: true);

            if (dto.Id != Guid.Empty && existingById.TryGetValue(dto.Id, out var ruleById))
            {
                ruleById.UpdatePoints(dto.Points, description: null, actor);
                continue;
            }

            var ruleByNaturalKey = existing.SingleOrDefault(
                r => r.FormatCode == formatCode && r.Outcome == outcome && r.ContextKey == dto.ContextKey);
            if (ruleByNaturalKey is not null)
            {
                ruleByNaturalKey.UpdatePoints(dto.Points, description: null, actor);
                continue;
            }

            _db.ScoringRules.Add(ScoringRule.Create(
                request.ProgramId, formatCode, outcome, dto.Points, actor, contextKey: dto.ContextKey));
        }

        await _db.SaveChangesAsync(cancellationToken);

        var all = await _db.ScoringRules
            .Where(r => r.ProgramId == request.ProgramId && r.StageId == null && r.SegmentTemplateId == null)
            .ToListAsync(cancellationToken);
        return all.Select(r => r.ToDto()).ToList();
    }
}
