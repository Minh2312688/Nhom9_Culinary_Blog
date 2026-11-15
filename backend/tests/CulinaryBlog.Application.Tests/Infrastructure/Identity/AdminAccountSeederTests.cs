using CulinaryBlog.Domain.Constants;
using CulinaryBlog.Infrastructure.Identity;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Persistence.Seed;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.Application.Tests.Infrastructure.Identity;

public sealed class AdminAccountSeederTests
{
    [Fact]
    public async Task SeedAsync_creates_admin_once_and_preserves_existing_password()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase($"AdminSeed_{Guid.NewGuid():N}"));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        const string email = "admin@example.test";

        await AdminAccountSeeder.SeedAsync(userManager, roleManager, email, "Original-Admin-123!");
        var originalUser = await userManager.FindByEmailAsync(email);
        await AdminAccountSeeder.SeedAsync(userManager, roleManager, email, "Replacement-Admin-456!");

        var seededUser = await userManager.FindByEmailAsync(email);
        seededUser.Should().NotBeNull();
        seededUser!.Id.Should().Be(originalUser!.Id);
        (await userManager.CheckPasswordAsync(seededUser, "Original-Admin-123!")).Should().BeTrue();
        (await userManager.CheckPasswordAsync(seededUser, "Replacement-Admin-456!")).Should().BeFalse();
        (await userManager.IsInRoleAsync(seededUser, AppRoles.Admin)).Should().BeTrue();
        (await roleManager.RoleExistsAsync(AppRoles.Admin)).Should().BeTrue();
        (await userManager.Users.CountAsync(user => user.Email == email)).Should().Be(1);
        (await roleManager.Roles.CountAsync(role => role.Name == AppRoles.Admin)).Should().Be(1);
    }
}
