using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using ValidationException = Quizware.Application.Common.Exceptions.ValidationException;
using Quizware.Application.Gameplay.Dtos;
using Quizware.Domain.Enums;
using Quizware.Domain.Gameplay;

namespace Quizware.Application.Gameplay.Commands;

public sealed record AddMatchSegmentCommand(Guid ProgramId, Guid MatchId, string FormatCode, int QuestionCount)
    : IRequest<MatchSegmentDto>;

public sealed class AddMatchSegmentCommandValidator : AbstractValidator<AddMatchSegmentCommand>
{
    public AddMatchSegmentCommandValidator()
    {
        RuleFor(x => x.FormatCode).Must(f => Enum.TryParse<QuestionFormatCode>(f, ignoreCase: true, out _))
            .WithMessage($"FormatCode must be one of: {string.Join(", ", Enum.GetNames<QuestionFormatCode>())}.");
        RuleFor(x => x.QuestionCount).GreaterThan(0);
    }
}

/// <summary>An extra segment for one match only. It borrows the stage's
/// template of the same format, because that template is what scoring and
/// selection rules are resolved against — a format the stage has no template
/// for has no rules to play by, so it is refused.</summary>
public sealed class AddMatchSegmentCommandHandler : IRequestHandler<AddMatchSegmentCommand, MatchSegmentDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public AddMatchSegmentCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<MatchSegmentDto> Handle(AddMatchSegmentCommand request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, request.ProgramId, request.MatchId, cancellationToken);
        var actor = _currentUser.Email ?? "unknown";
        match.TouchSetup(actor);

        var format = Enum.Parse<QuestionFormatCode>(request.FormatCode, ignoreCase: true);
        var template = await _db.StageSegmentTemplates
            .Where(t => t.StageId == match.StageId && t.FormatCode == format)
            .OrderBy(t => t.OrderIndex)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ValidationException(new Dictionary<string, string[]>
            {
                ["formatCode"] = [$"This match's stage has no {format} segment template to base the segment on."],
            });

        var segments = await MatchSetup.LoadSegmentsAsync(_db, match.Id, cancellationToken);
        var orderIndex = segments.Count == 0 ? 0 : segments.Max(s => s.OrderIndex) + 1;

        var segment = MatchSegment.Create(
            match.ProgramId, match.Id, template.Id, format, orderIndex, request.QuestionCount, actor);
        _db.MatchSegments.Add(segment);
        await _db.SaveChangesAsync(cancellationToken);

        return MatchSetup.ToDto(segment);
    }
}
