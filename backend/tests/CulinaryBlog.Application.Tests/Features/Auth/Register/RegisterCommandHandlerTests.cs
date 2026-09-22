using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.Features.Auth.Register;
using CulinaryBlog.Domain.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Auth.Register;

public class RegisterCommandHandlerTests
{
    private readonly Mock<IIdentityService> _identityServiceMock = new();
    private readonly Mock<IWelcomeEmailEnqueuer> _welcomeEmailEnqueuerMock = new();
    private readonly RegisterCommandHandler _handler;

    public RegisterCommandHandlerTests()
    {
        _handler = new RegisterCommandHandler(
            _identityServiceMock.Object,
            _welcomeEmailEnqueuerMock.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldRegisterUser_AssignAuthorRole_AndEnqueueWelcomeEmail()
    {
        // Arrange
        var command = new RegisterCommand("newuser@example.com", "P@ssword123", "New User");
        var expectedUserId = Guid.NewGuid().ToString();

        _identityServiceMock
            .Setup(x => x.RegisterUserAsync(
                command.Email,
                command.Password,
                command.DisplayName,
                AppRoles.Author,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegisterResult(
                Succeeded: true,
                UserId: expectedUserId,
                Email: command.Email,
                DisplayName: command.DisplayName,
                ErrorMessage: null));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(expectedUserId);
        result.Email.Should().Be(command.Email);
        result.DisplayName.Should().Be(command.DisplayName);

        // Verify Author role was requested
        _identityServiceMock.Verify(x => x.RegisterUserAsync(
            command.Email,
            command.Password,
            command.DisplayName,
            AppRoles.Author,
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify welcome email enqueued (TV4 dependency)
        _welcomeEmailEnqueuerMock.Verify(x => x.EnqueueWelcomeEmailAsync(
            command.Email,
            command.DisplayName,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ShouldThrowConflictException()
    {
        // Arrange
        var command = new RegisterCommand("duplicate@example.com", "P@ssword123", "Duplicate User");

        _identityServiceMock
            .Setup(x => x.RegisterUserAsync(
                command.Email,
                command.Password,
                command.DisplayName,
                AppRoles.Author,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegisterResult(
                Succeeded: false,
                UserId: null,
                Email: null,
                DisplayName: null,
                ErrorMessage: "An account with this email already exists.",
                IsDuplicateEmail: true));

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() =>
            _handler.Handle(command, CancellationToken.None));

        _welcomeEmailEnqueuerMock.Verify(x => x.EnqueueWelcomeEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_FailedCreation_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var command = new RegisterCommand("fail@example.com", "P@ssword123", "Fail User");

        _identityServiceMock
            .Setup(x => x.RegisterUserAsync(
                command.Email,
                command.Password,
                command.DisplayName,
                AppRoles.Author,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegisterResult(
                Succeeded: false,
                UserId: null,
                Email: null,
                DisplayName: null,
                ErrorMessage: "Database error"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }
}
