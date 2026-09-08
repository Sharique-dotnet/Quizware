using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.Authorization;

namespace QuizApp.Application.Tags.Commands;

/// <summary>No "tag in use" check — the Question&#8596;Tag join
/// (QuestionTag) is explicitly deferred (no such type/config exists
/// anywhere in the codebase yet), so there is nothing to check usage
/// against.</summary>
public sealed record DeleteTagCommand(Guid ProgramId, Guid TagId) : IRequest;

public sealed class DeleteTagCommandHandler : IRequestHandler<DeleteTagCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public DeleteTagCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteTagCommand request, CancellationToken cancellationToken)
    {
        var tag = await _db.Tags
            .SingleOrDefaultAsync(
                t => t.Id == request.TagId && (t.ProgramId == request.ProgramId || t.ProgramId == null), cancellationToken)
            ?? throw new KeyNotFoundException($"Tag '{request.TagId}' was not found.");

        if (tag.ProgramId is null && !_currentUser.IsInRole(Roles.SuperAdmin))
        {
            throw new UnauthorizedAccessException("Only SuperAdmin may delete a shared tag.");
        }

        tag.Delete(_currentUser.Email ?? "unknown");

        await _db.SaveChangesAsync(cancellationToken);
    }
}
