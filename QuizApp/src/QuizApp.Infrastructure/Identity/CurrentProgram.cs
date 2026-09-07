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

    public Guid ProgramId =>
        Guid.TryParse(RawProgramId, out var id)
            ? id
            : throw new InvalidOperationException("The current token is not scoped to a program.");
}
