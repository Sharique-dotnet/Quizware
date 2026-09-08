using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.QuestionBank.Dtos;
using Quizware.Domain.Enums;
using Quizware.Domain.QuestionBank;

namespace Quizware.Application.QuestionBank.Commands;

public sealed record SequenceItemInput(string? Text, Guid? MediaAssetId, int CorrectPosition, int DisplayOrder);

public sealed record CreateSequenceQuestionCommand(
    Guid ProgramId, string? QuestionText, byte DifficultyLevelId, Guid? TopicId, string Language,
    int? TimeLimitSeconds, string? Source, IReadOnlyList<Guid> TagIds, IReadOnlyList<SequenceItemInput> Items,
    bool PartialCreditEnabled, int? PointsPerCorrectPosition, SequenceItemKind ItemKind, Guid? ReplacesQuestionId = null)
    : IRequest<QuestionDto>;

public sealed class CreateSequenceQuestionCommandValidator : AbstractValidator<CreateSequenceQuestionCommand>
{
    public CreateSequenceQuestionCommandValidator()
    {
        RuleFor(x => x.ProgramId).NotEmpty();
        RuleFor(x => x.DifficultyLevelId).InclusiveBetween((byte)1, (byte)5);
        RuleFor(x => x.Items).Must(i => i.Count >= 2).WithMessage("A sequence needs at least 2 items.");
        RuleFor(x => x.Items)
            .Must(items => items.Select(i => i.CorrectPosition).OrderBy(p => p).SequenceEqual(Enumerable.Range(1, items.Count)))
            .WithMessage("Correct positions must be 1..N with no gaps or duplicates.");
        RuleFor(x => x.PointsPerCorrectPosition).NotNull().When(x => x.PartialCreditEnabled);
    }
}

public sealed class CreateSequenceQuestionCommandHandler : IRequestHandler<CreateSequenceQuestionCommand, QuestionDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public CreateSequenceQuestionCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<QuestionDto> Handle(CreateSequenceQuestionCommand request, CancellationToken cancellationToken)
    {
        if (!await _db.Programs.AnyAsync(p => p.Id == request.ProgramId, cancellationToken))
        {
            throw new KeyNotFoundException($"Program '{request.ProgramId}' was not found.");
        }

        await QuestionCommon.EnsureTopicAndTagsExistAsync(_db, request.ProgramId, request.TopicId, request.TagIds, cancellationToken);
        var previousVersion = await QuestionCommon.ResolveReplacementTargetAsync(
            _db, request.ProgramId, request.ReplacesQuestionId, QuestionFormatCode.Sequence, cancellationToken);

        var actor = _currentUser.Email ?? "unknown";
        var question = SequenceQuestion.Create(
            request.ProgramId, QuestionOwnerScope.Program, request.QuestionText!, request.Items.Count,
            (DifficultyLevel)request.DifficultyLevelId, request.Language, actor, request.TopicId,
            request.PartialCreditEnabled, request.PointsPerCorrectPosition, request.ItemKind);
        QuestionCommon.ApplyVersioning(question, previousVersion, actor);

        _db.Questions.Add(question);
        foreach (var item in request.Items)
        {
            _db.SequenceItems.Add(SequenceItem.Create(question.Id, item.CorrectPosition, item.DisplayOrder, actor, item.Text, item.MediaAssetId));
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await QuestionMapper.ToDtoAsync(_db, question, cancellationToken);
    }
}
