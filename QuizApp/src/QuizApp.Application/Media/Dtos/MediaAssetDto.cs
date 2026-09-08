namespace QuizApp.Application.Media.Dtos;

public sealed record MediaAssetDto(
    Guid Id, string FileName, string MediaType, string MimeType, long FileSizeBytes, bool IsValidated, Guid? ProgramId, bool WasDeduplicated);
