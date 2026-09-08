using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.QuestionBank.Dtos;
using Quizware.Domain.Enums;
using Quizware.Domain.QuestionBank;

namespace Quizware.Application.QuestionBank.Commands;

public sealed record CreateCardQuestionCommand(
    Guid ProgramId, string? QuestionText, byte DifficultyLevelId, Guid? TopicId, string Language,
    int? TimeLimitSeconds, string? Source, IReadOnlyList<Guid> TagIds, IReadOnlyList<McqOptionInput> Options,
    Guid? ReplacesQuestionId = null) : IRequest<QuestionDto>;

public sealed class CreateCardQuestionCommandValidator : AbstractValidator<CreateCardQuestionCommand>
{
    public CreateCardQuestionCommandValidator()
    {
        RuleFor(x => x.ProgramId).NotEmpty();
        RuleFor(x => x.QuestionText).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.DifficultyLevelId).InclusiveBetween((byte)1, (byte)5);
        RuleFor(x => x.Options).Must(o => o.Count is >= 2 and <= 8).WithMessage("Between 2 and 8 options are required.");
    }
}

public sealed class CreateCardQuestionCommandHandler : IRequestHandler<CreateCardQuestionCommand, QuestionDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public CreateCardQuestionCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<QuestionDto> Handle(CreateCardQuestionCommand request, CancellationToken cancellationToken)
    {
        if (!await _db.Programs.AnyAsync(p => p.Id == request.ProgramId, cancellationToken))
        {
            throw new KeyNotFoundException($"Program '{request.ProgramId}' was not found.");
        }

        await QuestionCommon.EnsureTopicAndTagsExistAsync(_db, request.ProgramId, request.TopicId, request.TagIds, cancellationToken);
        var previousVersion = await QuestionCommon.ResolveReplacementTargetAsync(
            _db, request.ProgramId, request.ReplacesQuestionId, QuestionFormatCode.Card, cancellationToken);

        var actor = _currentUser.Email ?? "unknown";
        var question = CardQuestion.Create(
            request.ProgramId, QuestionOwnerScope.Program, request.QuestionText!,
            (DifficultyLevel)request.DifficultyLevelId, request.Language, actor, request.TopicId);
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
