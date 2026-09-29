using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.Features.Auth.TokenRefresh;
using CulinaryBlog.Domain.Constants;
using CulinaryBlog.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Auth.TokenRefresh;

public class RefreshTokenCommandHandlerTests
{
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock = new();
    private readonly Mock<IIdentityService> _identityServiceMock = new();
    private readonly Mock<IJwtTokenGenerator> _jwtTokenGeneratorMock = new();
    private readonly Mock<ILogger<RefreshTokenCommandHandler>> _loggerMock = new();
    private readonly RefreshTokenCommandHandler _handler;

    public RefreshTokenCommandHandlerTests()
    {
        _handler = new RefreshTokenCommandHandler(
            _refreshTokenRepositoryMock.Object,
            _identityServiceMock.Object,
            _jwtTokenGeneratorMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ValidRefreshToken_ShouldRotateTokensAndReturnNewPair()
    {
        // Arrange
        var rawOldToken = "valid_raw_old_token";
        var oldTokenHash = "old_token_hash_sha256";
        var newRawToken = "new_raw_token_512";
        var newTokenHash = "new_token_hash_sha256";
        var userId = "user-123";
        var email = "chef@example.com";
        var roles = new List<string> { AppRoles.Author };

        var existingToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = oldTokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(3),
            RevokedAt = null,
            ReplacedByTokenHash = null
        };

        _jwtTokenGeneratorMock
            .Setup(x => x.HashRefreshToken(rawOldToken))
            .Returns(oldTokenHash);

        _refreshTokenRepositoryMock
            .Setup(x => x.GetByHashAsync(oldTokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingToken);

        _identityServiceMock
            .Setup(x => x.GetUserProfileByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfileResult(
                Succeeded: true,
                UserId: userId,
                Email: email,
                DisplayName: "Chef Nam",
                AvatarUrl: null,
                Bio: "Culinary explorer",
                EmailConfirmed: true,
                CreatedAt: DateTimeOffset.UtcNow.AddMonths(-1),
                Roles: roles,
                IsActive: true,
                IsLockedOut: false));

        _jwtTokenGeneratorMock
            .Setup(x => x.GenerateRefreshToken())
            .Returns((newRawToken, newTokenHash));

        _jwtTokenGeneratorMock
            .Setup(x => x.GenerateAccessToken(userId, email, roles))
            .Returns("new.jwt.access.token");

        _refreshTokenRepositoryMock
            .Setup(x => x.TryRotateRefreshTokenAsync(existingToken, It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new RefreshTokenCommand(rawOldToken, "127.0.0.1");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("new.jwt.access.token");
        result.RefreshToken.Should().Be(newRawToken);
        result.ExpiresIn.Should().Be(900);

        // Verify old token was revoked and points to new token hash
        existingToken.RevokedAt.Should().NotBeNull();
        existingToken.ReplacedByTokenHash.Should().Be(newTokenHash);

        // Verify repository atomic rotation was called with new token containing hash (not raw token)
        _refreshTokenRepositoryMock.Verify(x => x.TryRotateRefreshTokenAsync(
            existingToken,
            It.Is<RefreshToken>(t => t.UserId == userId && t.TokenHash == newTokenHash && t.TokenHash != newRawToken),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ConcurrentRotationRaceLost_ShouldThrowUnauthorizedException()
    {
        // Arrange: TryRotateRefreshTokenAsync returns false (another request already claimed the token)
        var rawOldToken = "valid_raw_old_token";
        var oldTokenHash = "old_token_hash_sha256";
        var userId = "user-123";
        var email = "chef@example.com";
        var roles = new List<string> { AppRoles.Author };

        var existingToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = oldTokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(3),
            RevokedAt = null,
            ReplacedByTokenHash = null
        };

        _jwtTokenGeneratorMock
            .Setup(x => x.HashRefreshToken(rawOldToken))
            .Returns(oldTokenHash);

        _refreshTokenRepositoryMock
            .Setup(x => x.GetByHashAsync(oldTokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingToken);

        _identityServiceMock
            .Setup(x => x.GetUserProfileByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfileResult(
                Succeeded: true,
                UserId: userId,
                Email: email,
                DisplayName: "Chef Nam",
                AvatarUrl: null,
                Bio: null,
                EmailConfirmed: true,
                CreatedAt: DateTimeOffset.UtcNow,
                Roles: roles,
                IsActive: true,
                IsLockedOut: false));

        _jwtTokenGeneratorMock
            .Setup(x => x.GenerateRefreshToken())
            .Returns(("newRawToken", "newTokenHash"));

        _jwtTokenGeneratorMock
            .Setup(x => x.GenerateAccessToken(userId, email, roles))
            .Returns("new.jwt.access.token");

        // Simulation: another thread won the race
        _refreshTokenRepositoryMock
            .Setup(x => x.TryRotateRefreshTokenAsync(existingToken, It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new RefreshTokenCommand(rawOldToken);

        // Act & Assert: Must throw UnauthorizedException
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UnknownToken_ShouldThrowUnauthorizedException()
    {
        // Arrange
        var rawToken = "unknown_token";
        _jwtTokenGeneratorMock
            .Setup(x => x.HashRefreshToken(rawToken))
            .Returns("unknown_hash");

        _refreshTokenRepositoryMock
            .Setup(x => x.GetByHashAsync("unknown_hash", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?)null);

        var command = new RefreshTokenCommand(rawToken);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ExpiredToken_ShouldThrowUnauthorizedException()
    {
        // Arrange
        var rawToken = "expired_raw_token";
        var tokenHash = "expired_hash";

        var expiredToken = new RefreshToken
        {
            UserId = "user-123",
            TokenHash = tokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            RevokedAt = null
        };

        _jwtTokenGeneratorMock
            .Setup(x => x.HashRefreshToken(rawToken))
            .Returns(tokenHash);

        _refreshTokenRepositoryMock
            .Setup(x => x.GetByHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredToken);

        var command = new RefreshTokenCommand(rawToken);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_TokenReuse_ShouldRevokeDescendantAndThrowUnauthorizedException()
    {
        // Arrange: Token was already rotated and has a descendant
        var rawReusedToken = "reused_raw_token";
        var reusedHash = "reused_token_hash";
        var descendantHash = "active_descendant_hash";

        var compromisedToken = new RefreshToken
        {
            UserId = "user-123",
            TokenHash = reusedHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(5),
            RevokedAt = DateTimeOffset.UtcNow.AddDays(-1), // already revoked
            ReplacedByTokenHash = descendantHash
        };

        var activeDescendant = new RefreshToken
        {
            UserId = "user-123",
            TokenHash = descendantHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(6),
            RevokedAt = null // currently active!
        };

        _jwtTokenGeneratorMock
            .Setup(x => x.HashRefreshToken(rawReusedToken))
            .Returns(reusedHash);

        _refreshTokenRepositoryMock
            .Setup(x => x.GetByHashAsync(reusedHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(compromisedToken);

        _refreshTokenRepositoryMock
            .Setup(x => x.GetByHashAsync(descendantHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeDescendant);

        var command = new RefreshTokenCommand(rawReusedToken);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _handler.Handle(command, CancellationToken.None));

        // Verify active descendant was revoked due to reuse detection
        activeDescendant.RevokedAt.Should().NotBeNull();
        _refreshTokenRepositoryMock.Verify(x => x.UpdateRefreshTokenAsync(
            activeDescendant, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_InactiveOrLockedUser_ShouldThrowUnauthorizedException()
    {
        // Arrange
        var rawToken = "raw_token";
        var tokenHash = "token_hash";
        var userId = "locked-user";

        var existingToken = new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(3),
            RevokedAt = null
        };

        _jwtTokenGeneratorMock
            .Setup(x => x.HashRefreshToken(rawToken))
            .Returns(tokenHash);

        _refreshTokenRepositoryMock
            .Setup(x => x.GetByHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingToken);

        _identityServiceMock
            .Setup(x => x.GetUserProfileByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfileResult(
                Succeeded: true,
                UserId: userId,
                Email: "locked@example.com",
                DisplayName: "Locked",
                AvatarUrl: null,
                Bio: null,
                EmailConfirmed: true,
                CreatedAt: DateTimeOffset.UtcNow,
                Roles: new List<string>(),
                IsActive: false, // Inactive user
                IsLockedOut: true));

        var command = new RefreshTokenCommand(rawToken);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }
}
