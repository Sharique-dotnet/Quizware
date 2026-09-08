using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.Tags.Dtos;

namespace QuizApp.Application.Tags.Queries;

public sealed record ListTagsQuery(Guid ProgramId) : IRequest<IReadOnlyList<TagDto>>;

public sealed class ListTagsQueryHandler : IRequestHandler<ListTagsQuery, IReadOnlyList<TagDto>>
{
    private readonly IAppDbContext _db;

    public ListTagsQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TagDto>> Handle(ListTagsQuery request, CancellationToken cancellationToken)
    {
        var tags = await _db.Tags
            .Where(t => t.ProgramId == request.ProgramId || t.ProgramId == null)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        return tags.Select(t => t.ToDto()).ToList();
    }
}
