using Quizware.Domain.Programs;

namespace Quizware.Application.Programs.Dtos;

internal static class ProgramMappings
{
    public static ProgramDto ToDto(this Program program) => new(
        program.Id,
        program.Code,
        program.Name,
        program.State.ToString(),
        program.Description,
        program.OrganisationName,
        program.LogoUrl,
        program.ThemePrimaryColor,
        program.ThemeSecondaryColor,
        program.FontFamily,
        program.MaxTeams,
        program.CreatedAtUtc,
        program.UpdatedAtUtc);

    public static ProgramSummaryDto ToSummaryDto(this Program program) => new(
        program.Id, program.Code, program.Name, program.State.ToString(), program.CreatedAtUtc);

    public static ProgramFormatDto ToDto(this ProgramQuestionFormat format) => new(
        format.FormatCode.ToString(), format.IsEnabled, format.DisplayOrder, format.DisabledReason);

    public static ProgramSettingDto ToDto(this ProgramSetting setting) => new(
        setting.Category, setting.Key, setting.Value);
}
