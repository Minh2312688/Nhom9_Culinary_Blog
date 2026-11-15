using CulinaryBlog.Domain.Constants;
using CulinaryBlog.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace CulinaryBlog.Infrastructure.Persistence.Seed;

public static class AdminAccountSeeder
{
    public static async Task SeedAsync(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        string email,
        string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        if (!await roleManager.RoleExistsAsync(AppRoles.Admin))
        {
            EnsureSucceeded(
                await roleManager.CreateAsync(new IdentityRole(AppRoles.Admin)),
                $"create the '{AppRoles.Admin}' role");
        }

        var normalizedEmail = email.Trim();
        var user = await userManager.FindByEmailAsync(normalizedEmail);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = normalizedEmail,
                Email = normalizedEmail,
                EmailConfirmed = true,
                DisplayName = "Administrator",
                CreatedAt = DateTimeOffset.UtcNow
            };
            EnsureSucceeded(await userManager.CreateAsync(user, password), "create the configured Admin account");
        }

        if (!await userManager.IsInRoleAsync(user, AppRoles.Admin))
        {
            EnsureSucceeded(
                await userManager.AddToRoleAsync(user, AppRoles.Admin),
                $"assign the Admin role to '{normalizedEmail}'");
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join("; ", result.Errors.Select(error => error.Description));
        throw new InvalidOperationException($"Failed to {operation}: {errors}");
    }
}
