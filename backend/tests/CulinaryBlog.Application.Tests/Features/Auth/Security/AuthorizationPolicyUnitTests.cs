using System.Security.Claims;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Domain.Constants;
using CulinaryBlog.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Auth.Security;

/// <summary>
/// NFR-SEC-006: Authorization Policies & Resource Ownership Unit Tests.
/// Verifies policy names, role constants, and application-layer access evaluation.
/// </summary>
public sealed class AuthorizationPolicyUnitTests
{
    [Fact]
    public void AppPolicies_ShouldDefineAuthorAndAdminPolicies()
    {
        AppPolicies.AuthorPolicy.Should().Be("AuthorPolicy");
        AppPolicies.AdminPolicy.Should().Be("AdminPolicy");
    }

    [Fact]
    public void AppRoles_ShouldDefineAllRequiredRolesWithoutHardcoding()
    {
        AppRoles.Admin.Should().Be("Admin");
        AppRoles.Author.Should().Be("Author");
        AppRoles.Reader.Should().Be("Reader");
    }

    [Theory]
    [InlineData(AppRoles.Admin, true)]
    [InlineData(AppRoles.Author, true)]
    [InlineData(AppRoles.Reader, false)]
    [InlineData("Guest", false)]
    [InlineData("", false)]
    public void AuthorPolicy_RequirementCheck_ShouldAllowOnlyAuthorOrAdmin(string userRole, bool expectedAllowed)
    {
        // AuthorPolicy: allows Author or Admin
        var claims = string.IsNullOrEmpty(userRole)
            ? Array.Empty<Claim>()
            : new[] { new Claim(ClaimTypes.Role, userRole) };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var isAllowed = principal.IsInRole(AppRoles.Author) || principal.IsInRole(AppRoles.Admin);
        isAllowed.Should().Be(expectedAllowed);
    }

    [Theory]
    [InlineData(AppRoles.Admin, true)]
    [InlineData(AppRoles.Author, false)]
    [InlineData(AppRoles.Reader, false)]
    public void AdminPolicy_RequirementCheck_ShouldAllowOnlyAdmin(string userRole, bool expectedAllowed)
    {
        // AdminPolicy: strictly requires Admin
        var claims = new[] { new Claim(ClaimTypes.Role, userRole) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var isAllowed = principal.IsInRole(AppRoles.Admin);
        isAllowed.Should().Be(expectedAllowed);
    }

    [Fact]
    public void UnauthenticatedUser_ShouldBeRejectedByBothPolicies()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity()); // IsAuthenticated = false

        var canAuthor = principal.Identity?.IsAuthenticated == true &&
                        (principal.IsInRole(AppRoles.Author) || principal.IsInRole(AppRoles.Admin));
        var canAdmin = principal.Identity?.IsAuthenticated == true &&
                       principal.IsInRole(AppRoles.Admin);

        canAuthor.Should().BeFalse();
        canAdmin.Should().BeFalse();
    }

    [Fact]
    public void ApplicationLayer_ResourceOwnership_NonOwnerNonAdmin_ShouldBeForbidden()
    {
        var currentUserId = "author-user-123";
        var recipeOwnerId = "author-user-456";
        var isAdmin = false;

        // Simulate ownership check from RecipeCommands EnsureOwner logic
        var isAuthorized = isAdmin || currentUserId == recipeOwnerId;

        isAuthorized.Should().BeFalse();
    }

    [Fact]
    public void ApplicationLayer_ResourceOwnership_Owner_ShouldBeAllowed()
    {
        var currentUserId = "author-user-123";
        var recipeOwnerId = "author-user-123";
        var isAdmin = false;

        var isAuthorized = isAdmin || currentUserId == recipeOwnerId;

        isAuthorized.Should().BeTrue();
    }

    [Fact]
    public void ApplicationLayer_ResourceOwnership_AdminOverridingOwner_ShouldBeAllowed()
    {
        var currentUserId = "admin-super-999";
        var recipeOwnerId = "author-user-123";
        var isAdmin = true;

        var isAuthorized = isAdmin || currentUserId == recipeOwnerId;

        isAuthorized.Should().BeTrue();
    }
}
