using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.QuestionBank.Dtos;
using Quizware.Domain.Enums;

namespace Quizware.Application.QuestionBank.Queries;

/// <summary>TagId is deliberately not filterable — QuestionTag (the join
/// table) doesn't exist anywhere in this codebase yet (explicitly
/// deferred). Silently ignoring it here is more honest than pretending to
/// filter by something that isn't wired up.</summary>
public sealed record ListQuestionsQuery(
    Guid ProgramId, QuestionFormatCode? FormatCode, byte? DifficultyLevelId, Guid? TopicId,
    QuestionStatus? Status, string? Text) : IRequest<IReadOnlyList<QuestionSummaryDto>>;

public sealed class ListQuestionsQueryHandler : IRequestHandler<ListQuestionsQuery, IReadOnlyList<QuestionSummaryDto>>
{
    private readonly IAppDbContext _db;

    public ListQuestionsQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<QuestionSummaryDto>> Handle(ListQuestionsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.Questions.Where(q => q.ProgramId == request.ProgramId);

        if (request.FormatCode is QuestionFormatCode formatCode)
        {
            query = query.Where(q => q.FormatCode == formatCode);
        }

        if (request.DifficultyLevelId is byte difficultyLevelId)
        {
            query = query.Where(q => q.DifficultyLevel == (DifficultyLevel)difficultyLevelId);
        }

        if (request.TopicId is Guid topicId)
        {
            query = query.Where(q => q.TopicId == topicId);
        }

        if (request.Status is QuestionStatus status)
        {
            query = query.Where(q => q.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Text))
        {
            query = query.Where(q => q.QuestionText != null && q.QuestionText.Contains(request.Text));
        }

        var questions = await query.OrderByDescending(q => q.CreatedAtUtc).ToListAsync(cancellationToken);

        var topicIds = questions.Where(q => q.TopicId is not null).Select(q => q.TopicId!.Value).Distinct().ToList();
        var topicNamesById = topicIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Topics.Where(t => topicIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

        return questions
            .Select(q => QuestionMapper.ToSummaryDto(q, q.TopicId is Guid id ? topicNamesById.GetValueOrDefault(id) : null))
            .ToList();
    }
}
