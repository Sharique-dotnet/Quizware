using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.Topics.Dtos;

namespace QuizApp.Application.Topics.Queries;

public sealed record ListTopicsQuery(Guid ProgramId) : IRequest<IReadOnlyList<TopicDto>>;

public sealed class ListTopicsQueryHandler : IRequestHandler<ListTopicsQuery, IReadOnlyList<TopicDto>>
{
    private readonly IAppDbContext _db;

    public ListTopicsQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TopicDto>> Handle(ListTopicsQuery request, CancellationToken cancellationToken)
    {
        var topics = await _db.Topics
            .Where(t => t.ProgramId == request.ProgramId || t.ProgramId == null)
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);

        return topics.Select(t => t.ToDto()).ToList();
    }
}
