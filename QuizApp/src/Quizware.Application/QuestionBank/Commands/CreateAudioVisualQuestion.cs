using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.QuestionBank.Dtos;
using Quizware.Domain.Enums;
using Quizware.Domain.QuestionBank;

namespace Quizware.Application.QuestionBank.Commands;

public sealed record CreateAudioVisualQuestionCommand(
    Guid ProgramId, string? QuestionText, byte DifficultyLevelId, Guid? TopicId, string Language,
    int? TimeLimitSeconds, string? Source, IReadOnlyList<Guid> TagIds, Guid MediaAssetId, MediaKind MediaKind,
    string AnswerText, IReadOnlyList<string> AcceptableAnswers, int? PlaybackStartSeconds, int? PlaybackDurationSeconds,
    bool AutoPlay, bool ReplayAllowed, Guid? RevealMediaAssetId, Guid? ReplacesQuestionId = null) : IRequest<QuestionDto>;

public sealed class CreateAudioVisualQuestionCommandValidator : AbstractValidator<CreateAudioVisualQuestionCommand>
{
    public CreateAudioVisualQuestionCommandValidator()
    {
        RuleFor(x => x.ProgramId).NotEmpty();
        RuleFor(x => x.DifficultyLevelId).InclusiveBetween((byte)1, (byte)5);
        RuleFor(x => x.MediaAssetId).NotEmpty();
        RuleFor(x => x.AnswerText).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.PlaybackDurationSeconds).GreaterThan(0).When(x => x.PlaybackDurationSeconds.HasValue);
    }
}

public sealed class CreateAudioVisualQuestionCommandHandler : IRequestHandler<CreateAudioVisualQuestionCommand, QuestionDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public CreateAudioVisualQuestionCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<QuestionDto> Handle(CreateAudioVisualQuestionCommand request, CancellationToken cancellationToken)
    {
        if (!await _db.Programs.AnyAsync(p => p.Id == request.ProgramId, cancellationToken))
        {
            throw new KeyNotFoundException($"Program '{request.ProgramId}' was not found.");
        }

        await QuestionCommon.EnsureTopicAndTagsExistAsync(_db, request.ProgramId, request.TopicId, request.TagIds, cancellationToken);

        if (!await _db.MediaAssets.AnyAsync(m => m.Id == request.MediaAssetId, cancellationToken))
        {
            throw new KeyNotFoundException($"Media asset '{request.MediaAssetId}' was not found.");
        }

        var previousVersion = await QuestionCommon.ResolveReplacementTargetAsync(
            _db, request.ProgramId, request.ReplacesQuestionId, QuestionFormatCode.AudioVisual, cancellationToken);

        var actor = _currentUser.Email ?? "unknown";
        var acceptableAnswersJson = request.AcceptableAnswers.Count > 0 ? JsonSerializer.Serialize(request.AcceptableAnswers) : null;

        var question = AudioVisualQuestion.Create(
            request.ProgramId, QuestionOwnerScope.Program, request.QuestionText, request.MediaAssetId, request.MediaKind,
            request.AnswerText, (DifficultyLevel)request.DifficultyLevelId, request.Language, actor, request.TopicId,
            acceptableAnswersJson, request.PlaybackStartSeconds, request.PlaybackDurationSeconds, request.AutoPlay,
            request.ReplayAllowed, request.RevealMediaAssetId);
        QuestionCommon.ApplyVersioning(question, previousVersion, actor);

        _db.Questions.Add(question);
        await _db.SaveChangesAsync(cancellationToken);

        return await QuestionMapper.ToDtoAsync(_db, question, cancellationToken);
    }
}
