using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.Features.Auth.Logout;
using CulinaryBlog.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Auth.Logout;

public class LogoutCommandHandlerTests
{
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock = new();
    private readonly Mock<IJwtTokenGenerator> _jwtTokenGeneratorMock = new();
    private readonly Mock<ILogger<LogoutCommandHandler>> _loggerMock = new();
    private readonly LogoutCommandHandler _handler;

    public LogoutCommandHandlerTests()
    {
        _handler = new LogoutCommandHandler(
            _refreshTokenRepositoryMock.Object,
            _jwtTokenGeneratorMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ValidTokenBelongingToCurrentUser_ShouldRevokeToken()
    {
        // Arrange
        var rawToken = "raw_logout_token";
        var tokenHash = "token_hash_sha256";
        var currentUserId = "user-123";

        var existingToken = new RefreshToken
        {
            UserId = currentUserId,
            TokenHash = tokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            RevokedAt = null
        };

        _jwtTokenGeneratorMock
            .Setup(x => x.HashRefreshToken(rawToken))
            .Returns(tokenHash);

        _refreshTokenRepositoryMock
            .Setup(x => x.GetByHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingToken);

        var command = new LogoutCommand(rawToken, currentUserId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        existingToken.RevokedAt.Should().NotBeNull();
        _refreshTokenRepositoryMock.Verify(x => x.UpdateRefreshTokenAsync(
            existingToken, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UnknownToken_ShouldCompleteWithoutError_Idempotent()
    {
        // Arrange
        var rawToken = "non_existent_token";
        var tokenHash = "non_existent_hash";

        _jwtTokenGeneratorMock
            .Setup(x => x.HashRefreshToken(rawToken))
            .Returns(tokenHash);

        _refreshTokenRepositoryMock
            .Setup(x => x.GetByHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?)null);

        var command = new LogoutCommand(rawToken, "user-123");

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert: must succeed with no exception
        await act.Should().NotThrowAsync();
        _refreshTokenRepositoryMock.Verify(x => x.UpdateRefreshTokenAsync(
            It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AlreadyRevokedToken_ShouldCompleteWithoutError_Idempotent()
    {
        // Arrange
        var rawToken = "already_revoked_token";
        var tokenHash = "already_revoked_hash";
        var currentUserId = "user-123";

        var existingToken = new RefreshToken
        {
            UserId = currentUserId,
            TokenHash = tokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(5),
            RevokedAt = DateTimeOffset.UtcNow.AddDays(-1) // already revoked
        };

        _jwtTokenGeneratorMock
            .Setup(x => x.HashRefreshToken(rawToken))
            .Returns(tokenHash);

        _refreshTokenRepositoryMock
            .Setup(x => x.GetByHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingToken);

        var command = new LogoutCommand(rawToken, currentUserId);

        // Act & Assert
        var act = async () => await _handler.Handle(command, CancellationToken.None);
        await act.Should().NotThrowAsync();
        _refreshTokenRepositoryMock.Verify(x => x.UpdateRefreshTokenAsync(
            It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_TokenBelongingToAnotherUser_ShouldNotRevokeToken()
    {
        // Arrange
        var rawToken = "victim_token";
        var tokenHash = "victim_token_hash";
        var attackerUserId = "attacker-999";
        var victimUserId = "victim-111";

        var victimToken = new RefreshToken
        {
            UserId = victimUserId,
            TokenHash = tokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            RevokedAt = null
        };

        _jwtTokenGeneratorMock
            .Setup(x => x.HashRefreshToken(rawToken))
            .Returns(tokenHash);

        _refreshTokenRepositoryMock
            .Setup(x => x.GetByHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(victimToken);

        var command = new LogoutCommand(rawToken, attackerUserId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert: Victim's token MUST NOT be revoked!
        victimToken.RevokedAt.Should().BeNull();
        _refreshTokenRepositoryMock.Verify(x => x.UpdateRefreshTokenAsync(
            It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
