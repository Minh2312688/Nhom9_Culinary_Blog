using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.Features.Auth.Profile;
using FluentAssertions;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Auth.Profile;

public class UpdateProfileCommandHandlerTests
{
    private readonly Mock<IIdentityService> _identityServiceMock = new();
    private readonly UpdateProfileCommandHandler _handler;

    public UpdateProfileCommandHandlerTests()
    {
        _handler = new UpdateProfileCommandHandler(_identityServiceMock.Object);
    }

    [Fact]
    public async Task Handle_UserExists_ShouldUpdateOnlySuppliedFields()
    {
        // Arrange
        var userId = "user-123";
        var originalEmail = "chef@example.com";
        var originalAvatar = "https://example.com/original.jpg";
        var newDisplayName = "Updated Chef Nam";
        // Bio omitted (null) in command

        var command = new UpdateProfileCommand(userId, newDisplayName, null, null);

        var expectedResult = new UserProfileResult(
            Succeeded: true,
            UserId: userId,
            Email: originalEmail,
            DisplayName: newDisplayName,
            AvatarUrl: originalAvatar, // unchanged
            Bio: "Original bio",      // unchanged
            EmailConfirmed: true,
            CreatedAt: DateTimeOffset.UtcNow.AddMonths(-1),
            IsActive: true,
            IsLockedOut: false,
            Roles: new[] { "Author" });

        _identityServiceMock
            .Setup(x => x.UpdateUserProfileAsync(userId, newDisplayName, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.DisplayName.Should().Be(newDisplayName);
        result.AvatarUrl.Should().Be(originalAvatar);
        result.Bio.Should().Be("Original bio");
        result.Email.Should().Be(originalEmail);

        _identityServiceMock.Verify(x => x.UpdateUserProfileAsync(
            userId, newDisplayName, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UserNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var userId = "non-existent-user";
        var command = new UpdateProfileCommand(userId, "New Name", null, null);

        _identityServiceMock
            .Setup(x => x.UpdateUserProfileAsync(userId, "New Name", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProfileResult?)null);

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("User not found.");
    }
}
