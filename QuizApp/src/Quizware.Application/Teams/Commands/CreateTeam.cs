using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Teams.Dtos;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Teams;

namespace Quizware.Application.Teams.Commands;

public sealed record CreateTeamCommand(
    Guid ProgramId, string Code, string SchoolName, string DisplayName, IReadOnlyList<string> MemberNames)
    : IRequest<TeamDto>;

public sealed class CreateTeamCommandValidator : AbstractValidator<CreateTeamCommand>
{
    public CreateTeamCommandValidator()
    {
        RuleFor(x => x.ProgramId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30);
        RuleFor(x => x.SchoolName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
    }
}

/// <summary>No hardcoded team limit (P6-11) — the only cap is the
/// program's own optional MaxTeams, checked here and nowhere else, so the
/// single-create and Excel-commit paths can never disagree.</summary>
public sealed class CreateTeamCommandHandler : IRequestHandler<CreateTeamCommand, TeamDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public CreateTeamCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<TeamDto> Handle(CreateTeamCommand request, CancellationToken cancellationToken)
    {
        var program = await _db.Programs.SingleOrDefaultAsync(p => p.Id == request.ProgramId, cancellationToken)
            ?? throw new KeyNotFoundException($"Program '{request.ProgramId}' was not found.");

        if (program.MaxTeams is int maxTeams)
        {
            var currentCount = await _db.Teams.CountAsync(t => t.ProgramId == request.ProgramId, cancellationToken);
            if (currentCount >= maxTeams)
            {
                throw new InvalidStateTransitionException(
                    $"Program '{program.Code}' already has {currentCount} teams, its configured maximum ({maxTeams}).");
            }
        }

        var codeTaken = await _db.Teams
            .AnyAsync(t => t.ProgramId == request.ProgramId && t.Code == request.Code, cancellationToken);
        if (codeTaken)
        {
            throw new InvalidStateTransitionException($"Team code '{request.Code}' is already in use in this program.");
        }

        var actor = _currentUser.Email ?? "unknown";
        var team = Team.Register(request.ProgramId, request.Code, request.SchoolName, request.DisplayName, actor);
        _db.Teams.Add(team);

        var members = new List<TeamMember>();
        foreach (var name in request.MemberNames)
        {
            var member = TeamMember.Create(request.ProgramId, team.Id, name, actor);
            members.Add(member);
            _db.TeamMembers.Add(member);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return team.ToDto(members);
    }
}
