using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.QuestionBank.Dtos;
using Quizware.Domain.Enums;
using Quizware.Domain.QuestionBank;

namespace Quizware.Application.QuestionBank.Commands;

public sealed record VrfItemInput(Guid MediaAssetId, string AnswerText, IReadOnlyList<string> AcceptableAnswers, int DisplayOrder);

public sealed record CreateVisualRapidFireQuestionCommand(
    Guid ProgramId, byte DifficultyLevelId, Guid? TopicId, string Language, int? TimeLimitSeconds, string? Source,
    IReadOnlyList<Guid> TagIds, IReadOnlyList<VrfItemInput> Items, int? RevealSecondsPerImage, int? GridColumns,
    bool ScorePerImage, Guid? ReplacesQuestionId = null) : IRequest<QuestionDto>;

public sealed class CreateVisualRapidFireQuestionCommandValidator : AbstractValidator<CreateVisualRapidFireQuestionCommand>
{
    public CreateVisualRapidFireQuestionCommandValidator()
    {
        RuleFor(x => x.ProgramId).NotEmpty();
        RuleFor(x => x.DifficultyLevelId).InclusiveBetween((byte)1, (byte)5);
        RuleFor(x => x.Items).Must(i => i.Count >= 2).WithMessage("Visual rapid fire needs at least 2 images.");
        RuleForEach(x => x.Items).ChildRules(i => i.RuleFor(p => p.AnswerText).NotEmpty());
    }
}

public sealed class CreateVisualRapidFireQuestionCommandHandler : IRequestHandler<CreateVisualRapidFireQuestionCommand, QuestionDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public CreateVisualRapidFireQuestionCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<QuestionDto> Handle(CreateVisualRapidFireQuestionCommand request, CancellationToken cancellationToken)
    {
        if (!await _db.Programs.AnyAsync(p => p.Id == request.ProgramId, cancellationToken))
        {
            throw new KeyNotFoundException($"Program '{request.ProgramId}' was not found.");
        }

        await QuestionCommon.EnsureTopicAndTagsExistAsync(_db, request.ProgramId, request.TopicId, request.TagIds, cancellationToken);

        var mediaIds = request.Items.Select(i => i.MediaAssetId).Distinct().ToList();
        var visibleMediaCount = await _db.MediaAssets.CountAsync(m => mediaIds.Contains(m.Id), cancellationToken);
        if (visibleMediaCount != mediaIds.Count)
        {
            throw new KeyNotFoundException("One or more media assets were not found.");
        }

        var previousVersion = await QuestionCommon.ResolveReplacementTargetAsync(
            _db, request.ProgramId, request.ReplacesQuestionId, QuestionFormatCode.VisualRapidFire, cancellationToken);

        var actor = _currentUser.Email ?? "unknown";
        var question = VisualRapidFireQuestion.Create(
            request.ProgramId, QuestionOwnerScope.Program, request.Items.Count,
            (DifficultyLevel)request.DifficultyLevelId, request.Language, actor, request.TopicId,
            request.RevealSecondsPerImage, request.GridColumns, request.ScorePerImage);
        QuestionCommon.ApplyVersioning(question, previousVersion, actor);

        _db.Questions.Add(question);
        foreach (var item in request.Items)
        {
            var vrfItem = VisualRapidFireItem.Create(question.Id, item.MediaAssetId, item.AnswerText, item.DisplayOrder, actor);
            _db.VisualRapidFireItems.Add(vrfItem);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await QuestionMapper.ToDtoAsync(_db, question, cancellationToken);
    }
}
