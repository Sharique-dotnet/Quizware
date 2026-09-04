namespace QuizApp.Domain.Enums;

public enum TieBreakEventState
{
    Detected = 1,
    AwaitingPlay = 2,
    InProgress = 3,
    Resolved = 4,
    Abandoned = 5,
}
