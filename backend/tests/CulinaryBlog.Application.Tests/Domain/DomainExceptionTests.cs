using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Application.Tests.Domain;

public class DomainExceptionTests
{
    [Fact]
    public void Revoke_ActiveToken_ShouldSucceedAndSetRevocationFields()
    {
        // Arrange
        var token = new RefreshToken
        {
            UserId = "user-123",
            TokenHash = "hash-123",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            RevokedAt = null
        };
        var now = DateTimeOffset.UtcNow;
        var replacementHash = "new-hash-456";

        // Act
        token.Revoke(now, replacementHash);

        // Assert
        token.RevokedAt.Should().Be(now);
        token.ReplacedByTokenHash.Should().Be(replacementHash);
        token.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Revoke_AlreadyRevokedToken_ShouldThrowInvalidTokenStateException()
    {
        // Arrange
        var token = new RefreshToken
        {
            UserId = "user-123",
            TokenHash = "hash-123",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            RevokedAt = DateTimeOffset.UtcNow.AddHours(-1)
        };

        // Act
        var act = () => token.Revoke(DateTimeOffset.UtcNow, "any-hash");

        // Assert
        act.Should().Throw<InvalidTokenStateException>()
            .WithMessage("Refresh token has already been revoked.")
            .And.Should().BeAssignableTo<DomainException>();
    }

    [Fact]
    public void InvalidTokenStateException_ShouldInheritFromDomainException()
    {
        // Arrange & Act
        var exception = new InvalidTokenStateException("Domain rule breached");

        // Assert
        exception.Should().BeAssignableTo<DomainException>();
        exception.Should().BeAssignableTo<Exception>();
        exception.Message.Should().Be("Domain rule breached");
    }
}
