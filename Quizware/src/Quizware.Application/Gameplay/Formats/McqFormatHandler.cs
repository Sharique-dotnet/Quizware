using Quizware.Application.Abstractions;
using Quizware.Domain.Enums;
using Quizware.Domain.QuestionBank;

namespace Quizware.Application.Gameplay.Formats;

/// <summary>Honours the question's own ShuffleOptions: with it off, options
/// appear in the authored order rather than the per-match shuffle.</summary>
public sealed class McqFormatHandler : OptionFormatHandler
{
    public McqFormatHandler(IAppDbContext db)
        : base(db)
    {
    }

    public override QuestionFormatCode Format => QuestionFormatCode.Mcq;

    protected override bool ShuffleOptions(Question question) => question is not McqQuestion { ShuffleOptions: false };
}
