using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.QuestionBank.Dtos;
using QuizApp.Domain.Enums;
using QuizApp.Domain.QuestionBank;

namespace QuizApp.Application.QuestionBank.Commands;

public sealed record CreateTieBreakerQuestionCommand(
    Guid ProgramId, string? QuestionText, byte DifficultyLevelId, Guid? TopicId, string Language,
    int? TimeLimitSeconds, string? Source, IReadOnlyList<Guid> TagIds, TieBreakAnswerMode AnswerMode,
    IReadOnlyList<McqOptionInput> Options, string? AnswerText, decimal? NumericAnswer, Guid? ReplacesQuestionId = null)
    : IRequest<QuestionDto>;

public sealed class CreateTieBreakerQuestionCommandValidator : AbstractValidator<CreateTieBreakerQuestionCommand>
{
    public CreateTieBreakerQuestionCommandValidator()
    {
        RuleFor(x => x.ProgramId).NotEmpty();
        RuleFor(x => x.QuestionText).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.DifficultyLevelId).InclusiveBetween((byte)1, (byte)5);
        RuleFor(x => x.Options).Must(o => o.Count is >= 2 and <= 8).WithMessage("Between 2 and 8 options are required.")
            .When(x => x.AnswerMode == TieBreakAnswerMode.Options);
        RuleFor(x => x.AnswerText).NotEmpty().When(x => x.AnswerMode == TieBreakAnswerMode.ExactText);
        RuleFor(x => x.NumericAnswer).NotNull().When(x => x.AnswerMode == TieBreakAnswerMode.NumericProximity);
    }
}

public sealed class CreateTieBreakerQuestionCommandHandler : IRequestHandler<CreateTieBreakerQuestionCommand, QuestionDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public CreateTieBreakerQuestionCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<QuestionDto> Handle(CreateTieBreakerQuestionCommand request, CancellationToken cancellationToken)
    {
        if (!await _db.Programs.AnyAsync(p => p.Id == request.ProgramId, cancellationToken))
        {
            throw new KeyNotFoundException($"Program '{request.ProgramId}' was not found.");
        }

        await QuestionCommon.EnsureTopicAndTagsExistAsync(_db, request.ProgramId, request.TopicId, request.TagIds, cancellationToken);
        var previousVersion = await QuestionCommon.ResolveReplacementTargetAsync(
            _db, request.ProgramId, request.ReplacesQuestionId, QuestionFormatCode.TieBreaker, cancellationToken);

        var actor = _currentUser.Email ?? "unknown";
        var difficulty = (DifficultyLevel)request.DifficultyLevelId;

        var question = request.AnswerMode switch
        {
            TieBreakAnswerMode.NumericProximity => TieBreakerQuestion.CreateNumericProximity(
                request.ProgramId, QuestionOwnerScope.Program, request.QuestionText!, request.NumericAnswer!.Value,
                difficulty, request.Language, actor, request.TopicId),
            TieBreakAnswerMode.ExactText => TieBreakerQuestion.CreateExactText(
                request.ProgramId, QuestionOwnerScope.Program, request.QuestionText!, request.AnswerText!,
                difficulty, request.Language, actor, request.TopicId),
            _ => TieBreakerQuestion.CreateWithOptions(
                request.ProgramId, QuestionOwnerScope.Program, request.QuestionText!, difficulty, request.Language, actor, request.TopicId),
        };
        QuestionCommon.ApplyVersioning(question, previousVersion, actor);

        _db.Questions.Add(question);
        if (request.AnswerMode == TieBreakAnswerMode.Options)
        {
            foreach (var option in request.Options)
            {
                _db.QuestionOptions.Add(QuestionOption.Create(question.Id, option.Text, option.IsCorrect, option.DisplayOrder, actor, option.MediaAssetId));
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await QuestionMapper.ToDtoAsync(_db, question, cancellationToken);
    }
}
