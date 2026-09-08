using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Teams.Dtos;
using Quizware.Domain.Enums;

namespace Quizware.Application.Teams.Commands;

public sealed record ChangeTeamStatusCommand(Guid ProgramId, Guid TeamId, TeamStatus Status, string Reason)
    : IRequest<TeamDto>;

public sealed class ChangeTeamStatusCommandValidator : AbstractValidator<ChangeTeamStatusCommand>
{
    public ChangeTeamStatusCommandValidator()
    {
        RuleFor(x => x.TeamId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

/// <summary>Team.ChangeStatus already enforces a mandatory reason and
/// stamps the timestamp/actor (P6-12) — this handler just resolves the
/// route/body into the domain call.</summary>
public sealed class ChangeTeamStatusCommandHandler : IRequestHandler<ChangeTeamStatusCommand, TeamDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ChangeTeamStatusCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<TeamDto> Handle(ChangeTeamStatusCommand request, CancellationToken cancellationToken)
    {
        var team = await _db.Teams
            .SingleOrDefaultAsync(t => t.Id == request.TeamId && t.ProgramId == request.ProgramId, cancellationToken)
            ?? throw new KeyNotFoundException($"Team '{request.TeamId}' was not found.");

        team.ChangeStatus(request.Status, request.Reason, _currentUser.Email ?? "unknown");

        await _db.SaveChangesAsync(cancellationToken);

        var members = await _db.TeamMembers.Where(m => m.TeamId == team.Id).ToListAsync(cancellationToken);
        return team.ToDto(members);
    }
}
