using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.QuestionBank.Dtos;
using QuizApp.Domain.Enums;
using QuizApp.Domain.QuestionBank;

namespace QuizApp.Application.QuestionBank.Commands;

public sealed record CreateRapidFireQuestionCommand(
    Guid ProgramId, string? QuestionText, byte DifficultyLevelId, Guid? TopicId, string Language,
    int? TimeLimitSeconds, string? Source, IReadOnlyList<Guid> TagIds, bool IsHostRead, string? AnswerText,
    Guid? ReplacesQuestionId = null) : IRequest<QuestionDto>;

public sealed class CreateRapidFireQuestionCommandValidator : AbstractValidator<CreateRapidFireQuestionCommand>
{
    public CreateRapidFireQuestionCommandValidator()
    {
        RuleFor(x => x.ProgramId).NotEmpty();
        RuleFor(x => x.DifficultyLevelId).InclusiveBetween((byte)1, (byte)5);
        RuleFor(x => x.QuestionText).NotEmpty().MaximumLength(4000).Unless(x => x.IsHostRead);
        RuleFor(x => x.AnswerText).NotEmpty().Unless(x => x.IsHostRead);
    }
}

public sealed class CreateRapidFireQuestionCommandHandler : IRequestHandler<CreateRapidFireQuestionCommand, QuestionDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public CreateRapidFireQuestionCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<QuestionDto> Handle(CreateRapidFireQuestionCommand request, CancellationToken cancellationToken)
    {
        if (!await _db.Programs.AnyAsync(p => p.Id == request.ProgramId, cancellationToken))
        {
            throw new KeyNotFoundException($"Program '{request.ProgramId}' was not found.");
        }

        await QuestionCommon.EnsureTopicAndTagsExistAsync(_db, request.ProgramId, request.TopicId, request.TagIds, cancellationToken);
        var previousVersion = await QuestionCommon.ResolveReplacementTargetAsync(
            _db, request.ProgramId, request.ReplacesQuestionId, QuestionFormatCode.RapidFire, cancellationToken);

        var actor = _currentUser.Email ?? "unknown";
        var difficulty = (DifficultyLevel)request.DifficultyLevelId;

        var question = request.IsHostRead
            ? RapidFireQuestion.CreateHostRead(request.ProgramId, QuestionOwnerScope.Program, difficulty, request.Language, actor, request.TopicId)
            : RapidFireQuestion.CreateStored(
                request.ProgramId, QuestionOwnerScope.Program, request.QuestionText!, request.AnswerText!, difficulty, request.Language, actor, request.TopicId);
        QuestionCommon.ApplyVersioning(question, previousVersion, actor);

        _db.Questions.Add(question);
        await _db.SaveChangesAsync(cancellationToken);

        return await QuestionMapper.ToDtoAsync(_db, question, cancellationToken);
    }
}
