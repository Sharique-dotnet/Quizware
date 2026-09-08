using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.QuestionBank.Dtos;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;

namespace Quizware.Application.QuestionBank.Commands;

public sealed record ApproveQuestionCommand(Guid ProgramId, Guid QuestionId) : IRequest<QuestionDto>;

public sealed class ApproveQuestionCommandHandler : IRequestHandler<ApproveQuestionCommand, QuestionDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ApproveQuestionCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<QuestionDto> Handle(ApproveQuestionCommand request, CancellationToken cancellationToken)
    {
        var question = await _db.Questions
            .SingleOrDefaultAsync(q => q.Id == request.QuestionId && q.ProgramId == request.ProgramId, cancellationToken)
            ?? throw new KeyNotFoundException($"Question '{request.QuestionId}' was not found.");

        // Question.Approve() throws InvalidOperationException (its own,
        // already-tested contract) when not Draft — that isn't one of the
        // exception types GlobalExceptionHandler maps, so it would surface
        // as a raw 500. Checking state here first gives a clean 409
        // instead, without changing the domain method or its test.
        if (question.Status != QuestionStatus.Draft)
        {
            throw new InvalidStateTransitionException($"Question is {question.Status}; only a Draft question may be approved.");
        }

        question.Approve(_currentUser.UserId ?? Guid.Empty);

        await _db.SaveChangesAsync(cancellationToken);

        return await QuestionMapper.ToDtoAsync(_db, question, cancellationToken);
    }
}
