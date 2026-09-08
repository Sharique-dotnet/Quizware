using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Domain.QuestionBank;

namespace Quizware.Application.QuestionBank.Dtos;

/// <summary>Builds the full polymorphic DTO for one question. Querying
/// IAppDbContext.Questions materializes the actual derived TPT type
/// already (EF Core's normal polymorphic-query behaviour), so this just
/// pattern-matches on the runtime type rather than re-querying.</summary>
internal static class QuestionMapper
{
    public static async Task<QuestionDto> ToDtoAsync(IAppDbContext db, Question question, CancellationToken cancellationToken)
    {
        var topicName = question.TopicId is Guid topicId
            ? await db.Topics.Where(t => t.Id == topicId).Select(t => t.Name).SingleOrDefaultAsync(cancellationToken)
            : null;

        switch (question)
        {
            case McqQuestion mcq:
            {
                var (options, correctIds) = await LoadOptionsAsync(db, mcq.Id, cancellationToken);
                return new McqQuestionDto
                {
                    Id = mcq.Id,
                    FormatCode = mcq.FormatCode.ToString(),
                    DifficultyLevel = (byte)mcq.DifficultyLevel,
                    TopicName = topicName,
                    QuestionText = mcq.QuestionText,
                    Status = mcq.Status.ToString(),
                    SupersedesQuestionId = mcq.SupersedesQuestionId,
                    Options = options,
                    CorrectOptionIds = correctIds,
                    AllowMultipleCorrect = mcq.AllowMultipleCorrect,
                };
            }

            case BuzzerQuestion buzzer:
            {
                var (options, correctIds) = await LoadOptionsAsync(db, buzzer.Id, cancellationToken);
                return new BuzzerQuestionDto
                {
                    Id = buzzer.Id,
                    FormatCode = buzzer.FormatCode.ToString(),
                    DifficultyLevel = (byte)buzzer.DifficultyLevel,
                    TopicName = topicName,
                    QuestionText = buzzer.QuestionText,
                    Status = buzzer.Status.ToString(),
                    SupersedesQuestionId = buzzer.SupersedesQuestionId,
                    Options = options,
                    CorrectOptionIds = correctIds,
                    BuzzWindowSeconds = buzzer.BuzzWindowSeconds,
                };
            }

            case PassingQuestion passing:
            {
                var (options, correctIds) = await LoadOptionsAsync(db, passing.Id, cancellationToken);
                return new PassingQuestionDto
                {
                    Id = passing.Id,
                    FormatCode = passing.FormatCode.ToString(),
                    DifficultyLevel = (byte)passing.DifficultyLevel,
                    TopicName = topicName,
                    QuestionText = passing.QuestionText,
                    Status = passing.Status.ToString(),
                    SupersedesQuestionId = passing.SupersedesQuestionId,
                    Options = options,
                    CorrectOptionIds = correctIds,
                    MaxPassCount = passing.MaxPassCount,
                };
            }

            case CardQuestion card:
            {
                var (options, correctIds) = await LoadOptionsAsync(db, card.Id, cancellationToken);
                return new CardQuestionDto
                {
                    Id = card.Id,
                    FormatCode = card.FormatCode.ToString(),
                    DifficultyLevel = (byte)card.DifficultyLevel,
                    TopicName = topicName,
                    QuestionText = card.QuestionText,
                    Status = card.Status.ToString(),
                    SupersedesQuestionId = card.SupersedesQuestionId,
                    Options = options,
                    CorrectOptionIds = correctIds,
                };
            }

            case ChoiceQuestion choice:
            {
                var (options, correctIds) = await LoadOptionsAsync(db, choice.Id, cancellationToken);
                return new ChoiceQuestionDto
                {
                    Id = choice.Id,
                    FormatCode = choice.FormatCode.ToString(),
                    DifficultyLevel = (byte)choice.DifficultyLevel,
                    TopicName = topicName,
                    QuestionText = choice.QuestionText,
                    Status = choice.Status.ToString(),
                    SupersedesQuestionId = choice.SupersedesQuestionId,
                    Options = options,
                    CorrectOptionIds = correctIds,
                    TopicLabel = choice.TopicLabel,
                    TopicDisplayOrder = choice.TopicDisplayOrder,
                };
            }

            case RapidFireQuestion rapidFire:
                return new RapidFireQuestionDto
                {
                    Id = rapidFire.Id,
                    FormatCode = rapidFire.FormatCode.ToString(),
                    DifficultyLevel = (byte)rapidFire.DifficultyLevel,
                    TopicName = topicName,
                    QuestionText = rapidFire.QuestionText,
                    Status = rapidFire.Status.ToString(),
                    SupersedesQuestionId = rapidFire.SupersedesQuestionId,
                    AnswerText = rapidFire.AnswerText,
                    IsHostRead = rapidFire.IsHostRead,
                };

            case TieBreakerQuestion tieBreaker:
            {
                var (options, correctIds) = await LoadOptionsAsync(db, tieBreaker.Id, cancellationToken);
                return new TieBreakerQuestionDto
                {
                    Id = tieBreaker.Id,
                    FormatCode = tieBreaker.FormatCode.ToString(),
                    DifficultyLevel = (byte)tieBreaker.DifficultyLevel,
                    TopicName = topicName,
                    QuestionText = tieBreaker.QuestionText,
                    Status = tieBreaker.Status.ToString(),
                    SupersedesQuestionId = tieBreaker.SupersedesQuestionId,
                    AnswerMode = tieBreaker.AnswerMode.ToString(),
                    Options = options,
                    CorrectOptionIds = correctIds,
                    AnswerText = tieBreaker.AnswerText,
                    NumericAnswer = tieBreaker.NumericAnswer,
                };
            }

            case SequenceQuestion sequence:
            {
                var items = await db.SequenceItems
                    .Where(i => i.QuestionId == sequence.Id)
                    .OrderBy(i => i.DisplayOrder)
                    .Select(i => new SequenceItemDto(i.Id, i.ItemText, i.MediaAssetId, i.CorrectPosition, i.DisplayOrder))
                    .ToListAsync(cancellationToken);

                return new SequenceQuestionDto
                {
                    Id = sequence.Id,
                    FormatCode = sequence.FormatCode.ToString(),
                    DifficultyLevel = (byte)sequence.DifficultyLevel,
                    TopicName = topicName,
                    QuestionText = sequence.QuestionText,
                    Status = sequence.Status.ToString(),
                    SupersedesQuestionId = sequence.SupersedesQuestionId,
                    Items = items,
                    PartialCreditEnabled = sequence.PartialCreditEnabled,
                };
            }

            case AudioVisualQuestion audioVisual:
                return new AudioVisualQuestionDto
                {
                    Id = audioVisual.Id,
                    FormatCode = audioVisual.FormatCode.ToString(),
                    DifficultyLevel = (byte)audioVisual.DifficultyLevel,
                    TopicName = topicName,
                    QuestionText = audioVisual.QuestionText,
                    Status = audioVisual.Status.ToString(),
                    SupersedesQuestionId = audioVisual.SupersedesQuestionId,
                    MediaAssetId = audioVisual.MediaAssetId,
                    MediaKind = audioVisual.MediaKind.ToString(),
                    AnswerText = audioVisual.AnswerText,
                    PlaybackStartSeconds = audioVisual.PlaybackStartSeconds,
                    PlaybackDurationSeconds = audioVisual.PlaybackDurationSeconds,
                    ReplayAllowed = audioVisual.ReplayAllowed,
                };

            case VisualRapidFireQuestion visualRapidFire:
            {
                var items = await db.VisualRapidFireItems
                    .Where(i => i.QuestionId == visualRapidFire.Id)
                    .OrderBy(i => i.DisplayOrder)
                    .Select(i => new VrfItemDto(i.Id, i.MediaAssetId, i.AnswerText, i.DisplayOrder))
                    .ToListAsync(cancellationToken);

                return new VisualRapidFireQuestionDto
                {
                    Id = visualRapidFire.Id,
                    FormatCode = visualRapidFire.FormatCode.ToString(),
                    DifficultyLevel = (byte)visualRapidFire.DifficultyLevel,
                    TopicName = topicName,
                    QuestionText = visualRapidFire.QuestionText,
                    Status = visualRapidFire.Status.ToString(),
                    SupersedesQuestionId = visualRapidFire.SupersedesQuestionId,
                    Items = items,
                };
            }

            default:
                throw new NotSupportedException($"Unknown question format: {question.GetType().Name}");
        }
    }

    public static QuestionSummaryDto ToSummaryDto(Question question, string? topicName) => new(
        question.Id, question.FormatCode.ToString(), (byte)question.DifficultyLevel, topicName, question.Status.ToString(), question.QuestionText);

    private static async Task<(IReadOnlyList<QuestionOptionDto> Options, IReadOnlyList<Guid> CorrectIds)> LoadOptionsAsync(
        IAppDbContext db, Guid questionId, CancellationToken cancellationToken)
    {
        var options = await db.QuestionOptions
            .Where(o => o.QuestionId == questionId)
            .OrderBy(o => o.DisplayOrder)
            .ToListAsync(cancellationToken);

        var dtos = options.Select(o => new QuestionOptionDto(o.Id, o.OptionText, o.DisplayOrder, o.MediaAssetId)).ToList();
        var correctIds = options.Where(o => o.IsCorrect).Select(o => o.Id).ToList();
        return (dtos, correctIds);
    }
}
