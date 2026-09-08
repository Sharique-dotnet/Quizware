using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.Tournament.Dtos;

namespace QuizApp.Application.Tournament.Commands;

public sealed record UpdateSegmentTemplateCommand(
    Guid ProgramId, Guid StageId, Guid SegmentTemplateId, int QuestionCount, bool IsOrderLocked) : IRequest<SegmentTemplateDto>;

public sealed class UpdateSegmentTemplateCommandValidator : AbstractValidator<UpdateSegmentTemplateCommand>
{
    public UpdateSegmentTemplateCommandValidator()
    {
        RuleFor(x => x.QuestionCount).GreaterThanOrEqualTo(1);
    }
}

public sealed class UpdateSegmentTemplateCommandHandler : IRequestHandler<UpdateSegmentTemplateCommand, SegmentTemplateDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public UpdateSegmentTemplateCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<SegmentTemplateDto> Handle(UpdateSegmentTemplateCommand request, CancellationToken cancellationToken)
    {
        var segment = await _db.StageSegmentTemplates
            .SingleOrDefaultAsync(
                s => s.Id == request.SegmentTemplateId && s.StageId == request.StageId && s.ProgramId == request.ProgramId,
                cancellationToken)
            ?? throw new KeyNotFoundException($"Segment '{request.SegmentTemplateId}' was not found.");

        segment.Update(request.QuestionCount, request.IsOrderLocked, _currentUser.Email ?? "unknown");
        await _db.SaveChangesAsync(cancellationToken);

        return segment.ToDto();
    }
}
