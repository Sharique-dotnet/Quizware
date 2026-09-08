using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;
using Quizware.Domain.QuestionBank;

namespace Quizware.Application.QuestionBank;

/// <summary>Shared by every per-format create/update handler. TagIds is
/// validated (every id must exist and be visible to the program) but never
/// linked to anything yet — QuestionTag, the join table, is explicitly
/// deferred (Implementation-Plan.md notes it as an open gap; no such type
/// or EF config exists anywhere in this codebase). Validating now and
/// wiring the join later, once it exists, is a smaller step than silently
/// accepting ids that go nowhere.</summary>
internal static class QuestionCommon
{
    public static async Task EnsureTopicAndTagsExistAsync(
        IAppDbContext db, Guid programId, Guid? topicId, IReadOnlyList<Guid> tagIds, CancellationToken cancellationToken)
    {
        if (topicId is Guid id)
        {
            var topicExists = await db.Topics
                .AnyAsync(t => t.Id == id && (t.ProgramId == programId || t.ProgramId == null), cancellationToken);
            if (!topicExists)
            {
                throw new KeyNotFoundException($"Topic '{id}' was not found.");
            }
        }

        if (tagIds.Count > 0)
        {
            var visibleTagCount = await db.Tags
                .Where(t => tagIds.Contains(t.Id) && (t.ProgramId == programId || t.ProgramId == null))
                .CountAsync(cancellationToken);
            if (visibleTagCount != tagIds.Distinct().Count())
            {
                throw new KeyNotFoundException("One or more tags were not found.");
            }
        }
    }

    /// <summary>P6-16's PUT reuses the matching format's Create command —
    /// this resolves and validates the question being replaced (must
    /// exist, same program, same format) rather than every handler
    /// duplicating that check.</summary>
    public static async Task<Question?> ResolveReplacementTargetAsync(
        IAppDbContext db, Guid programId, Guid? replacesQuestionId, QuestionFormatCode expectedFormat, CancellationToken cancellationToken)
    {
        if (replacesQuestionId is not Guid id)
        {
            return null;
        }

        var existing = await db.Questions.SingleOrDefaultAsync(q => q.Id == id && q.ProgramId == programId, cancellationToken)
            ?? throw new KeyNotFoundException($"Question '{id}' was not found.");

        if (existing.FormatCode != expectedFormat)
        {
            throw new InvalidStateTransitionException(
                $"Question '{id}' is format {existing.FormatCode}, not {expectedFormat} — PUT must target the same format.");
        }

        return existing;
    }

    /// <summary>P6-17: "a question used in a live match shall not be
    /// editable; a new version shall be created instead" (FR-3.10). If the
    /// old row was never used, it's soft-deleted (no history value in
    /// keeping an unused draft artifact around); if it was used, it's
    /// retired instead so its history/usage stays intact.</summary>
    public static void ApplyVersioning(Question newQuestion, Question? previousVersion, string actor)
    {
        if (previousVersion is null)
        {
            return;
        }

        newQuestion.LinkSupersedes(previousVersion.Id, previousVersion.Version);

        if (previousVersion.TimesUsed > 0)
        {
            previousVersion.Retire();
        }
        else
        {
            previousVersion.Delete(actor);
        }
    }
}
