using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.Teams.Dtos;

namespace QuizApp.Application.Teams.Commands;

public sealed record SetTeamImagesCommand(Guid ProgramId, Guid TeamId, string? ScoreImageUrl, string? SelectionImageUrl)
    : IRequest<TeamDto>;

public sealed class SetTeamImagesCommandValidator : AbstractValidator<SetTeamImagesCommand>
{
    public SetTeamImagesCommandValidator()
    {
        RuleFor(x => x.TeamId).NotEmpty();
    }
}

public sealed class SetTeamImagesCommandHandler : IRequestHandler<SetTeamImagesCommand, TeamDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public SetTeamImagesCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<TeamDto> Handle(SetTeamImagesCommand request, CancellationToken cancellationToken)
    {
        var team = await _db.Teams
            .SingleOrDefaultAsync(t => t.Id == request.TeamId && t.ProgramId == request.ProgramId, cancellationToken)
            ?? throw new KeyNotFoundException($"Team '{request.TeamId}' was not found.");

        team.SetImages(request.ScoreImageUrl, request.SelectionImageUrl, _currentUser.Email ?? "unknown");

        await _db.SaveChangesAsync(cancellationToken);

        var members = await _db.TeamMembers.Where(m => m.TeamId == team.Id).ToListAsync(cancellationToken);
        return team.ToDto(members);
    }
}
