using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.Features.Auth.ChangePassword;
using FluentAssertions;
using FluentValidation;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Auth.ChangePassword;

public class ChangePasswordTests
{
    private readonly Mock<IIdentityService> _identityServiceMock = new();
    private readonly ChangePasswordCommandHandler _handler;
    private readonly ChangePasswordCommandValidator _validator = new();

    public ChangePasswordTests()
    {
        _handler = new ChangePasswordCommandHandler(_identityServiceMock.Object);
    }

    [Fact]
    public void Validator_ValidCommand_ShouldPassValidation()
    {
        var command = new ChangePasswordCommand(
            "user-1",
            "OldPassword@123",
            "NewPassword@456",
            "NewPassword@456");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "OldPassword@123", "NewPassword@456", "NewPassword@456")] // Empty UserId
    [InlineData("user-1", "", "NewPassword@456", "NewPassword@456")] // Empty CurrentPassword
    [InlineData("user-1", "OldPassword@123", "short", "short")] // Too short
    [InlineData("user-1", "OldPassword@123", "nouppercase1!", "nouppercase1!")] // No uppercase
    [InlineData("user-1", "OldPassword@123", "NOLOWERCASE1!", "NOLOWERCASE1!")] // No lowercase
    [InlineData("user-1", "OldPassword@123", "NoDigitsHere!", "NoDigitsHere!")] // No digit
    [InlineData("user-1", "OldPassword@123", "NoSpecial123", "NoSpecial123")] // No special
    [InlineData("user-1", "SamePassword@123", "SamePassword@123", "SamePassword@123")] // Identical
    [InlineData("user-1", "OldPassword@123", "NewPassword@456", "MismatchPassword@456")] // Mismatch
    public void Validator_InvalidCommand_ShouldFailValidation(
        string userId,
        string currentPassword,
        string newPassword,
        string confirmPassword)
    {
        var command = new ChangePasswordCommand(userId, currentPassword, newPassword, confirmPassword);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ValidRequest_ShouldCallIdentityServiceSuccessfully()
    {
        var command = new ChangePasswordCommand(
            "user-1",
            "OldPassword@123",
            "NewPassword@456",
            "NewPassword@456");

        _identityServiceMock
            .Setup(x => x.ChangePasswordAsync("user-1", "OldPassword@123", "NewPassword@456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChangePasswordResult(true));

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().NotThrowAsync();
        _identityServiceMock.Verify(
            x => x.ChangePasswordAsync("user-1", "OldPassword@123", "NewPassword@456", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_IncorrectCurrentPassword_ShouldThrowValidationException()
    {
        var command = new ChangePasswordCommand(
            "user-1",
            "WrongPassword@123",
            "NewPassword@456",
            "NewPassword@456");

        _identityServiceMock
            .Setup(x => x.ChangePasswordAsync("user-1", "WrongPassword@123", "NewPassword@456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChangePasswordResult(false, "Password mismatch", IsIncorrectCurrentPassword: true));

        var act = () => _handler.Handle(command, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().Contain(e => e.PropertyName == "CurrentPassword");
    }

    [Fact]
    public async Task Handle_IdentityFailure_ShouldThrowValidationException()
    {
        var command = new ChangePasswordCommand(
            "user-1",
            "OldPassword@123",
            "NewPassword@456",
            "NewPassword@456");

        _identityServiceMock
            .Setup(x => x.ChangePasswordAsync("user-1", "OldPassword@123", "NewPassword@456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChangePasswordResult(false, "Password too weak according to identity policy", IsIncorrectCurrentPassword: false));

        var act = () => _handler.Handle(command, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().Contain(e => e.PropertyName == "NewPassword");
    }
}
