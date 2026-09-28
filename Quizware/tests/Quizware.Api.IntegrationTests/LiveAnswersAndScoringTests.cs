using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Quizware.Api.Contracts.V1.LiveMatch;
using Quizware.Api.Contracts.V1.Matches;
using Quizware.Application.Authorization;
using Quizware.Domain.Enums;

namespace Quizware.Api.IntegrationTests;

/// <summary>Phase 9d: recording answers with rule-resolved scoring, passing,
/// idempotency, and compensating reversal.</summary>
public class LiveAnswersAndScoringTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public LiveAnswersAndScoringTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static async Task<Guid> HolderAsync(MatchTestHarness h, MatchTestHarness.StartedMatch started) =>
        (await h.StateAsync(started)).ActiveParticipant!.ParticipantId;

    [Fact]
    public async Task Correct_ScoresByTheRule_WritesTheLedger_AndClosesTheQuestion()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(3, 2));
        var question = await h.ServeAsync(started);
        var holder = await HolderAsync(h, started);

        var response = await h.AnswerAsync(started, question.MatchQuestionId, holder, "Correct", selectedOptionId: question.CorrectOptionId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = (await response.Content.ReadFromJsonAsync<RecordAnswerResponse>())!;
        result.Outcome.Should().Be("Correct");
        result.PointsAwarded.Should().Be(10);
        result.TeamScore.Should().Be(10);
        result.Scores.Should().HaveCount(3);
        result.Scores[0].Should().Match<RankedTeamScoreDto>(s => s.Score == 10 && s.Rank == 1);
        result.Scores.Skip(1).Should().OnlyContain(s => s.Score == 0 && s.Rank == 2);
        result.NextQuestion!.ActiveParticipantId.Should().NotBe(holder, "the turn moves on to the next team");
        result.SegmentComplete.Should().BeFalse();

        var answer = await h.ReadDbAsync(db => db.AnswerRecords.SingleAsync(a => a.Id == result.AnswerRecordId));
        answer.IsCorrect.Should().BeTrue();
        (await h.ReadDbAsync(db => db.ScoreEvents.SingleAsync(e => e.AnswerRecordId == answer.Id))).Points.Should().Be(10);
        (await h.ReadDbAsync(db => db.MatchQuestions.SingleAsync(q => q.Id == question.MatchQuestionId))).State
            .Should().Be(MatchQuestionState.Answered);
        (await h.StateAsync(started)).Participants.Single(p => p.ParticipantId == holder).Score.Should().Be(10);
    }

    [Fact]
    public async Task LastQuestionOfLastSegment_ReportsSegmentAndMatchComplete()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 1));
        var question = await h.ServeAsync(started);

        var result = (await (await h.AnswerAsync(started, question.MatchQuestionId, await HolderAsync(h, started), "Incorrect"))
            .Content.ReadFromJsonAsync<RecordAnswerResponse>())!;

        result.PointsAwarded.Should().Be(0);
        result.NextQuestion.Should().BeNull();
        result.SegmentComplete.Should().BeTrue();
        result.MatchComplete.Should().BeTrue();
    }

    [Fact]
    public async Task OutcomeContradictingTheSelectedOption_IsRejected()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        var question = await h.ServeAsync(started);
        var holder = await HolderAsync(h, started);
        var wrongOption = question.Options!.Single(o => o.OptionId != question.CorrectOptionId).OptionId;

        var correctButWrongOption = await h.AnswerAsync(started, question.MatchQuestionId, holder, "Correct", selectedOptionId: wrongOption);
        var incorrectButRightOption = await h.AnswerAsync(started, question.MatchQuestionId, holder, "Incorrect", selectedOptionId: question.CorrectOptionId);
        var foreignOption = await h.AnswerAsync(started, question.MatchQuestionId, holder, "Correct", selectedOptionId: Guid.NewGuid());

        correctButWrongOption.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        incorrectButRightOption.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        foreignOption.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await h.ReadDbAsync(db => db.AnswerRecords.CountAsync(a => a.MatchId == started.Match.Id))).Should().Be(0);
    }

    [Fact]
    public async Task OnlyTheHolder_MayAnswer_AndOnlyTheQuestionOnScreen()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        var question = await h.ServeAsync(started);
        var state = await h.StateAsync(started);
        var holder = state.ActiveParticipant!.ParticipantId;
        var other = state.Participants.Single(p => p.ParticipantId != holder).ParticipantId;

        var wrongTeam = await h.AnswerAsync(started, question.MatchQuestionId, other, "Correct");
        await h.AnswerAsync(started, question.MatchQuestionId, holder, "Correct");
        var alreadyClosed = await h.AnswerAsync(started, question.MatchQuestionId, holder, "Correct");

        wrongTeam.StatusCode.Should().Be(HttpStatusCode.Conflict);
        alreadyClosed.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task OutcomeWithoutAScoringRule_IsRefusedWithScoringRuleMissing()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        var question = await h.ServeAsync(started);

        var response = await h.AnswerAsync(started, question.MatchQuestionId, await HolderAsync(h, started), "NoAnswer");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MatchTestHarness.ErrorCodeAsync(response)).Should().Be("SCORING_RULE_MISSING");
    }

    [Fact]
    public async Task NonAnswerOutcomes_AreRejected_WithTheirOwnEndpointsInstead()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        var question = await h.ServeAsync(started);

        var response = await h.AnswerAsync(started, question.MatchQuestionId, await HolderAsync(h, started), "Passed");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ScorerRole_MayRecordAnswers_ButNotServe()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        var question = await h.ServeAsync(started);
        var scorer = await h.CreateClientAsync(Roles.Scorer);

        var answer = await scorer.PostAsJsonAsync($"{started.Live}/answers", new RecordAnswerRequest(
            question.MatchQuestionId, await HolderAsync(h, started), "Correct", null, null, null, 0, "Operator", null, null));
        var serve = await scorer.PostAsJsonAsync($"{started.Live}/questions/serve", new ServeQuestionRequest(started.Segments[0].Id));

        answer.StatusCode.Should().Be(HttpStatusCode.OK);
        serve.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SameIdempotencyKey_ReplaysTheFirstResult_WithoutASecondAnswer()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        var question = await h.ServeAsync(started);
        var holder = await HolderAsync(h, started);
        var key = $"answer-{Guid.NewGuid():N}";

        var first = await h.AnswerAsync(started, question.MatchQuestionId, holder, "Correct", idempotencyKey: key);
        var replay = await h.AnswerAsync(started, question.MatchQuestionId, holder, "Correct", idempotencyKey: key);

        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        (await replay.Content.ReadFromJsonAsync<RecordAnswerResponse>())!.AnswerRecordId
            .Should().Be((await first.Content.ReadFromJsonAsync<RecordAnswerResponse>())!.AnswerRecordId);
        (await h.ReadDbAsync(db => db.AnswerRecords.CountAsync(a => a.MatchId == started.Match.Id))).Should().Be(1);
    }

    [Fact]
    public async Task Reverse_WindsTheScoreBack_KeepsTheRecord_AndReopensTheQuestion()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        var question = await h.ServeAsync(started);
        var holder = await HolderAsync(h, started);
        var answered = (await (await h.AnswerAsync(started, question.MatchQuestionId, holder, "Correct"))
            .Content.ReadFromJsonAsync<RecordAnswerResponse>())!;

        var noReason = await h.Client.PostAsJsonAsync($"{started.Live}/answers/{answered.AnswerRecordId}/reverse", new ReverseAnswerRequest(""));
        var reversed = await h.Client.PostAsJsonAsync($"{started.Live}/answers/{answered.AnswerRecordId}/reverse", new ReverseAnswerRequest("Mis-click"));
        var twice = await h.Client.PostAsJsonAsync($"{started.Live}/answers/{answered.AnswerRecordId}/reverse", new ReverseAnswerRequest("Again"));
        var reAnswer = await h.AnswerAsync(started, question.MatchQuestionId, holder, "Incorrect");

        noReason.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        reversed.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = (await reversed.Content.ReadFromJsonAsync<RecordAnswerResponse>())!;
        result.Outcome.Should().Be("Voided");
        result.PointsAwarded.Should().Be(-10);
        result.TeamScore.Should().Be(0);
        twice.StatusCode.Should().Be(HttpStatusCode.Conflict);
        reAnswer.StatusCode.Should().Be(HttpStatusCode.OK, "the reversed question went back on screen");

        var original = await h.ReadDbAsync(db => db.AnswerRecords.SingleAsync(a => a.Id == answered.AnswerRecordId));
        original.IsReversed.Should().BeTrue();
        original.ReversalReason.Should().Be("Mis-click");
        var ledger = await h.ReadDbAsync(db => db.ScoreEvents.Where(e => e.MatchId == started.Match.Id).ToListAsync());
        ledger.Select(e => e.Points).Should().BeEquivalentTo([10, -10, 0]);
    }

    [Fact]
    public async Task Reverse_AfterTheNextQuestionWasServed_DoesNotReopen()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        var first = await h.ServeAsync(started);
        var answered = (await (await h.AnswerAsync(started, first.MatchQuestionId, await HolderAsync(h, started), "Correct"))
            .Content.ReadFromJsonAsync<RecordAnswerResponse>())!;
        await h.ServeAsync(started);

        var reversed = await h.Client.PostAsJsonAsync($"{started.Live}/answers/{answered.AnswerRecordId}/reverse", new ReverseAnswerRequest("Late fix"));

        reversed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await h.ReadDbAsync(db => db.MatchQuestions.SingleAsync(q => q.Id == first.MatchQuestionId))).State
            .Should().Be(MatchQuestionState.Answered);
    }

    [Fact]
    public async Task Pass_MovesTheQuestionToTheNextTeam_WhoseAnswerIsScoredAsAfterPass()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var match = await h.CreateReadyToStartMatchAsync(3, 2);
        await h.ConfigureTemplatesAsync(match.StageId, allowPassing: true, maxPassCount: 1);
        await h.AddScoringRuleAsync("Mcq", "PassedCorrect", "AfterPass", 5);
        var started = await h.StartWithFirstSegmentOpenAsync(match);
        var question = await h.ServeAsync(started);
        var before = await h.StateAsync(started);
        var holder = before.ActiveParticipant!;

        var passed = await h.Client.PostAsJsonAsync($"{started.Live}/pass", new PassQuestionRequest(question.MatchQuestionId, holder.ParticipantId));
        var newHolder = (await passed.Content.ReadFromJsonAsync<LiveMatchStateResponse>())!.ActiveParticipant!;
        var passAgain = await h.Client.PostAsJsonAsync($"{started.Live}/pass", new PassQuestionRequest(question.MatchQuestionId, newHolder.ParticipantId));
        var staleNumber = await h.AnswerAsync(started, question.MatchQuestionId, newHolder.ParticipantId, "Correct", passNumber: 0);
        var answer = await h.AnswerAsync(started, question.MatchQuestionId, newHolder.ParticipantId, "Correct", passNumber: 1);

        passed.StatusCode.Should().Be(HttpStatusCode.OK);
        newHolder.TurnOrder.Should().Be(holder.TurnOrder + 1);
        passAgain.StatusCode.Should().Be(HttpStatusCode.Conflict, "MaxPassCount is 1");
        staleNumber.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = (await answer.Content.ReadFromJsonAsync<RecordAnswerResponse>())!;
        result.Outcome.Should().Be("PassedCorrect");
        result.PointsAwarded.Should().Be(5);
        var passRecord = await h.ReadDbAsync(db => db.AnswerRecords.SingleAsync(
            a => a.MatchQuestionId == question.MatchQuestionId && a.Outcome == AnswerOutcome.Passed));
        passRecord.MatchParticipantId.Should().Be(holder.ParticipantId);
    }

    [Fact]
    public async Task Pass_WhereNotAllowed_OrByAnotherTeam_IsRefused()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        var question = await h.ServeAsync(started);
        var state = await h.StateAsync(started);
        var other = state.Participants.Single(p => p.ParticipantId != state.ActiveParticipant!.ParticipantId).ParticipantId;

        var notAllowed = await h.Client.PostAsJsonAsync(
            $"{started.Live}/pass", new PassQuestionRequest(question.MatchQuestionId, state.ActiveParticipant!.ParticipantId));
        var notHolder = await h.Client.PostAsJsonAsync($"{started.Live}/pass", new PassQuestionRequest(question.MatchQuestionId, other));

        notAllowed.StatusCode.Should().Be(HttpStatusCode.Conflict);
        notHolder.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Buzzer_AWrongAnswerLeavesTheQuestionOpenToTheOthers()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        await h.ResetScoringDefaultsAsync();
        await h.SeedBuzzerAsync(2);
        var stage = await h.CreateStageAsync(("Buzzer", 2));
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateMatchAsync(stage.Id, 1, await h.CreateTeamsAsync(3)));
        var question = await h.ServeAsync(started);
        var participants = (await h.StateAsync(started)).Participants.Select(p => p.ParticipantId).ToList();

        var wrong = (await (await h.AnswerAsync(started, question.MatchQuestionId, participants[2], "Incorrect"))
            .Content.ReadFromJsonAsync<RecordAnswerResponse>())!;
        var sameTeamAgain = await h.AnswerAsync(started, question.MatchQuestionId, participants[2], "Correct");
        var right = (await (await h.AnswerAsync(started, question.MatchQuestionId, participants[1], "Correct"))
            .Content.ReadFromJsonAsync<RecordAnswerResponse>())!;

        wrong.PointsAwarded.Should().Be(-15);
        wrong.NextQuestion!.MatchQuestionId.Should().Be(question.MatchQuestionId, "the question stays open for a steal");
        sameTeamAgain.StatusCode.Should().Be(HttpStatusCode.Conflict);
        right.PointsAwarded.Should().Be(20);
        (await h.ReadDbAsync(db => db.MatchQuestions.SingleAsync(q => q.Id == question.MatchQuestionId))).State
            .Should().Be(MatchQuestionState.Answered);
    }

    [Fact]
    public async Task Buzzer_WithoutSteal_AWrongAnswerClosesTheQuestion()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        await h.ResetScoringDefaultsAsync();
        await h.SeedBuzzerAsync(2, allowStealAfterWrong: false);
        var stage = await h.CreateStageAsync(("Buzzer", 2));
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateMatchAsync(stage.Id, 1, await h.CreateTeamsAsync(2)));
        var question = await h.ServeAsync(started);
        var participant = (await h.StateAsync(started)).Participants[0].ParticipantId;

        await h.AnswerAsync(started, question.MatchQuestionId, participant, "Incorrect");

        (await h.ReadDbAsync(db => db.MatchQuestions.SingleAsync(q => q.Id == question.MatchQuestionId))).State
            .Should().Be(MatchQuestionState.Answered);
    }

    [Fact]
    public async Task End_AfterScoring_NamesTheWinner_AndRanksTheRest()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        var question = await h.ServeAsync(started);
        var holder = await HolderAsync(h, started);
        await h.AnswerAsync(started, question.MatchQuestionId, holder, "Correct");

        await h.Client.PostAsync($"{started.Live}/end", null);

        var stored = await h.ReadDbAsync(db => db.Matches.SingleAsync(m => m.Id == started.Match.Id));
        var participants = await h.ReadDbAsync(db => db.MatchParticipants.Where(p => p.MatchId == started.Match.Id).ToListAsync());
        var winner = participants.Single(p => p.Id == holder);
        stored.IsTied.Should().BeFalse();
        stored.WinnerTeamId.Should().Be(winner.TeamId);
        winner.FinalRank.Should().Be(1);
        winner.FinalScore.Should().Be(10);
        participants.Single(p => p.Id != holder).FinalRank.Should().Be(2);
    }

    [Fact]
    public async Task Ready_FlagsAFormatWithNoRuleForAnIncorrectAnswer()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        await h.ResetScoringDefaultsAsync();
        var stage = await h.CreateStageAsync(("AudioVisual", 1));
        var match = await h.CreateMatchAsync(stage.Id, 1, await h.CreateTeamsAsync(2));

        var result = (await (await h.Client.PostAsync($"{h.MatchesUrl}/{match.Id}/ready", null))
            .Content.ReadFromJsonAsync<MatchReadyResponse>())!;

        result.Blockers.Should().Contain("SCORING_RULE_MISSING: no scoring rule for an incorrect AudioVisual answer.");
        result.Blockers.Should().NotContain(b => b.Contains("a correct AudioVisual"));
    }

    [Theory]
    [InlineData(Roles.Operator)]
    [InlineData(Roles.Scorer)]
    public async Task ReversalAndDisqualification_AreProgramAdminOnly(string role)
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(3, 2));
        var question = await h.ServeAsync(started);
        var holder = await HolderAsync(h, started);
        var answered = (await (await h.AnswerAsync(started, question.MatchQuestionId, holder, "Correct"))
            .Content.ReadFromJsonAsync<RecordAnswerResponse>())!;
        var client = await h.CreateClientAsync(role);

        var reverse = await client.PostAsJsonAsync(
            $"{started.Live}/answers/{answered.AnswerRecordId}/reverse", new ReverseAnswerRequest("Not allowed"));
        var disqualify = await client.PostAsJsonAsync(
            $"{started.Live}/participants/{holder}/disqualify", new DisqualifyParticipantRequest("Not allowed", Guid.NewGuid(), true));

        reverse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        disqualify.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
