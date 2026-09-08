namespace Quizware.Domain.Common.Exceptions;

/// <summary>One stage segment template still using the format being disabled
/// (05-API-Design.md §5.4's <c>FORMAT_IN_USE</c> response naming exactly
/// which stages block the change).</summary>
public sealed record FormatUsage(Guid StageId, string StageName, Guid SegmentTemplateId);

public sealed class FormatInUseException : Exception
{
    public FormatInUseException(string formatCode, IReadOnlyList<FormatUsage> usedBy)
        : base($"Format '{formatCode}' is still used by {usedBy.Count} segment template(s) and cannot be disabled.")
    {
        FormatCode = formatCode;
        UsedBy = usedBy;
    }

    public string FormatCode { get; }
    public IReadOnlyList<FormatUsage> UsedBy { get; }
}
