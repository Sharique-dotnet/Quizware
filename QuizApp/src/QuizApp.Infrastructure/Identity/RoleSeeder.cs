using Microsoft.AspNetCore.Identity;
using QuizApp.Application.Authorization;

namespace QuizApp.Infrastructure.Identity;

/// <summary>Seeds exactly the 7 roles from ADR-009 — no Judge role.</summary>
public static class RoleSeeder
{
    public static async Task SeedAsync(RoleManager<AppRole> roleManager)
    {
        foreach (var roleName in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new AppRole(roleName));
            }
        }
    }
}
