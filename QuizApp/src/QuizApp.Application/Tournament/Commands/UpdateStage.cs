using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.Tournament.Dtos;

namespace QuizApp.Application.Tournament.Commands;

public sealed record UpdateStageCommand(Guid ProgramId, Guid StageId, string Name) : IRequest<StageDto>;

public sealed class UpdateStageCommandValidator : AbstractValidator<UpdateStageCommand>
{
    public UpdateStageCommandValidator()
    {
        RuleFor(x => x.StageId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    }
}

public sealed class UpdateStageCommandHandler : IRequestHandler<UpdateStageCommand, StageDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public UpdateStageCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<StageDto> Handle(UpdateStageCommand request, CancellationToken cancellationToken)
    {
        var stage = await _db.Stages
            .SingleOrDefaultAsync(s => s.Id == request.StageId && s.ProgramId == request.ProgramId, cancellationToken)
            ?? throw new KeyNotFoundException($"Stage '{request.StageId}' was not found.");

        stage.Rename(request.Name, _currentUser.Email ?? "unknown");
        await _db.SaveChangesAsync(cancellationToken);

        var segments = await _db.StageSegmentTemplates
            .Where(s => s.StageId == stage.Id).ToListAsync(cancellationToken);
        return stage.ToDto(segments);
    }
}
