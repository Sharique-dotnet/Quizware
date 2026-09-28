using App = Quizware.Application.Gameplay.Dtos;

namespace Quizware.Api.Contracts.V1.LiveMatch;

/// <summary>Maps the Application layer's live-match DTOs onto the frozen API
/// contract types (Application cannot reference Api).</summary>
public static class LiveMapper
{
    public static LiveMatchStateResponse ToResponse(App.LiveMatchStateDto dto) =>
        new(
            dto.MatchId,
            dto.MatchName,
            dto.StageName,
            dto.State,
            dto.IsPaused,
            dto.CurrentSegment is null ? null : ToResponse(dto.CurrentSegment),
            dto.CurrentQuestion is null ? null : ToResponse(dto.CurrentQuestion),
            dto.ActiveParticipant is null
                ? null
                : new ActiveParticipantDto(
                    dto.ActiveParticipant.ParticipantId, dto.ActiveParticipant.TeamId, dto.ActiveParticipant.TeamName,
                    dto.ActiveParticipant.SeatNumber, dto.ActiveParticipant.TurnOrder),
            dto.Participants.Select(ToResponse).ToList(),
            new MatchProgressDto(dto.Progress.QuestionsServed, dto.Progress.QuestionsTotal, dto.Progress.SegmentsCompleted, dto.Progress.SegmentsTotal),
            new LiveBuzzerStatusDto(dto.Buzzer.Available, dto.Buzzer.SessionId, dto.Buzzer.State),
            dto.CanUndo,
            dto.LastAnswerId);

    public static StartMatchResponse ToResponse(App.StartMatchResultDto dto) =>
        new(
            dto.MatchId,
            dto.State,
            dto.RandomSeed,
            dto.StartedAtUtc,
            dto.Participants
                .Select(p => new LiveParticipantDto(p.ParticipantId, p.TeamId, p.TeamName, p.SeatNumber, p.TurnOrder, p.Status, p.ScoreImageUrl, p.BuzzDeviceId))
                .ToList(),
            dto.Segments.Select(s => new LiveSegmentDto(s.SegmentId, s.Format, s.OrderIndex, s.PlannedQuestionCount, s.State)).ToList(),
            dto.QuestionsReserved,
            dto.BuzzerAvailable);

    public static CurrentSegmentDto ToResponse(App.CurrentSegmentDto dto) =>
        new(dto.SegmentId, dto.Format, dto.DisplayName, dto.OrderIndex, dto.State, dto.PlannedQuestionCount,
            dto.ServedQuestionCount, dto.TopicSelectionMode);

    public static CurrentQuestionDto ToResponse(App.CurrentQuestionDto dto) =>
        new(
            dto.MatchQuestionId,
            dto.OrderIndex,
            dto.State,
            dto.Format,
            dto.QuestionText,
            dto.DifficultyLevel,
            dto.TopicName,
            dto.MediaUrl,
            dto.Options?.Select(o => new CurrentQuestionOptionDto(o.OptionId, o.Text, o.DisplayOrder)).ToList(),
            dto.CorrectOptionId,
            dto.TimeLimitSeconds,
            dto.TimerStartedAtUtc,
            dto.ServerNowUtc,
            dto.RemainingSeconds);

    public static LiveParticipantScoreDto ToResponse(App.LiveParticipantScoreDto dto) =>
        new(dto.ParticipantId, dto.TeamName, dto.SeatNumber, dto.TurnOrder, dto.Status, dto.Score);

    public static RecordAnswerResponse ToResponse(App.RecordAnswerResultDto dto) =>
        new(
            dto.AnswerRecordId,
            dto.Outcome,
            dto.PointsAwarded,
            dto.ScoringRuleId,
            dto.TeamScore,
            dto.Scores.Select(s => new RankedTeamScoreDto(s.TeamId, s.Score, s.Rank)).ToList(),
            dto.NextQuestion is null
                ? null
                : new NextQuestionPreviewDto(dto.NextQuestion.MatchQuestionId, dto.NextQuestion.ActiveParticipantId, dto.NextQuestion.ActiveTeamName),
            dto.SegmentComplete,
            dto.MatchComplete);

    public static DisqualifyParticipantResponse ToResponse(App.DisqualifyResultDto dto) =>
        new(
            dto.ParticipantId,
            dto.TeamName,
            dto.Status,
            dto.RemovedAtUtc,
            dto.RemainingActiveParticipants.Select(ToResponse).ToList(),
            dto.MatchCanContinue,
            dto.TurnOrderRecalculated,
            dto.CurrentSegmentAdjustment is null
                ? null
                : new CurrentSegmentAdjustmentDto(
                    dto.CurrentSegmentAdjustment.Policy,
                    dto.CurrentSegmentAdjustment.PlannedQuestionCountBefore,
                    dto.CurrentSegmentAdjustment.PlannedQuestionCountAfter,
                    dto.CurrentSegmentAdjustment.Message),
            dto.NextActiveParticipantId,
            dto.MatchCompleted,
            dto.WinnerTeamId);
}
