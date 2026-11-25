using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Common.Exceptions;

namespace Quizware.Application.Rules;

/// <summary>P15-03: a rule may only point at the program's own stages. The
/// stage id arrives in the request body, where the route's program scope
/// does not reach, so every rule upsert checks it here.</summary>
internal static class RuleStages
{
    public static async Task EnsureOwnedAsync(
        IAppDbContext db, Guid programId, IEnumerable<Guid?> stageIds, CancellationToken cancellationToken)
    {
        var ids = stageIds.Where(id => id is not null).Select(id => id!.Value).Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var owned = await db.Stages
            .Where(s => s.ProgramId == programId && ids.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);
        var foreign = ids.Except(owned).ToList();
        if (foreign.Count > 0)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["Rules"] = [$"Not stages of this program: {string.Join(", ", foreign)}."],
            });
        }
    }
}
