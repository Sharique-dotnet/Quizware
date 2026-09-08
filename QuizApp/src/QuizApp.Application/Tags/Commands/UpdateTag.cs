using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.Authorization;
using QuizApp.Application.Tags.Dtos;
using QuizApp.Domain.Common.Exceptions;

namespace QuizApp.Application.Tags.Commands;

public sealed record UpdateTagCommand(Guid ProgramId, Guid TagId, string Name) : IRequest<TagDto>;

public sealed class UpdateTagCommandValidator : AbstractValidator<UpdateTagCommand>
{
    public UpdateTagCommandValidator()
    {
        RuleFor(x => x.TagId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
    }
}

public sealed class UpdateTagCommandHandler : IRequestHandler<UpdateTagCommand, TagDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public UpdateTagCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<TagDto> Handle(UpdateTagCommand request, CancellationToken cancellationToken)
    {
        var tag = await _db.Tags
            .SingleOrDefaultAsync(
                t => t.Id == request.TagId && (t.ProgramId == request.ProgramId || t.ProgramId == null), cancellationToken)
            ?? throw new KeyNotFoundException($"Tag '{request.TagId}' was not found.");

        if (tag.ProgramId is null && !_currentUser.IsInRole(Roles.SuperAdmin))
        {
            throw new UnauthorizedAccessException("Only SuperAdmin may edit a shared tag.");
        }

        var nameTaken = await _db.Tags
            .AnyAsync(t => t.Id != tag.Id && t.ProgramId == tag.ProgramId && t.Name == request.Name, cancellationToken);
        if (nameTaken)
        {
            throw new InvalidStateTransitionException($"A tag named '{request.Name}' already exists in this scope.");
        }

        tag.UpdateDetails(request.Name, _currentUser.Email ?? "unknown");

        await _db.SaveChangesAsync(cancellationToken);

        return tag.ToDto();
    }
}
