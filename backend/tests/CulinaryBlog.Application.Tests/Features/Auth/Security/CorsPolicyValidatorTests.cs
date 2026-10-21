using CulinaryBlog.Application.Common.Security;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Auth.Security;

/// <summary>
/// NFR-SEC-005 Unit Tests for CORS Policy & Wildcard Validation.
/// Strictly enforces that wildcard '*' and wildcard subdomains are rejected.
/// </summary>
public sealed class CorsPolicyValidatorTests
{
    [Theory]
    [InlineData("*")]
    [InlineData(" * ")]
    [InlineData("https://*")]
    [InlineData("*.culinaryblog.com")]
    [InlineData("https://*.culinaryblog.com")]
    public void ValidateAndFilterAllowedOrigins_WithWildcardOrigins_ShouldThrowInvalidOperationException(string wildcardOrigin)
    {
        var origins = new[] { wildcardOrigin };

        var act = () => CorsPolicyValidator.ValidateAndFilterAllowedOrigins(origins, isDevelopmentOrTesting: false);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Wildcard origins are strictly prohibited by SRS v1.0.0 NFR-SEC-005*");
    }

    [Fact]
    public void ValidateAndFilterAllowedOrigins_WithMixedValidAndWildcard_ShouldThrowInvalidOperationException()
    {
        var origins = new[] { "https://valid-domain.com", "*" };

        var act = () => CorsPolicyValidator.ValidateAndFilterAllowedOrigins(origins, isDevelopmentOrTesting: false);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Wildcard origins are strictly prohibited*");
    }

    [Theory]
    [InlineData("ftp://domain.com")]
    [InlineData("ws://domain.com")]
    [InlineData("just_a_string")]
    public void ValidateAndFilterAllowedOrigins_WithNonHttpUri_ShouldThrowInvalidOperationException(string invalidUri)
    {
        var origins = new[] { invalidUri };

        var act = () => CorsPolicyValidator.ValidateAndFilterAllowedOrigins(origins, isDevelopmentOrTesting: false);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must be valid absolute HTTP or HTTPS URLs*");
    }

    [Fact]
    public void ValidateAndFilterAllowedOrigins_WithValidOrigins_ShouldNormalizeAndTrim()
    {
        var origins = new[] { "https://culinaryblog.com/", "http://localhost:3000", "https://culinaryblog.com" };

        var result = CorsPolicyValidator.ValidateAndFilterAllowedOrigins(origins, isDevelopmentOrTesting: false);

        result.Should().HaveCount(2);
        result.Should().Contain("https://culinaryblog.com");
        result.Should().Contain("http://localhost:3000");
    }

    [Fact]
    public void ValidateAndFilterAllowedOrigins_EmptyOrNull_InDevelopmentOrTesting_ShouldDefaultToLocalhost()
    {
        var resultNull = CorsPolicyValidator.ValidateAndFilterAllowedOrigins(null, isDevelopmentOrTesting: true);
        var resultEmpty = CorsPolicyValidator.ValidateAndFilterAllowedOrigins(Array.Empty<string>(), isDevelopmentOrTesting: true);

        resultNull.Should().BeEquivalentTo(new[] { "http://localhost:3000", "http://localhost:3001" });
        resultEmpty.Should().BeEquivalentTo(new[] { "http://localhost:3000", "http://localhost:3001" });
    }

    [Fact]
    public void ValidateAndFilterAllowedOrigins_EmptyOrNull_InProduction_ShouldReturnEmptyArray()
    {
        var result = CorsPolicyValidator.ValidateAndFilterAllowedOrigins(null, isDevelopmentOrTesting: false);

        result.Should().BeEmpty();
    }
}
