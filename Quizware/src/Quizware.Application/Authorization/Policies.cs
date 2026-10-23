namespace Quizware.Application.Authorization;

/// <summary>Policy names per 05-API-Design.md §5.9. Role membership for each
/// is wired up in Quizware.Api's AddApi() using Quizware.Application.Authorization.Roles.</summary>
public static class Policies
{
    public const string CanManageProgram = nameof(CanManageProgram);
    public const string CanManageQuestions = nameof(CanManageQuestions);
    public const string CanOperateMatch = nameof(CanOperateMatch);
    public const string CanRecordAnswer = nameof(CanRecordAnswer);
    public const string CanAdjustScore = nameof(CanAdjustScore);
    public const string CanDisqualify = nameof(CanDisqualify);
    public const string CanResolveTie = nameof(CanResolveTie);
    public const string CanViewLive = nameof(CanViewLive);
    public const string DisplayOnly = nameof(DisplayOnly);

    /// <summary>Reports and the audit log: the admins plus the Auditor, who has
    /// read-only access to everything (03-PRD-API.md §3.2).</summary>
    public const string CanViewReports = nameof(CanViewReports);
}
