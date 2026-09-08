using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.Tournament.Dtos;
using QuizApp.Domain.Enums;
using QuizApp.Domain.Tournament;

namespace QuizApp.Application.Tournament.Commands;

public sealed record CreateSegmentTemplateCommand(
    Guid ProgramId, Guid StageId, string FormatCode, int QuestionCount, bool IsOrderLocked) : IRequest<SegmentTemplateDto>;

public sealed class CreateSegmentTemplateCommandValidator : AbstractValidator<CreateSegmentTemplateCommand>
{
    public CreateSegmentTemplateCommandValidator()
    {
        RuleFor(x => x.StageId).NotEmpty();
        RuleFor(x => x.QuestionCount).GreaterThanOrEqualTo(1);
        RuleFor(x => x.FormatCode).Must(f => Enum.TryParse<QuestionFormatCode>(f, ignoreCase: true, out _))
            .WithMessage("FormatCode is not a recognized question format.");
    }
}

public sealed class CreateSegmentTemplateCommandHandler : IRequestHandler<CreateSegmentTemplateCommand, SegmentTemplateDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public CreateSegmentTemplateCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<SegmentTemplateDto> Handle(CreateSegmentTemplateCommand request, CancellationToken cancellationToken)
    {
        var stage = await _db.Stages
            .SingleOrDefaultAsync(s => s.Id == request.StageId && s.ProgramId == request.ProgramId, cancellationToken)
            ?? throw new KeyNotFoundException($"Stage '{request.StageId}' was not found.");

        var nextOrderIndex = await _db.StageSegmentTemplates
            .Where(s => s.StageId == stage.Id)
            .Select(s => (int?)s.OrderIndex)
            .MaxAsync(cancellationToken) ?? -1;

        var actor = _currentUser.Email ?? "unknown";
        var formatCode = Enum.Parse<QuestionFormatCode>(request.FormatCode, ignoreCase: true);
        var segment = StageSegmentTemplate.Create(request.ProgramId, stage.Id, formatCode, nextOrderIndex + 1, request.QuestionCount, actor);
        if (request.IsOrderLocked)
        {
            segment.Lock(actor);
        }

        _db.StageSegmentTemplates.Add(segment);
        await _db.SaveChangesAsync(cancellationToken);

        return segment.ToDto();
    }
}
