namespace Quizware.Api.Middleware;

/// <summary>Names the route value that identifies the program when it is not
/// <c>{programId}</c> — on ProgramsController it is <c>{id}</c>. The
/// <see cref="ProgramScopeMiddleware"/> enforces it exactly like
/// <c>{programId}</c> (P15-03: without this, a token of one program could read
/// another program's details, dashboard, formats and settings).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ProgramRouteAttribute : Attribute
{
    public ProgramRouteAttribute(string parameterName)
    {
        ParameterName = parameterName;
    }

    public string ParameterName { get; }

    /// <summary>Lets a SuperAdmin token that is not scoped to any program
    /// through — program management is SuperAdmin's cross-tenant job. A token
    /// scoped to a program still has to match.</summary>
    public bool AllowUnscopedSuperAdmin { get; init; }
}
