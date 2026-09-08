using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.QuestionBank.Dtos;
using QuizApp.Domain.Enums;
using QuizApp.Domain.QuestionBank;

namespace QuizApp.Application.QuestionBank.Commands;

public sealed record McqOptionInput(string Text, bool IsCorrect, int DisplayOrder, Guid? MediaAssetId);

public sealed record CreateMcqQuestionCommand(
    Guid ProgramId,
    string? QuestionText,
    byte DifficultyLevelId,
    Guid? TopicId,
    string Language,
    int? TimeLimitSeconds,
    string? Source,
    IReadOnlyList<Guid> TagIds,
    IReadOnlyList<McqOptionInput> Options,
    bool AllowMultipleCorrect,
    bool ShuffleOptions,
    bool NegativeMarkingEnabled,
    Guid? ReplacesQuestionId = null) : IRequest<QuestionDto>;

public sealed class CreateMcqQuestionCommandValidator : AbstractValidator<CreateMcqQuestionCommand>
{
    public CreateMcqQuestionCommandValidator()
    {
        RuleFor(x => x.ProgramId).NotEmpty();
        RuleFor(x => x.QuestionText).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.DifficultyLevelId).InclusiveBetween((byte)1, (byte)5);
        RuleFor(x => x.Options).Must(o => o.Count is >= 2 and <= 8).WithMessage("Between 2 and 8 options are required.");
    }
}

/// <summary>P6-16: base row + format row written in one transaction — the
/// EF change tracker + one SaveChangesAsync call already gives us that
/// (no explicit transaction needed for a single SaveChanges).</summary>
public sealed class CreateMcqQuestionCommandHandler : IRequestHandler<CreateMcqQuestionCommand, QuestionDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public CreateMcqQuestionCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<QuestionDto> Handle(CreateMcqQuestionCommand request, CancellationToken cancellationToken)
    {
        if (!await _db.Programs.AnyAsync(p => p.Id == request.ProgramId, cancellationToken))
        {
            throw new KeyNotFoundException($"Program '{request.ProgramId}' was not found.");
        }

        await QuestionCommon.EnsureTopicAndTagsExistAsync(_db, request.ProgramId, request.TopicId, request.TagIds, cancellationToken);
        var previousVersion = await QuestionCommon.ResolveReplacementTargetAsync(
            _db, request.ProgramId, request.ReplacesQuestionId, QuestionFormatCode.Mcq, cancellationToken);

        var actor = _currentUser.Email ?? "unknown";
        var mcq = McqQuestion.Create(
            request.ProgramId, QuestionOwnerScope.Program, request.QuestionText!,
            (DifficultyLevel)request.DifficultyLevelId, request.Language, actor, request.TopicId,
            request.AllowMultipleCorrect, request.ShuffleOptions, request.NegativeMarkingEnabled);
        QuestionCommon.ApplyVersioning(mcq, previousVersion, actor);

        _db.Questions.Add(mcq);
        foreach (var option in request.Options)
        {
            _db.QuestionOptions.Add(QuestionOption.Create(mcq.Id, option.Text, option.IsCorrect, option.DisplayOrder, actor, option.MediaAssetId));
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await QuestionMapper.ToDtoAsync(_db, mcq, cancellationToken);
    }
}
