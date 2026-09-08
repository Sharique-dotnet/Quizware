using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.Tournament.Dtos;
using QuizApp.Domain.Enums;

namespace QuizApp.Application.Tournament.Commands;

public sealed record SetSegmentOrderModeCommand(Guid ProgramId, Guid StageId, string SegmentOrderMode) : IRequest<StageDto>;

public sealed class SetSegmentOrderModeCommandValidator : AbstractValidator<SetSegmentOrderModeCommand>
{
    public SetSegmentOrderModeCommandValidator()
    {
        RuleFor(x => x.SegmentOrderMode).Must(s => Enum.TryParse<SegmentOrderMode>(s, ignoreCase: true, out _))
            .WithMessage("SegmentOrderMode must be one of: Fixed, RandomPerMatch, OperatorChoice.");
    }
}

public sealed class SetSegmentOrderModeCommandHandler : IRequestHandler<SetSegmentOrderModeCommand, StageDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public SetSegmentOrderModeCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<StageDto> Handle(SetSegmentOrderModeCommand request, CancellationToken cancellationToken)
    {
        var stage = await _db.Stages
            .SingleOrDefaultAsync(s => s.Id == request.StageId && s.ProgramId == request.ProgramId, cancellationToken)
            ?? throw new KeyNotFoundException($"Stage '{request.StageId}' was not found.");

        var mode = Enum.Parse<SegmentOrderMode>(request.SegmentOrderMode, ignoreCase: true);
        stage.SetSegmentOrderMode(mode, _currentUser.Email ?? "unknown");
        await _db.SaveChangesAsync(cancellationToken);

        var segments = await _db.StageSegmentTemplates.Where(s => s.StageId == stage.Id).ToListAsync(cancellationToken);
        return stage.ToDto(segments);
    }
}
