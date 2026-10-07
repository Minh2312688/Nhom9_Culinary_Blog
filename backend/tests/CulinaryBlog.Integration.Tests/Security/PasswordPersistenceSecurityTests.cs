using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using CulinaryBlog.Application.DTOs.Auth;
using CulinaryBlog.Infrastructure.Identity;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Integration.Tests.Endpoints;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Security;

/// <summary>
/// NFR-SEC-001 Integration Security Tests:
/// Verifies that upon registration through the real application path:
/// 1. A valid user is created and persisted in the database.
/// 2. PasswordHash is generated and persisted (never null or empty).
/// 3. PasswordHash != raw password.
/// 4. PasswordHash verifies correctly via UserManager.
/// 5. The raw password is NEVER persisted in any mapped database property or column.
/// </summary>
public class PasswordPersistenceSecurityTests : IClassFixture<CustomAuthWebApplicationFactory>
{
    private readonly CustomAuthWebApplicationFactory _factory;

    public PasswordPersistenceSecurityTests(CustomAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RegisterFlow_MustPersistOnlyPasswordHash_AndNeverStorePlaintextPassword()
    {
        // Arrange - Generate isolated credentials (never logged or printed)
        var client = _factory.CreateIsolatedClient();
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..12];
        var email = $"sec_test_{uniqueSuffix}@example.com";
        var rawPassword = $"Str0ng#P@ssword_{uniqueSuffix}";
        var displayName = "Security Test User";

        var request = new RegisterRequestDto(email, rawPassword, displayName);

        // Act 1: Register through real HTTP endpoint and application pipeline
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "Valid registration must succeed with 201 Created");

        // Act 2: Query the real persisted record from ApplicationDbContext
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var persistedUser = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email);

        // Assert 1: User entity is persisted
        persistedUser.Should().NotBeNull("Registered user must be persisted in database");

        // Assert 2: PasswordHash is present and is NOT the raw password
        persistedUser!.PasswordHash.Should().NotBeNullOrWhiteSpace(
            "NFR-SEC-001 requires PasswordHash to be generated and stored");

        persistedUser.PasswordHash.Should().NotBe(rawPassword,
            "NFR-SEC-001 strictly prohibits storing raw password in PasswordHash");

        // Assert 3: PasswordHash verifies correctly through Identity UserManager
        var verificationSucceeded = await userManager.CheckPasswordAsync(persistedUser, rawPassword);
        verificationSucceeded.Should().BeTrue(
            "Persisted PasswordHash must successfully verify against the raw password");

        var invalidVerification = await userManager.CheckPasswordAsync(persistedUser, "IncorrectP@ssword#123");
        invalidVerification.Should().BeFalse(
            "Incorrect password must be rejected");

        // Assert 4: Raw password is NOT stored in ANY mapped entity property/database column
        var stringProperties = typeof(ApplicationUser)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string));

        foreach (var property in stringProperties)
        {
            var value = property.GetValue(persistedUser) as string;
            if (!string.IsNullOrEmpty(value))
            {
                value.Should().NotBe(rawPassword,
                    $"Property '{property.Name}' must never store the raw plaintext password");

                value.Should().NotContain(rawPassword,
                    $"Property '{property.Name}' must never contain the raw plaintext password");
            }
        }
    }
}
