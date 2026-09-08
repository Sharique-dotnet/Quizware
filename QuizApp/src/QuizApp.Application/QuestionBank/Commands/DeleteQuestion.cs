using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Domain.Common.Exceptions;

namespace QuizApp.Application.QuestionBank.Commands;

/// <summary>A question that has ever been used (TimesUsed &gt; 0) can't be
/// deleted, only retired — mirrors the Team/Topic delete-guard convention
/// established in Phases 6c/6d.</summary>
public sealed record DeleteQuestionCommand(Guid ProgramId, Guid QuestionId) : IRequest;

public sealed class DeleteQuestionCommandHandler : IRequestHandler<DeleteQuestionCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public DeleteQuestionCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteQuestionCommand request, CancellationToken cancellationToken)
    {
        var question = await _db.Questions
            .SingleOrDefaultAsync(q => q.Id == request.QuestionId && q.ProgramId == request.ProgramId, cancellationToken)
            ?? throw new KeyNotFoundException($"Question '{request.QuestionId}' was not found.");

        if (question.TimesUsed > 0)
        {
            throw new InvalidStateTransitionException("This question has already been used and cannot be deleted — retire it instead.");
        }

        question.Delete(_currentUser.Email ?? "unknown");

        await _db.SaveChangesAsync(cancellationToken);
    }
}
