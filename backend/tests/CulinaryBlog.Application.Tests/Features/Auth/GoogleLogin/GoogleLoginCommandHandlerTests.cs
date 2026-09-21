using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.Features.Auth.GoogleLogin;
using CulinaryBlog.Domain.Constants;
using CulinaryBlog.Domain.Entities;
using FluentAssertions;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Auth.GoogleLogin;

public class GoogleLoginCommandHandlerTests
{
    private readonly Mock<IIdentityService> _identityServiceMock = new();
    private readonly Mock<IJwtTokenGenerator> _jwtTokenGeneratorMock = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock = new();
    private readonly GoogleLoginCommandHandler _handler;

    public GoogleLoginCommandHandlerTests()
    {
        _handler = new GoogleLoginCommandHandler(
            _identityServiceMock.Object,
            _jwtTokenGeneratorMock.Object,
            _refreshTokenRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_ValidToken_ShouldAuthenticateAndReturnTokens()
    {
        // Arrange
        var command = new GoogleLoginCommand("valid_google_id_token");
        var userId = Guid.NewGuid().ToString();
        var roles = new List<string> { AppRoles.Author };

        _identityServiceMock
            .Setup(x => x.AuthenticateGoogleUserAsync(command.IdToken, AppRoles.Author, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleAuthResult(
                Succeeded: true,
                UserId: userId,
                Email: "googleuser@gmail.com",
                Roles: roles));

        _jwtTokenGeneratorMock
            .Setup(x => x.GenerateAccessToken(userId, "googleuser@gmail.com", roles))
            .Returns("jwt.access.token");

        _jwtTokenGeneratorMock
            .Setup(x => x.GenerateRefreshToken())
            .Returns(("raw_refresh_token", "hash_refresh_token"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("jwt.access.token");
        result.RefreshToken.Should().Be("raw_refresh_token");
        result.ExpiresIn.Should().Be(900);

        _refreshTokenRepositoryMock.Verify(x => x.SaveRefreshTokenAsync(
            It.Is<RefreshToken>(t => t.UserId == userId && t.TokenHash == "hash_refresh_token"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_InvalidGoogleToken_ShouldThrowInvalidGoogleTokenException()
    {
        // Arrange
        var command = new GoogleLoginCommand("invalid_google_id_token");

        _identityServiceMock
            .Setup(x => x.AuthenticateGoogleUserAsync(command.IdToken, AppRoles.Author, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleAuthResult(
                Succeeded: false,
                UserId: null,
                Email: null,
                Roles: null,
                ErrorMessage: "Invalid Google token.",
                IsInvalidToken: true));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidGoogleTokenException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }
}
