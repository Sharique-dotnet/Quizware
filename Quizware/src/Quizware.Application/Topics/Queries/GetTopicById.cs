using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Topics.Dtos;

namespace Quizware.Application.Topics.Queries;

public sealed record GetTopicByIdQuery(Guid ProgramId, Guid TopicId) : IRequest<TopicDto>;

public sealed class GetTopicByIdQueryHandler : IRequestHandler<GetTopicByIdQuery, TopicDto>
{
    private readonly IAppDbContext _db;

    public GetTopicByIdQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<TopicDto> Handle(GetTopicByIdQuery request, CancellationToken cancellationToken)
    {
        var topic = await _db.Topics
            .SingleOrDefaultAsync(
                t => t.Id == request.TopicId && (t.ProgramId == request.ProgramId || t.ProgramId == null), cancellationToken)
            ?? throw new KeyNotFoundException($"Topic '{request.TopicId}' was not found.");

        return topic.ToDto();
    }
}
