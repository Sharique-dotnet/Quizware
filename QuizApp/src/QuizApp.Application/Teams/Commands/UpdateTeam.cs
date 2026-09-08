using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.Teams.Dtos;

namespace QuizApp.Application.Teams.Commands;

public sealed record UpdateTeamCommand(
    Guid ProgramId,
    Guid TeamId,
    string SchoolName,
    string DisplayName,
    string? ShortName,
    string? ContactName,
    string? ContactPhone,
    string? ContactEmail) : IRequest<TeamDto>;

public sealed class UpdateTeamCommandValidator : AbstractValidator<UpdateTeamCommand>
{
    public UpdateTeamCommandValidator()
    {
        RuleFor(x => x.TeamId).NotEmpty();
        RuleFor(x => x.SchoolName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
    }
}

public sealed class UpdateTeamCommandHandler : IRequestHandler<UpdateTeamCommand, TeamDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public UpdateTeamCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<TeamDto> Handle(UpdateTeamCommand request, CancellationToken cancellationToken)
    {
        var team = await _db.Teams
            .SingleOrDefaultAsync(t => t.Id == request.TeamId && t.ProgramId == request.ProgramId, cancellationToken)
            ?? throw new KeyNotFoundException($"Team '{request.TeamId}' was not found.");

        team.UpdateDetails(
            request.SchoolName,
            request.DisplayName,
            request.ShortName,
            request.ContactName,
            request.ContactPhone,
            request.ContactEmail,
            _currentUser.Email ?? "unknown");

        await _db.SaveChangesAsync(cancellationToken);

        var members = await _db.TeamMembers.Where(m => m.TeamId == team.Id).ToListAsync(cancellationToken);
        return team.ToDto(members);
    }
}
