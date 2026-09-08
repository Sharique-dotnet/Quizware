namespace Quizware.Application.Abstractions;

/// <summary>The tenant for the current request. Always sourced from the JWT
/// `program_id` claim (see ADR-002) — never from a route or query value.</summary>
public interface ICurrentProgram
{
    bool HasProgram { get; }

    /// <summary>Guid.Empty when HasProgram is false — never throws. Check
    /// HasProgram first if the caller requires a scoped token.</summary>
    Guid ProgramId { get; }
}
