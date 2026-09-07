namespace QuizApp.Infrastructure.Imports;

public sealed class ImportBatchRow
{
    public Guid Id { get; set; }
    public Guid ImportBatchId { get; set; }
    public int RowNumber { get; set; }
    public string RawDataJson { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public string? ValidationErrorsJson { get; set; }
    public Guid? CreatedEntityId { get; set; }
}
