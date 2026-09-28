using Quizware.Application.Abstractions;
using Quizware.Domain.Enums;

namespace Quizware.Application.Gameplay.Formats;

/// <summary>Played from the Choice round's topic board (see TopicPicks).</summary>
public sealed class ChoiceFormatHandler : OptionFormatHandler
{
    public ChoiceFormatHandler(IAppDbContext db)
        : base(db)
    {
    }

    public override QuestionFormatCode Format => QuestionFormatCode.Choice;
}
