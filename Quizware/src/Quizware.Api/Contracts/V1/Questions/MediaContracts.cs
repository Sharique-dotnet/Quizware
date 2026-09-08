namespace Quizware.Api.Contracts.V1.Questions;

public sealed record MediaAssetResponse(
    Guid Id, string FileName, string MediaType, string MimeType, long FileSizeBytes, bool IsValidated, Guid? ProgramId, bool WasDeduplicated);
