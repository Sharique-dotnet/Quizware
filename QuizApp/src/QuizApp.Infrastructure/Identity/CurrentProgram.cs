using Microsoft.AspNetCore.Http;
using QuizApp.Application.Abstractions;

namespace QuizApp.Infrastructure.Identity;

public sealed class CurrentProgram : ICurrentProgram
{
    private const string ProgramIdClaimType = "program_id";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentProgram(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private string? RawProgramId => _httpContextAccessor.HttpContext?.User.FindFirst(ProgramIdClaimType)?.Value;

    public bool HasProgram => Guid.TryParse(RawProgramId, out _);

    /// <summary>
    /// Guid.Empty when HasProgram is false — deliberately does not throw.
    /// AppDbContext's tenant query filter reads this unconditionally as part
    /// of building the SQL parameter for "!HasProgram || ProgramId == ...";
    /// EF evaluates both operands eagerly to parameterize the query, before
    /// any OR short-circuiting happens at the database, so this getter must
    /// never throw. Callers that require a scoped token must check
    /// HasProgram themselves (e.g. ProgramScopeMiddleware already does).
    /// </summary>
    public Guid ProgramId => Guid.TryParse(RawProgramId, out var id) ? id : Guid.Empty;
}
