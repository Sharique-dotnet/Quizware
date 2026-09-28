using Quizware.Application.Abstractions;
using Quizware.Domain.Enums;

namespace Quizware.Application.Gameplay.Formats;

/// <summary>The holder may pass the question on (see PassQuestion).</summary>
public sealed class PassingFormatHandler : OptionFormatHandler
{
    public PassingFormatHandler(IAppDbContext db)
        : base(db)
    {
    }

    public override QuestionFormatCode Format => QuestionFormatCode.Passing;
}
