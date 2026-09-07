namespace QuizApp.Api.Contracts.V1.Programs;

public sealed record ProgramSummaryResponse(Guid Id, string Name, string State, DateTime CreatedAtUtc);

public sealed record ProgramDetailResponse(
    Guid Id,
    string Name,
    string State,
    string? Description,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record CreateProgramRequest(string Name, string? Description);

public sealed record UpdateProgramRequest(string Name, string? Description);

public sealed record CloneProgramRequest(Guid SourceProgramId, string NewProgramName);

public sealed record ProgramFormatEntry(string FormatCode, bool IsEnabled, int? DisplayOrder, string? DisabledReason);

public sealed record UpdateProgramFormatsRequest(IReadOnlyList<ProgramFormatEntry> Formats);

public sealed record ProgramFormatsResponse(IReadOnlyList<ProgramFormatEntry> Formats);

public sealed record ProgramSettingEntry(string Category, string Key, string Value);

public sealed record UpdateProgramSettingsRequest(IReadOnlyList<ProgramSettingEntry> Settings);

public sealed record ProgramSettingsResponse(IReadOnlyList<ProgramSettingEntry> Settings);

public sealed record ProgramValidationResponse(bool ReadyToGoLive, IReadOnlyList<string> Blockers);

public sealed record ProgramDashboardResponse(
    int TeamCount,
    int QuestionCount,
    int StageCount,
    int MatchCount,
    IReadOnlyList<string> Warnings);
