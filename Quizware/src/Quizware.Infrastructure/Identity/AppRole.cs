using Microsoft.AspNetCore.Identity;

namespace Quizware.Infrastructure.Identity;

public sealed class AppRole : IdentityRole<Guid>
{
    public AppRole()
    {
    }

    public AppRole(string roleName)
        : base(roleName)
    {
    }
}
