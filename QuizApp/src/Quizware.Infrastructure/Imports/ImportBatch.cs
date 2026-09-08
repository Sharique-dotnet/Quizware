namespace Quizware.Infrastructure.Imports;

/// <summary>Excel import: validate -> report -> commit. No silent row
/// skipping — every row's fate is visible before anything is saved.</summary>
public sealed class ImportBatch
{
    public Guid Id { get; set; }
    public Guid ProgramId { get; set; }
    public string ImportType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public int ImportedRows { get; set; }
    public ImportBatchState State { get; set; } = ImportBatchState.Uploaded;

    public DateTime CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
}
