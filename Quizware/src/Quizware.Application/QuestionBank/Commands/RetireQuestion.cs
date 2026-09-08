using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.QuestionBank.Dtos;

namespace Quizware.Application.QuestionBank.Commands;

/// <summary>Question has no RetiredReason column — the request's Reason
/// field is accepted (for the caller's own audit trail elsewhere) but not
/// persisted; adding a column for it is out of scope for P6-18's actual
/// acceptance criterion (Draft/Approved/Retired, only Approved is
/// selectable — already true via Question.IsSelectable).</summary>
public sealed record RetireQuestionCommand(Guid ProgramId, Guid QuestionId, string? Reason) : IRequest<QuestionDto>;

public sealed class RetireQuestionCommandHandler : IRequestHandler<RetireQuestionCommand, QuestionDto>
{
    private readonly IAppDbContext _db;

    public RetireQuestionCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<QuestionDto> Handle(RetireQuestionCommand request, CancellationToken cancellationToken)
    {
        var question = await _db.Questions
            .SingleOrDefaultAsync(q => q.Id == request.QuestionId && q.ProgramId == request.ProgramId, cancellationToken)
            ?? throw new KeyNotFoundException($"Question '{request.QuestionId}' was not found.");

        question.Retire();

        await _db.SaveChangesAsync(cancellationToken);

        return await QuestionMapper.ToDtoAsync(_db, question, cancellationToken);
    }
}
