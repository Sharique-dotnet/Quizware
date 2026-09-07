using QuizApp.Domain.Common;
using QuizApp.Domain.Common.Exceptions;
using QuizApp.Domain.Enums;

namespace QuizApp.Domain.Programs;

/// <summary>
/// The root of every tenant table. Creating next year's event is one row here
/// — no new database, no redeployment.
/// </summary>
public sealed class Program : BaseEntity, IAuditable, ISoftDeletable
{
    private Program()
    {
        Code = string.Empty;
        Name = string.Empty;
        DefaultLanguage = string.Empty;
        TimeZoneId = string.Empty;
        CreatedBy = string.Empty;
    }

    public static Program Create(
        string code,
        string name,
        string createdBy,
        string defaultLanguage = "ur",
        string timeZoneId = "India Standard Time",
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Code is required.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        return new Program
        {
            Code = code,
            Name = name,
            Description = description,
            DefaultLanguage = defaultLanguage,
            TimeZoneId = timeZoneId,
            State = ProgramState.Draft,
            AllowNegativeTotals = true,
            QuestionPoolScope = QuestionPoolScope.ProgramOnly,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
        };
    }

    public string Code { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public string? OrganisationName { get; private set; }
    public int? SeasonYear { get; private set; }
    public DateOnly? StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public ProgramState State { get; private set; }
    public string DefaultLanguage { get; private set; }
    public string TimeZoneId { get; private set; }
    public bool AllowNegativeTotals { get; private set; }
    public int? MaxTeams { get; private set; }
    public QuestionPoolScope QuestionPoolScope { get; private set; }
    public bool BuzzerEnabled { get; private set; }
    public string? LogoUrl { get; private set; }
    public string? ThemePrimaryColor { get; private set; }
    public string? ThemeSecondaryColor { get; private set; }
    public string? FontFamily { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public string? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    /// <summary>Updates the descriptive and branding fields the admin UI
    /// exposes on one form. Does not touch lifecycle, formats or settings.</summary>
    public void UpdateDetails(
        string name,
        string? description,
        string? organisationName,
        string? logoUrl,
        string? themePrimaryColor,
        string? themeSecondaryColor,
        string? fontFamily,
        string updatedBy)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        Name = name;
        Description = description;
        OrganisationName = organisationName;
        LogoUrl = logoUrl;
        ThemePrimaryColor = themePrimaryColor;
        ThemeSecondaryColor = themeSecondaryColor;
        FontFamily = fontFamily;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void Configure()
    {
        RequireState(ProgramState.Draft, ProgramState.Configured);
        State = ProgramState.Configured;
    }

    /// <summary>A program cannot go Live without at least one stage — the
    /// central invariant of this entity. <paramref name="stageCount"/> is
    /// supplied by the caller (Application layer), since Stage is not part
    /// of this aggregate.</summary>
    public void GoLive(int stageCount)
    {
        RequireState(ProgramState.Configured, ProgramState.Live);

        if (stageCount < 1)
        {
            throw new InvalidStateTransitionException(
                $"Program '{Code}' cannot go Live without at least one stage.");
        }

        State = ProgramState.Live;
    }

    public void Complete()
    {
        RequireState(ProgramState.Live, ProgramState.Completed);
        State = ProgramState.Completed;
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void Archive()
    {
        RequireState(ProgramState.Completed, ProgramState.Archived);
        State = ProgramState.Archived;
    }

    private void RequireState(ProgramState required, ProgramState target)
    {
        if (State != required)
        {
            throw new InvalidStateTransitionException(
                $"Program '{Code}' cannot transition from {State} to {target}; it must be {required}.");
        }
    }
}
