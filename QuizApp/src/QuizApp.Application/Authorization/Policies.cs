namespace QuizApp.Application.Authorization;

/// <summary>Policy names per 05-API-Design.md §5.9. Role membership for each
/// is wired up in QuizApp.Api's AddApi() using QuizApp.Application.Authorization.Roles.</summary>
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
}
