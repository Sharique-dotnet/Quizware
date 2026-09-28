using Quizware.Application.Abstractions;
using Quizware.Domain.Enums;

namespace Quizware.Application.Gameplay.Formats;

/// <summary>Each option is a card face.</summary>
public sealed class CardFormatHandler : OptionFormatHandler
{
    public CardFormatHandler(IAppDbContext db)
        : base(db)
    {
    }

    public override QuestionFormatCode Format => QuestionFormatCode.Card;
}
