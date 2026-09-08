using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.QuestionBank.Dtos;

namespace Quizware.Application.QuestionBank.Queries;

public sealed record GetQuestionByIdQuery(Guid ProgramId, Guid QuestionId) : IRequest<QuestionDto>;

public sealed class GetQuestionByIdQueryHandler : IRequestHandler<GetQuestionByIdQuery, QuestionDto>
{
    private readonly IAppDbContext _db;

    public GetQuestionByIdQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<QuestionDto> Handle(GetQuestionByIdQuery request, CancellationToken cancellationToken)
    {
        var question = await _db.Questions
            .SingleOrDefaultAsync(q => q.Id == request.QuestionId && q.ProgramId == request.ProgramId, cancellationToken)
            ?? throw new KeyNotFoundException($"Question '{request.QuestionId}' was not found.");

        return await QuestionMapper.ToDtoAsync(_db, question, cancellationToken);
    }
}
