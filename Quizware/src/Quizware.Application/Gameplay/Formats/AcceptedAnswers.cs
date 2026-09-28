using System.Text.Json;

namespace Quizware.Application.Gameplay.Formats;

/// <summary>Matching a typed answer against the answer and its accepted
/// alternatives — case, surrounding space and repeated spaces ignored. A match
/// marks the answer correct; a miss proves nothing (spelling, accents and
/// "close enough" calls stay with the operator).</summary>
internal static class AcceptedAnswers
{
    public static bool Matches(string freeText, string? answer, string? acceptableAnswersJson)
    {
        var accepted = new List<string>();
        if (answer is not null)
        {
            accepted.Add(answer);
        }

        if (!string.IsNullOrWhiteSpace(acceptableAnswersJson))
        {
            accepted.AddRange(JsonSerializer.Deserialize<List<string>>(acceptableAnswersJson) ?? []);
        }

        var given = Normalize(freeText);
        return accepted.Any(a => Normalize(a) == given);
    }

    public static ResponseEvaluation Evaluate(string? freeText, string? answer, string? acceptableAnswersJson) =>
        freeText is not null && Matches(freeText, answer, acceptableAnswersJson)
            ? new ResponseEvaluation(true, false)
            : ResponseEvaluation.NotChecked;

    private static string Normalize(string text) =>
        string.Join(' ', text.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
