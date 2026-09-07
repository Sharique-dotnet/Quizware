using Microsoft.AspNetCore.Identity;
using QuizApp.Application.Authorization;

namespace QuizApp.Infrastructure.Identity;

/// <summary>Seeds exactly one SuperAdmin account so a fresh environment has
/// someone who can log in and create the first program. Only runs when no
/// users exist yet — never overwrites or resets an existing account.</summary>
public static class AdminUserSeeder
{
    public const string DefaultEmail = "admin@quizapp.local";
    public const string DefaultPassword = "ChangeMe!123";

    public static async Task SeedAsync(UserManager<AppUser> userManager)
    {
        if (userManager.Users.Any())
        {
            return;
        }

        var admin = new AppUser
        {
            UserName = DefaultEmail,
            Email = DefaultEmail,
            FullName = "Default Administrator",
            IsActive = true,
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(admin, DefaultPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, Roles.SuperAdmin);
        }
    }
}
