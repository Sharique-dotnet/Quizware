using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;
using Quizware.Domain.Scoring;
using Quizware.Domain.Tournament;

namespace Quizware.Application.Rules.Services;

public sealed class RuleService : IRuleService
{
    private readonly IAppDbContext _db;

    public RuleService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ScoringRule> ResolveScoringRuleAsync(
        Guid programId, QuestionFormatCode formatCode, AnswerOutcome outcome,
        Guid? stageId, Guid? segmentTemplateId, string? contextKey, CancellationToken cancellationToken)
    {
        var candidates = await _db.ScoringRules
            .Where(r => r.ProgramId == programId && r.FormatCode == formatCode && r.Outcome == outcome
                && r.ContextKey == contextKey
                && (r.SegmentTemplateId == null || r.SegmentTemplateId == segmentTemplateId)
                && (r.StageId == null || r.StageId == stageId))
            .ToListAsync(cancellationToken);

        var resolved = candidates.OrderByDescending(r => r.Specificity).FirstOrDefault();

        return resolved ?? throw new ScoringRuleNotFoundException(
            $"No scoring rule for program '{programId}', format {formatCode}, outcome {outcome}" +
            (contextKey is null ? "." : $", context '{contextKey}'."));
    }

    public async Task<QuestionSelectionRule?> ResolveSelectionRuleAsync(
        Guid programId, QuestionFormatCode formatCode, Guid? stageId, Guid? segmentTemplateId,
        CancellationToken cancellationToken)
    {
        var candidates = await _db.QuestionSelectionRules
            .Where(r => r.ProgramId == programId && r.FormatCode == formatCode
                && (r.SegmentTemplateId == null || r.SegmentTemplateId == segmentTemplateId)
                && (r.StageId == null || r.StageId == stageId))
            .ToListAsync(cancellationToken);

        return candidates.OrderByDescending(r => r.Specificity).FirstOrDefault();
    }
}
