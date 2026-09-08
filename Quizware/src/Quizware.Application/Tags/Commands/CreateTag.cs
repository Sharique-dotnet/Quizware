using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Authorization;
using Quizware.Application.Tags.Dtos;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.QuestionBank;

namespace Quizware.Application.Tags.Commands;

public sealed record CreateTagCommand(Guid ProgramId, string Name, bool Shared) : IRequest<TagDto>;

public sealed class CreateTagCommandValidator : AbstractValidator<CreateTagCommand>
{
    public CreateTagCommandValidator()
    {
        RuleFor(x => x.ProgramId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
    }
}

public sealed class CreateTagCommandHandler : IRequestHandler<CreateTagCommand, TagDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public CreateTagCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<TagDto> Handle(CreateTagCommand request, CancellationToken cancellationToken)
    {
        if (request.Shared && !_currentUser.IsInRole(Roles.SuperAdmin))
        {
            throw new UnauthorizedAccessException("Only SuperAdmin may create a shared tag.");
        }

        var actor = _currentUser.Email ?? "unknown";
        var ownerProgramId = request.Shared ? (Guid?)null : request.ProgramId;

        var nameTaken = await _db.Tags.AnyAsync(t => t.ProgramId == ownerProgramId && t.Name == request.Name, cancellationToken);
        if (nameTaken)
        {
            throw new InvalidStateTransitionException($"A tag named '{request.Name}' already exists in this scope.");
        }

        var tag = Tag.Create(request.Name, actor, ownerProgramId);
        _db.Tags.Add(tag);
        await _db.SaveChangesAsync(cancellationToken);

        return tag.ToDto();
    }
}
