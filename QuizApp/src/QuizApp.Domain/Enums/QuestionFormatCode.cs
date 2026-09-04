namespace QuizApp.Domain.Enums;

/// <summary>Values match the QuestionFormat lookup-table seed ids in 04-Database-Schema.md §4.3 (id 4 is intentionally unused).</summary>
public enum QuestionFormatCode
{
    Mcq = 1,
    AudioVisual = 2,
    Sequence = 3,
    Buzzer = 5,
    Passing = 6,
    Card = 7,
    Choice = 8,
    RapidFire = 9,
    VisualRapidFire = 10,
    TieBreaker = 11,
}
