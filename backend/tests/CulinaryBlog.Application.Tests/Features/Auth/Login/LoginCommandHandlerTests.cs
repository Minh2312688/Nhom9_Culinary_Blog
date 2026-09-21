using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.Features.Auth.Login;
using CulinaryBlog.Domain.Constants;
using CulinaryBlog.Domain.Entities;
using FluentAssertions;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Auth.Login;

public class LoginCommandHandlerTests
{
    private readonly Mock<IIdentityService> _identityServiceMock = new();
    private readonly Mock<IJwtTokenGenerator> _jwtTokenGeneratorMock = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock = new();
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _handler = new LoginCommandHandler(
            _identityServiceMock.Object,
            _jwtTokenGeneratorMock.Object,
            _refreshTokenRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_ValidCredentials_ShouldReturnTokensAndPersistRefreshHash()
    {
        // Arrange
        var command = new LoginCommand("user@example.com", "P@ssword123");
        var userId = Guid.NewGuid().ToString();
        var roles = new List<string> { AppRoles.Author };

        _identityServiceMock
            .Setup(x => x.ValidateCredentialsAsync(command.Email, command.Password, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginResult(
                Succeeded: true,
                UserId: userId,
                Email: command.Email,
                Roles: roles));

        _jwtTokenGeneratorMock
            .Setup(x => x.GenerateAccessToken(userId, command.Email, roles))
            .Returns("valid.jwt.token");

        _jwtTokenGeneratorMock
            .Setup(x => x.GenerateRefreshToken())
            .Returns(("raw_refresh_token_string", "sha256_hashed_token_string"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("valid.jwt.token");
        result.RefreshToken.Should().Be("raw_refresh_token_string");
        result.ExpiresIn.Should().Be(900); // 15 minutes TTL

        // Verify refresh token was persisted with tokenHash (not raw)
        _refreshTokenRepositoryMock.Verify(x => x.SaveRefreshTokenAsync(
            It.Is<RefreshToken>(t => t.UserId == userId && t.TokenHash == "sha256_hashed_token_string"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_InvalidCredentials_ShouldThrowUnauthorizedException_WithGenericMessage()
    {
        // Arrange
        var command = new LoginCommand("wrong@example.com", "WrongPassword!");

        _identityServiceMock
            .Setup(x => x.ValidateCredentialsAsync(command.Email, command.Password, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginResult(
                Succeeded: false,
                UserId: null,
                Email: null,
                Roles: null,
                ErrorMessage: "Invalid email or password."));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _handler.Handle(command, CancellationToken.None));

        exception.Message.Should().Be("Invalid email or password.");

        _refreshTokenRepositoryMock.Verify(x => x.SaveRefreshTokenAsync(
            It.IsAny<RefreshToken>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_LockedAccount_ShouldThrowAccountLockedException()
    {
        // Arrange
        var command = new LoginCommand("locked@example.com", "P@ssword123");
        var lockoutEnd = DateTimeOffset.UtcNow.AddMinutes(15);

        _identityServiceMock
            .Setup(x => x.ValidateCredentialsAsync(command.Email, command.Password, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginResult(
                Succeeded: false,
                UserId: "locked-user-id",
                Email: command.Email,
                Roles: null,
                IsLockedOut: true,
                LockoutEnd: lockoutEnd,
                ErrorMessage: "Account is locked due to multiple failed login attempts."));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<AccountLockedException>(() =>
            _handler.Handle(command, CancellationToken.None));

        exception.LockoutEnd.Should().Be(lockoutEnd);

        _refreshTokenRepositoryMock.Verify(x => x.SaveRefreshTokenAsync(
            It.IsAny<RefreshToken>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
