using Microsoft.EntityFrameworkCore;
using QuizApp.Domain.Enums;
using QuizApp.Domain.Programs;
using QuizApp.Domain.Qualification;
using QuizApp.Domain.Scoring;
using QuizApp.Domain.Teams;
using QuizApp.Domain.Tournament;

namespace QuizApp.Infrastructure.Persistence;

/// <summary>
/// P7-12: reproduces the legacy 18-team tournament entirely as configuration
/// rows — 18 teams, 3 stages (6 League + 3 Semi-Final + 1 Final matches),
/// League's 4-segment order, program-wide scoring defaults, and an MCQ
/// tie-break rule for League qualification. Dev/demo convenience only, gated
/// the same way as <see cref="Identity.AdminUserSeeder"/> (never runs in
/// Production) and idempotent by the program's Code, so re-running on an
/// already-seeded database is a no-op. Deliberately stops at configuration:
/// it does not assign teams to matches or create MatchParticipant rows —
/// that is bracket/scheduling logic belonging to later phases (Phase 8/9),
/// not P7's "configuration model" scope.
/// </summary>
public static class TournamentSeeder
{
    public const string DemoProgramCode = "DEMO-18";
    private const string Actor = "system:seed";

    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Programs.AnyAsync(p => p.Code == DemoProgramCode))
        {
            return;
        }

        var program = Program.Create(DemoProgramCode, "Demo 18-Team Tournament", Actor, description: "Seeded per P7-12.");
        db.Programs.Add(program);

        foreach (var formatCode in Enum.GetValues<QuestionFormatCode>())
        {
            db.ProgramQuestionFormats.Add(ProgramQuestionFormat.Create(program.Id, formatCode, Actor));
        }

        for (var i = 1; i <= 18; i++)
        {
            var code = $"T{i:D2}";
            db.Teams.Add(Team.Register(program.Id, code, $"School {i}", $"Team {i}", Actor));
        }

        foreach (var value in DefaultScoringValues.All)
        {
            db.ScoringRules.Add(ScoringRule.Create(program.Id, value.FormatCode, value.Outcome, value.Points, Actor, contextKey: value.ContextKey));
        }

        var league = Stage.Create(program.Id, "League", 0, StageType.League, Actor);
        var semiFinal = Stage.Create(program.Id, "Semi-Final", 1, StageType.Knockout, Actor);
        var final = Stage.Create(program.Id, "Final", 2, StageType.Final, Actor);
        db.Stages.AddRange(league, semiFinal, final);

        // League's 4-segment order, per the legacy tournament's running order.
        db.StageSegmentTemplates.AddRange(
            StageSegmentTemplate.Create(program.Id, league.Id, QuestionFormatCode.Mcq, 0, 5, Actor),
            StageSegmentTemplate.Create(program.Id, league.Id, QuestionFormatCode.AudioVisual, 1, 3, Actor),
            StageSegmentTemplate.Create(program.Id, league.Id, QuestionFormatCode.Buzzer, 2, 4, Actor),
            StageSegmentTemplate.Create(program.Id, league.Id, QuestionFormatCode.RapidFire, 3, 5, Actor));

        var seed = 42L;
        for (var i = 1; i <= 6; i++)
        {
            db.Matches.Add(Match.Create(program.Id, league.Id, i, seed + i, Actor));
        }

        for (var i = 1; i <= 3; i++)
        {
            db.Matches.Add(Match.Create(program.Id, semiFinal.Id, i, seed + 100 + i, Actor));
        }

        db.Matches.Add(Match.Create(program.Id, final.Id, 1, seed + 200, Actor));

        db.QualificationRules.Add(QualificationRule.Create(
            program.Id, league.Id, Actor, toStageId: semiFinal.Id, winnersPerMatch: 1, bestRemainingAcrossStage: 2));
        db.QualificationRules.Add(QualificationRule.Create(
            program.Id, semiFinal.Id, Actor, toStageId: final.Id, winnersPerMatch: 1));

        var leagueTieBreak = TieBreakRule.Create(
            program.Id, "League qualification tie-break", TieBreakScope.StageQualification,
            ["HeadToHead", "HigherDifficultyCorrect"], Actor, stageId: league.Id);
        db.TieBreakRules.Add(leagueTieBreak);

        await db.SaveChangesAsync();
    }
}
