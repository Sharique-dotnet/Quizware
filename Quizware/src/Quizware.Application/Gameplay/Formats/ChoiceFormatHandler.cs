using Quizware.Application.Abstractions;
using Quizware.Domain.Enums;
using Quizware.Domain.QuestionBank;

namespace Quizware.Application.Gameplay.Formats;

/// <summary>P9-09: the Choice round's board shows each question's TopicLabel
/// at its TopicDisplayOrder; an exclusive topic is gone once a team picks it.</summary>
public sealed class ChoiceFormatHandler : OptionFormatHandler
{
    public ChoiceFormatHandler(IAppDbContext db)
        : base(db)
    {
    }

    public override QuestionFormatCode Format => QuestionFormatCode.Choice;

    public override TopicChoice? TopicChoice(Question question) =>
        question is ChoiceQuestion choice ? new TopicChoice(choice.TopicLabel, choice.IsTopicExclusive, choice.TopicDisplayOrder) : null;
}
