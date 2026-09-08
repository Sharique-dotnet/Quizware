using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.Tags.Dtos;

namespace QuizApp.Application.Tags.Queries;

public sealed record GetTagByIdQuery(Guid ProgramId, Guid TagId) : IRequest<TagDto>;

public sealed class GetTagByIdQueryHandler : IRequestHandler<GetTagByIdQuery, TagDto>
{
    private readonly IAppDbContext _db;

    public GetTagByIdQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<TagDto> Handle(GetTagByIdQuery request, CancellationToken cancellationToken)
    {
        var tag = await _db.Tags
            .SingleOrDefaultAsync(
                t => t.Id == request.TagId && (t.ProgramId == request.ProgramId || t.ProgramId == null), cancellationToken)
            ?? throw new KeyNotFoundException($"Tag '{request.TagId}' was not found.");

        return tag.ToDto();
    }
}
