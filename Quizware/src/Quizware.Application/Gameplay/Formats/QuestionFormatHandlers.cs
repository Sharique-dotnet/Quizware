using Quizware.Domain.Enums;

namespace Quizware.Application.Gameplay.Formats;

/// <summary>Looks up the handler for a format. A format with no handler is a
/// configuration error the engine refuses to guess around.</summary>
public sealed class QuestionFormatHandlers
{
    private readonly IReadOnlyDictionary<QuestionFormatCode, IQuestionFormatHandler> _byFormat;

    public QuestionFormatHandlers(IEnumerable<IQuestionFormatHandler> handlers)
    {
        _byFormat = handlers.ToDictionary(h => h.Format);
    }

    public IQuestionFormatHandler For(QuestionFormatCode format) =>
        _byFormat.TryGetValue(format, out var handler)
            ? handler
            : throw new InvalidOperationException($"No live handler is registered for the {format} format.");

    public static string MediaUrl(Guid mediaAssetId) => $"/api/v1/media/{mediaAssetId}";
}
