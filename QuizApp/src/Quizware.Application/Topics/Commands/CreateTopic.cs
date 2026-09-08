using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Authorization;
using Quizware.Application.Topics.Dtos;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.QuestionBank;

namespace Quizware.Application.Topics.Commands;

public sealed record CreateTopicCommand(Guid ProgramId, string Name, Guid? ParentTopicId, bool Shared) : IRequest<TopicDto>;

public sealed class CreateTopicCommandValidator : AbstractValidator<CreateTopicCommand>
{
    public CreateTopicCommandValidator()
    {
        RuleFor(x => x.ProgramId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    }
}

/// <summary>Shared = true (P6-14's "or shared") sets ProgramId to null —
/// only SuperAdmin may, since a shared topic affects every program.</summary>
public sealed class CreateTopicCommandHandler : IRequestHandler<CreateTopicCommand, TopicDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public CreateTopicCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<TopicDto> Handle(CreateTopicCommand request, CancellationToken cancellationToken)
    {
        if (request.Shared && !_currentUser.IsInRole(Roles.SuperAdmin))
        {
            throw new UnauthorizedAccessException("Only SuperAdmin may create a shared topic.");
        }

        var actor = _currentUser.Email ?? "unknown";
        var ownerProgramId = request.Shared ? (Guid?)null : request.ProgramId;

        var nameTaken = await _db.Topics
            .AnyAsync(t => t.ProgramId == ownerProgramId && t.Name == request.Name, cancellationToken);
        if (nameTaken)
        {
            throw new InvalidStateTransitionException($"A topic named '{request.Name}' already exists in this scope.");
        }

        if (request.ParentTopicId is Guid parentId)
        {
            await TopicVisibility.EnsureParentIsValidAsync(_db, request.ProgramId, parentId, ownTopicId: null, cancellationToken);
        }

        var topic = Topic.Create(request.Name, actor, ownerProgramId, request.ParentTopicId);
        _db.Topics.Add(topic);
        await _db.SaveChangesAsync(cancellationToken);

        return topic.ToDto();
    }
}

/// <summary>Shared helpers used by Create/Update — a row is visible to
/// program P iff ProgramId == P or ProgramId is null (shared). There is no
/// ITenantScoped global filter to lean on here, since ProgramId is
/// nullable.</summary>
internal static class TopicVisibility
{
    public static async Task EnsureParentIsValidAsync(
        IAppDbContext db, Guid programId, Guid parentId, Guid? ownTopicId, CancellationToken cancellationToken)
    {
        if (parentId == ownTopicId)
        {
            throw new InvalidStateTransitionException("A topic cannot be its own parent.");
        }

        var parent = await db.Topics
            .SingleOrDefaultAsync(t => t.Id == parentId && (t.ProgramId == programId || t.ProgramId == null), cancellationToken)
            ?? throw new KeyNotFoundException($"Topic '{parentId}' was not found.");

        if (ownTopicId is null)
        {
            return;
        }

        // Walk the proposed parent's own chain looking for a cycle. 100 is
        // a defensive bound against a corrupt chain, not a documented
        // tree-depth limit — none exists.
        var current = parent;
        for (var hop = 0; hop < 100 && current.ParentTopicId is not null; hop++)
        {
            if (current.ParentTopicId == ownTopicId)
            {
                throw new InvalidStateTransitionException("This change would make the topic its own ancestor.");
            }

            current = await db.Topics.SingleOrDefaultAsync(t => t.Id == current.ParentTopicId, cancellationToken)
                ?? throw new KeyNotFoundException($"Topic '{current.ParentTopicId}' was not found.");
        }
    }
}
