using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.Tournament.Dtos;
using QuizApp.Domain.Common.Exceptions;
using QuizApp.Domain.Enums;
using QuizApp.Domain.Tournament;

namespace QuizApp.Application.Tournament.Commands;

public sealed record CreateStageCommand(Guid ProgramId, string Name, int OrderIndex, string StageType) : IRequest<StageDto>;

public sealed class CreateStageCommandValidator : AbstractValidator<CreateStageCommand>
{
    public CreateStageCommandValidator()
    {
        RuleFor(x => x.ProgramId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.StageType).Must(s => Enum.TryParse<StageType>(s, ignoreCase: true, out _))
            .WithMessage("StageType must be one of: League, Knockout, Final, TieBreaker.");
    }
}

public sealed class CreateStageCommandHandler : IRequestHandler<CreateStageCommand, StageDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public CreateStageCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<StageDto> Handle(CreateStageCommand request, CancellationToken cancellationToken)
    {
        var orderTaken = await _db.Stages
            .AnyAsync(s => s.ProgramId == request.ProgramId && s.OrderIndex == request.OrderIndex, cancellationToken);
        if (orderTaken)
        {
            throw new InvalidStateTransitionException($"A stage already occupies order index {request.OrderIndex}.");
        }

        var actor = _currentUser.Email ?? "unknown";
        var stageType = Enum.Parse<StageType>(request.StageType, ignoreCase: true);
        var stage = Stage.Create(request.ProgramId, request.Name, request.OrderIndex, stageType, actor);
        _db.Stages.Add(stage);
        await _db.SaveChangesAsync(cancellationToken);

        return stage.ToDto([]);
    }
}
