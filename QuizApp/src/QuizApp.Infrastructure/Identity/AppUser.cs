using Microsoft.AspNetCore.Identity;

namespace QuizApp.Infrastructure.Identity;

public sealed class AppUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
