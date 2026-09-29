using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.Features.Auth.Profile;
using CulinaryBlog.Domain.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Auth.Profile;

public class GetCurrentUserQueryHandlerTests
{
    private readonly Mock<IIdentityService> _identityServiceMock = new();
    private readonly GetCurrentUserQueryHandler _handler;

    public GetCurrentUserQueryHandlerTests()
    {
        _handler = new GetCurrentUserQueryHandler(_identityServiceMock.Object);
    }

    [Fact]
    public async Task Handle_UserExists_ShouldReturnUserProfileDto_WithoutSensitiveData()
    {
        // Arrange
        var userId = "user-123";
        var email = "chef@example.com";
        var displayName = "Master Chef";
        var roles = new List<string> { AppRoles.Author };
        var createdAt = DateTimeOffset.UtcNow.AddMonths(-2);

        _identityServiceMock
            .Setup(x => x.GetUserProfileByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfileResult(
                Succeeded: true,
                UserId: userId,
                Email: email,
                DisplayName: displayName,
                AvatarUrl: "https://example.com/avatar.jpg",
                Bio: "Love cooking Vietnamese food",
                EmailConfirmed: true,
                CreatedAt: createdAt,
                Roles: roles,
                IsActive: true,
                IsLockedOut: false));

        var query = new GetCurrentUserQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(userId);
        result.Email.Should().Be(email);
        result.DisplayName.Should().Be(displayName);
        result.AvatarUrl.Should().Be("https://example.com/avatar.jpg");
        result.Bio.Should().Be("Love cooking Vietnamese food");
        result.EmailConfirmed.Should().BeTrue();
        result.CreatedAt.Should().Be(createdAt);
        result.Roles.Should().Contain(AppRoles.Author);
    }

    [Fact]
    public async Task Handle_UserNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var userId = "non-existent-user";

        _identityServiceMock
            .Setup(x => x.GetUserProfileByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProfileResult?)null);

        var query = new GetCurrentUserQuery(userId);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(query, CancellationToken.None));
    }
}
