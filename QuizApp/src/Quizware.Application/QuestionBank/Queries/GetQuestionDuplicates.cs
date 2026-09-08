using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.QuestionBank.Dtos;

namespace Quizware.Application.QuestionBank.Queries;

/// <summary>P6-20 / FR-3.9: "near-duplicate", not exact — a Jaccard
/// similarity over each question's NormalizedText word set. Pairwise over
/// one program's questions (expected to be at most a few thousand rows at
/// this project's scale, not the millions where O(n^2) would matter).</summary>
public sealed record GetQuestionDuplicatesQuery(Guid ProgramId) : IRequest<IReadOnlyList<DuplicateQuestionPairDto>>;

public sealed class GetQuestionDuplicatesQueryHandler : IRequestHandler<GetQuestionDuplicatesQuery, IReadOnlyList<DuplicateQuestionPairDto>>
{
    private const double SimilarityThreshold = 0.6;

    private readonly IAppDbContext _db;

    public GetQuestionDuplicatesQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<DuplicateQuestionPairDto>> Handle(
        GetQuestionDuplicatesQuery request, CancellationToken cancellationToken)
    {
        var candidates = await _db.Questions
            .Where(q => q.ProgramId == request.ProgramId && q.NormalizedText != null)
            .Select(q => new { q.Id, q.NormalizedText })
            .ToListAsync(cancellationToken);

        var wordSets = candidates.ToDictionary(c => c.Id, c => c.NormalizedText!.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet());

        var pairs = new List<DuplicateQuestionPairDto>();
        for (var i = 0; i < candidates.Count; i++)
        {
            for (var j = i + 1; j < candidates.Count; j++)
            {
                var a = wordSets[candidates[i].Id];
                var b = wordSets[candidates[j].Id];
                var similarity = JaccardSimilarity(a, b);
                if (similarity >= SimilarityThreshold)
                {
                    pairs.Add(new DuplicateQuestionPairDto(candidates[j].Id, candidates[i].Id, Math.Round(similarity, 2)));
                }
            }
        }

        return pairs;
    }

    private static double JaccardSimilarity(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 && b.Count == 0)
        {
            return 0;
        }

        var intersection = a.Intersect(b).Count();
        var union = a.Union(b).Count();
        return union == 0 ? 0 : (double)intersection / union;
    }
}
