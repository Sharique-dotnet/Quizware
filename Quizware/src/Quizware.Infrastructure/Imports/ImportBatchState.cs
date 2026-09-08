namespace Quizware.Infrastructure.Imports;

public enum ImportBatchState
{
    Uploaded = 1,
    Validated = 2,
    Committed = 3,
    Cancelled = 4,
}
