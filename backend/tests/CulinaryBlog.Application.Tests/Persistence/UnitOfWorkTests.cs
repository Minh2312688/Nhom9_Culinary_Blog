using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.Features.Auth.Logout;
using CulinaryBlog.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests.Persistence;

public class UnitOfWorkTests
{
    [Fact]
    public async Task LogoutCommandHandler_WithUnitOfWork_ShouldCallSaveChangesAsyncWhenTokenRevoked()
    {
        // Arrange
        var repoMock = new Mock<IRefreshTokenRepository>();
        var jwtMock = new Mock<IJwtTokenGenerator>();
        var loggerMock = new Mock<ILogger<LogoutCommandHandler>>();
        var uowMock = new Mock<IUnitOfWork>();

        var rawToken = "raw-token";
        var tokenHash = "token-hash";
        var userId = "user-123";

        var existingToken = new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            RevokedAt = null
        };

        jwtMock.Setup(x => x.HashRefreshToken(rawToken)).Returns(tokenHash);
        repoMock.Setup(x => x.GetByHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingToken);
        uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new LogoutCommandHandler(repoMock.Object, jwtMock.Object, loggerMock.Object, uowMock.Object);
        var command = new LogoutCommand(rawToken, userId);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        existingToken.RevokedAt.Should().NotBeNull();
        repoMock.Verify(x => x.Update(existingToken), Times.Once);
        uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LogoutCommandHandler_WithUnitOfWork_ShouldNotCallSaveChangesAsyncWhenTokenNotFound()
    {
        // Arrange
        var repoMock = new Mock<IRefreshTokenRepository>();
        var jwtMock = new Mock<IJwtTokenGenerator>();
        var loggerMock = new Mock<ILogger<LogoutCommandHandler>>();
        var uowMock = new Mock<IUnitOfWork>();

        jwtMock.Setup(x => x.HashRefreshToken("unknown")).Returns("unknown-hash");
        repoMock.Setup(x => x.GetByHashAsync("unknown-hash", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?)null);

        var handler = new LogoutCommandHandler(repoMock.Object, jwtMock.Object, loggerMock.Object, uowMock.Object);
        var command = new LogoutCommand("unknown", "user-123");

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
