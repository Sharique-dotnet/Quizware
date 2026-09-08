using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.QuestionBank.Dtos;
using Quizware.Domain.Enums;
using Quizware.Domain.QuestionBank;

namespace Quizware.Application.QuestionBank.Commands;

public sealed record CreateChoiceQuestionCommand(
    Guid ProgramId, string? QuestionText, byte DifficultyLevelId, Guid? TopicId, string Language,
    int? TimeLimitSeconds, string? Source, IReadOnlyList<Guid> TagIds, IReadOnlyList<McqOptionInput> Options,
    string TopicLabel, bool IsExclusiveTopic, Guid? ReplacesQuestionId = null) : IRequest<QuestionDto>;

public sealed class CreateChoiceQuestionCommandValidator : AbstractValidator<CreateChoiceQuestionCommand>
{
    public CreateChoiceQuestionCommandValidator()
    {
        RuleFor(x => x.ProgramId).NotEmpty();
        RuleFor(x => x.QuestionText).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.DifficultyLevelId).InclusiveBetween((byte)1, (byte)5);
        RuleFor(x => x.Options).Must(o => o.Count is >= 2 and <= 8).WithMessage("Between 2 and 8 options are required.");
        RuleFor(x => x.TopicLabel).NotEmpty().MaximumLength(150);
    }
}

public sealed class CreateChoiceQuestionCommandHandler : IRequestHandler<CreateChoiceQuestionCommand, QuestionDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public CreateChoiceQuestionCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<QuestionDto> Handle(CreateChoiceQuestionCommand request, CancellationToken cancellationToken)
    {
        if (!await _db.Programs.AnyAsync(p => p.Id == request.ProgramId, cancellationToken))
        {
            throw new KeyNotFoundException($"Program '{request.ProgramId}' was not found.");
        }

        await QuestionCommon.EnsureTopicAndTagsExistAsync(_db, request.ProgramId, request.TopicId, request.TagIds, cancellationToken);
        var previousVersion = await QuestionCommon.ResolveReplacementTargetAsync(
            _db, request.ProgramId, request.ReplacesQuestionId, QuestionFormatCode.Choice, cancellationToken);

        var actor = _currentUser.Email ?? "unknown";
        var question = ChoiceQuestion.Create(
            request.ProgramId, QuestionOwnerScope.Program, request.QuestionText!, request.TopicLabel,
            (DifficultyLevel)request.DifficultyLevelId, request.Language, actor, request.TopicId,
            request.IsExclusiveTopic);
        QuestionCommon.ApplyVersioning(question, previousVersion, actor);

        _db.Questions.Add(question);
        foreach (var option in request.Options)
        {
            _db.QuestionOptions.Add(QuestionOption.Create(question.Id, option.Text, option.IsCorrect, option.DisplayOrder, actor, option.MediaAssetId));
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await QuestionMapper.ToDtoAsync(_db, question, cancellationToken);
    }
}
