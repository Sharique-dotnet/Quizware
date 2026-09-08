using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.Authorization;
using QuizApp.Domain.Common.Exceptions;

namespace QuizApp.Application.Topics.Commands;

public sealed record DeleteTopicCommand(Guid ProgramId, Guid TopicId) : IRequest;

/// <summary>Both guards are real checks against tables that already
/// exist (Question CRUD isn't built until Phase 6f, but the Questions
/// table is there since Phase 4) — not placeholders.</summary>
public sealed class DeleteTopicCommandHandler : IRequestHandler<DeleteTopicCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public DeleteTopicCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteTopicCommand request, CancellationToken cancellationToken)
    {
        var topic = await _db.Topics
            .SingleOrDefaultAsync(
                t => t.Id == request.TopicId && (t.ProgramId == request.ProgramId || t.ProgramId == null), cancellationToken)
            ?? throw new KeyNotFoundException($"Topic '{request.TopicId}' was not found.");

        if (topic.ProgramId is null && !_currentUser.IsInRole(Roles.SuperAdmin))
        {
            throw new UnauthorizedAccessException("Only SuperAdmin may delete a shared topic.");
        }

        var hasChildren = await _db.Topics.AnyAsync(t => t.ParentTopicId == topic.Id, cancellationToken);
        if (hasChildren)
        {
            throw new InvalidStateTransitionException(
                $"Topic '{topic.Name}' has sub-topics — reparent or delete them first.");
        }

        var usedByQuestions = await _db.Questions.AnyAsync(q => q.TopicId == topic.Id, cancellationToken);
        if (usedByQuestions)
        {
            throw new InvalidStateTransitionException($"Topic '{topic.Name}' is used by one or more questions.");
        }

        topic.Delete(_currentUser.Email ?? "unknown");

        await _db.SaveChangesAsync(cancellationToken);
    }
}
