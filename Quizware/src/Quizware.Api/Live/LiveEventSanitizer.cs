using System.Text.Json;
using System.Text.Json.Nodes;
using Quizware.Application.Gameplay;

namespace Quizware.Api.Live;

/// <summary>P12-04: a second line of defence for displays. Event details are
/// written without answers wherever possible, and the state snapshot is built
/// for the Display audience; this strips any answer-bearing field that slips
/// into an event's details anyway — except on QuestionRevealed, whose whole
/// point is the answer.</summary>
public static class LiveEventSanitizer
{
    private static readonly HashSet<string> AnswerFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "correctOptionId", "correctOptionIds", "correctAnswer", "answerText", "acceptedAnswers", "acceptableAnswers",
        "correctPosition", "correctOrder",
    };

    public static JsonElement ForDisplay(string eventType, JsonElement data)
    {
        if (eventType == LiveEvents.QuestionRevealed)
        {
            return data;
        }

        var node = JsonNode.Parse(data.GetRawText());
        Strip(node);
        return node is null ? data : JsonSerializer.SerializeToElement(node);
    }

    private static void Strip(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var name in obj.Select(p => p.Key).Where(AnswerFields.Contains).ToList())
                {
                    obj.Remove(name);
                }

                foreach (var (_, child) in obj)
                {
                    Strip(child);
                }

                break;
            case JsonArray array:
                foreach (var child in array)
                {
                    Strip(child);
                }

                break;
        }
    }
}
