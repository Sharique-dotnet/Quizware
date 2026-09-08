using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.Authorization;
using QuizApp.Application.Topics.Dtos;
using QuizApp.Domain.Common.Exceptions;

namespace QuizApp.Application.Topics.Commands;

public sealed record UpdateTopicCommand(Guid ProgramId, Guid TopicId, string Name, Guid? ParentTopicId) : IRequest<TopicDto>;

public sealed class UpdateTopicCommandValidator : AbstractValidator<UpdateTopicCommand>
{
    public UpdateTopicCommandValidator()
    {
        RuleFor(x => x.TopicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    }
}

public sealed class UpdateTopicCommandHandler : IRequestHandler<UpdateTopicCommand, TopicDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public UpdateTopicCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<TopicDto> Handle(UpdateTopicCommand request, CancellationToken cancellationToken)
    {
        var topic = await _db.Topics
            .SingleOrDefaultAsync(
                t => t.Id == request.TopicId && (t.ProgramId == request.ProgramId || t.ProgramId == null), cancellationToken)
            ?? throw new KeyNotFoundException($"Topic '{request.TopicId}' was not found.");

        if (topic.ProgramId is null && !_currentUser.IsInRole(Roles.SuperAdmin))
        {
            throw new UnauthorizedAccessException("Only SuperAdmin may edit a shared topic.");
        }

        if (request.ParentTopicId is Guid parentId)
        {
            await TopicVisibility.EnsureParentIsValidAsync(_db, request.ProgramId, parentId, topic.Id, cancellationToken);
        }

        var nameTaken = await _db.Topics
            .AnyAsync(t => t.Id != topic.Id && t.ProgramId == topic.ProgramId && t.Name == request.Name, cancellationToken);
        if (nameTaken)
        {
            throw new InvalidStateTransitionException($"A topic named '{request.Name}' already exists in this scope.");
        }

        topic.UpdateDetails(request.Name, request.ParentTopicId, _currentUser.Email ?? "unknown");

        await _db.SaveChangesAsync(cancellationToken);

        return topic.ToDto();
    }
}
