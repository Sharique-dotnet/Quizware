namespace QuizApp.Application.Programs.Dtos;

public sealed record ProgramDto(
    Guid Id,
    string Code,
    string Name,
    string State,
    string? Description,
    string? OrganisationName,
    string? LogoUrl,
    string? ThemePrimaryColor,
    string? ThemeSecondaryColor,
    string? FontFamily,
    int? MaxTeams,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record ProgramSummaryDto(Guid Id, string Code, string Name, string State, DateTime CreatedAtUtc);

public sealed record ProgramFormatDto(string FormatCode, bool IsEnabled, int DisplayOrder, string? DisabledReason);

public sealed record ProgramSettingDto(string Category, string Key, string Value);

public sealed record ProgramValidationDto(bool ReadyToGoLive, IReadOnlyList<string> Blockers);

public sealed record ProgramDashboardDto(
    int TeamCount, int QuestionCount, int StageCount, int MatchCount, IReadOnlyList<string> Warnings);
