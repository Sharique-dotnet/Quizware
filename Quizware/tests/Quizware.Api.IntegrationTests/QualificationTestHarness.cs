using System.Net.Http.Json;
using Quizware.Api.Contracts.V1.Matches;
using Quizware.Api.Contracts.V1.Qualification;
using Quizware.Api.Contracts.V1.Rules;
using Quizware.Api.Contracts.V1.Stages;
using Quizware.Domain.Enums;
using Quizware.Domain.Qualification;

namespace Quizware.Api.IntegrationTests;

/// <summary>Phase 11: builds stages, plays real matches through the live
/// API with each team's answers scripted turn by turn, and calls the
/// qualification endpoints.</summary>
public sealed class QualificationTestHarness
{
    private int _nextMatchNumber = 1;

    private QualificationTestHarness(MatchTestHarness match)
    {
        Match = match;
    }

    public MatchTestHarness Match { get; }

    public HttpClient Client => Match.Client;

    public string QualificationUrl => $"/api/v1/programs/{Match.ProgramId}/qualification";

    public static async Task<QualificationTestHarness> CreateAsync(CustomWebApplicationFactory factory)
    {
        var match = await MatchTestHarness.CreateAsync(factory);
        await match.ResetScoringDefaultsAsync();
        return new QualificationTestHarness(match);
    }

    /// <summary>A stage whose matches each play one MCQ segment of <paramref name="questionsPerMatch"/> questions.</summary>
    public Task<StageDetailResponse> CreateStageAsync(int questionsPerMatch) => Match.CreateStageAsync(("Mcq", questionsPerMatch));

    public async Task SetRuleAsync(Guid stageId, int winnersPerMatch, int bestRemaining, int wildcardSlots = 0)
    {
        var response = await Client.PutAsJsonAsync(
            $"/api/v1/programs/{Match.ProgramId}/rules/qualification",
            new UpsertQualificationRulesRequest([new QualificationRuleDto(Guid.Empty, stageId, winnersPerMatch, bestRemaining, wildcardSlots)]));
        response.EnsureSuccessStatusCode();
    }

    /// <summary>A rule with the fields the API does not expose yet (seeding mode, score threshold).</summary>
    public Task SeedRuleAsync(
        Guid stageId, int winnersPerMatch, int bestRemaining, SeedingMode seedingMode, int wildcardSlots = 0, int? minScoreThreshold = null) =>
        Match.SeedAsync((db, programId) => db.QualificationRules.Add(QualificationRule.Create(
            programId, stageId, "test", winnersPerMatch: winnersPerMatch, bestRemainingAcrossStage: bestRemaining,
            manualWildcardSlots: wildcardSlots, minScoreThreshold: minScoreThreshold, seedingMode: seedingMode)));

    public async Task SetTieBreakRuleAsync(Guid stageId, bool scoreCountsTowardStage = false, int maxExtraRounds = 3, string onStillTied = "ManualDecision")
    {
        var response = await Client.PutAsJsonAsync(
            $"/api/v1/programs/{Match.ProgramId}/rules/tie-break",
            new UpsertTieBreakRulesRequest([new TieBreakRuleDto(
                Guid.Empty, stageId, ["TotalScore", "FewerIncorrect", "HeadToHead"], "Mcq", 2, false, maxExtraRounds, scoreCountsTowardStage, onStillTied)]));
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Creates and plays a whole match. Each team answers its turns
    /// in order from its script: C = correct, I = incorrect, S = skipped (no
    /// answer at all). Seats follow the order of <paramref name="scripts"/>.</summary>
    public async Task<MatchDetailResponse> PlayMatchAsync(Guid stageId, params (Guid TeamId, string Script)[] scripts)
    {
        var turns = scripts.Sum(s => s.Script.Length);
        await Match.SeedMcqAsync(turns);
        var match = await Match.CreateMatchAsync(stageId, _nextMatchNumber++, scripts.Select(s => s.TeamId).ToList());
        await PlayAsync(match, scripts);
        return match;
    }

    /// <summary>Starts and plays an existing match (e.g. a tie-break), then ends it.</summary>
    public async Task PlayAsync(MatchDetailResponse match, params (Guid TeamId, string Script)[] scripts)
    {
        var started = await Match.StartWithFirstSegmentOpenAsync(match);
        var remaining = scripts.ToDictionary(s => s.TeamId, s => new Queue<char>(s.Script));
        foreach (var segment in started.Segments.Select((s, i) => (s, i)))
        {
            if (segment.i > 0)
            {
                (await Client.PostAsync($"{started.Live}/segments/{segment.s.Id}/open", null)).EnsureSuccessStatusCode();
            }

            while (true)
            {
                var state = await Match.StateAsync(started);
                if (state.CurrentSegment is not { } current || current.ServedQuestionCount >= current.PlannedQuestionCount || state.State != "InProgress")
                {
                    break;
                }

                var question = await Match.ServeAsync(started, segment.i);
                var holder = (await Match.StateAsync(started)).ActiveParticipant!;
                var turn = remaining[holder.TeamId].Dequeue();
                var response = turn == 'S'
                    ? await Client.PostAsJsonAsync(
                        $"{started.Live}/questions/{question.MatchQuestionId}/skip", new Contracts.V1.LiveMatch.SkipQuestionRequest("Scripted skip"))
                    : await Match.AnswerAsync(started, question.MatchQuestionId, holder.ParticipantId, turn == 'C' ? "Correct" : "Incorrect");
                response.EnsureSuccessStatusCode();
            }

            var after = await Match.StateAsync(started);
            if (after.State != "InProgress")
            {
                return;
            }

            if (after.CurrentSegment?.State == "Open")
            {
                (await Client.PostAsync($"{started.Live}/segments/{segment.s.Id}/close", null)).EnsureSuccessStatusCode();
            }
        }

        var end = await Client.PostAsync($"{started.Live}/end", null);
        end.EnsureSuccessStatusCode();
    }

    public async Task<QualificationPreviewResponse> PreviewAsync(Guid stageId)
    {
        var response = await Client.GetAsync($"{QualificationUrl}/stages/{stageId}/preview");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<QualificationPreviewResponse>())!;
    }
}
