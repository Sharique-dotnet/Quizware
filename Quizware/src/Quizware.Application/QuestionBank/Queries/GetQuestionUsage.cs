using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.QuestionBank.Dtos;

namespace Quizware.Application.QuestionBank.Queries;

public sealed record GetQuestionUsageQuery(Guid ProgramId, Guid QuestionId) : IRequest<IReadOnlyList<QuestionUsageEntryDto>>;

public sealed class GetQuestionUsageQueryHandler : IRequestHandler<GetQuestionUsageQuery, IReadOnlyList<QuestionUsageEntryDto>>
{
    private readonly IAppDbContext _db;

    public GetQuestionUsageQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<QuestionUsageEntryDto>> Handle(GetQuestionUsageQuery request, CancellationToken cancellationToken)
    {
        if (!await _db.Questions.AnyAsync(q => q.Id == request.QuestionId && q.ProgramId == request.ProgramId, cancellationToken))
        {
            throw new KeyNotFoundException($"Question '{request.QuestionId}' was not found.");
        }

        return await _db.QuestionUsageHistories
            .Where(h => h.QuestionId == request.QuestionId)
            .Join(_db.Matches, h => h.MatchId, m => m.Id, (h, m) => new { h.MatchId, m.Name, m.MatchNumber, h.UsedAtUtc })
            .OrderBy(x => x.UsedAtUtc)
            .Select(x => new QuestionUsageEntryDto(x.MatchId, x.Name ?? $"Match {x.MatchNumber}", x.UsedAtUtc))
            .ToListAsync(cancellationToken);
    }
}
