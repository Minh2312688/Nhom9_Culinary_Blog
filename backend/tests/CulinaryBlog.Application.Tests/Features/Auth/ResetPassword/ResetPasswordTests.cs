using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.Features.Auth.ResetPassword;
using FluentAssertions;
using FluentValidation;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Auth.ResetPassword;

public class ResetPasswordTests
{
    private readonly Mock<IIdentityService> _identityServiceMock = new();
    private readonly ResetPasswordCommandHandler _handler;
    private readonly ResetPasswordCommandValidator _validator = new();

    public ResetPasswordTests()
    {
        _handler = new ResetPasswordCommandHandler(_identityServiceMock.Object);
    }

    [Fact]
    public void Validator_ValidCommand_ShouldPassValidation()
    {
        var command = new ResetPasswordCommand(
            "chef@example.com",
            "valid-token-string",
            "NewPassword@123",
            "NewPassword@123");

        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "token", "NewPassword@123", "NewPassword@123")] // Empty email
    [InlineData("bad-email", "token", "NewPassword@123", "NewPassword@123")] // Bad email
    [InlineData("chef@example.com", "", "NewPassword@123", "NewPassword@123")] // Empty token
    [InlineData("chef@example.com", "token", "short", "short")] // Short password
    [InlineData("chef@example.com", "token", "nouppercase1!", "nouppercase1!")] // No uppercase
    [InlineData("chef@example.com", "token", "NewPassword@123", "DifferentPassword@123")] // Mismatch
    public void Validator_InvalidCommand_ShouldFailValidation(
        string email,
        string token,
        string newPassword,
        string confirmPassword)
    {
        var command = new ResetPasswordCommand(email, token, newPassword, confirmPassword);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ValidToken_ShouldCallIdentityServiceSuccessfully()
    {
        var command = new ResetPasswordCommand(
            "chef@example.com",
            "valid-token-string",
            "NewPassword@123",
            "NewPassword@123");

        _identityServiceMock
            .Setup(x => x.ResetPasswordAsync("chef@example.com", "valid-token-string", "NewPassword@123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResetPasswordResult(true));

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().NotThrowAsync();
        _identityServiceMock.Verify(
            x => x.ResetPasswordAsync("chef@example.com", "valid-token-string", "NewPassword@123", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_InvalidToken_ShouldThrowValidationException()
    {
        var command = new ResetPasswordCommand(
            "chef@example.com",
            "expired-or-invalid-token",
            "NewPassword@123",
            "NewPassword@123");

        _identityServiceMock
            .Setup(x => x.ResetPasswordAsync("chef@example.com", "expired-or-invalid-token", "NewPassword@123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResetPasswordResult(false, "Mã xác thực không hợp lệ hoặc đã hết hạn.", IsInvalidToken: true));

        var act = () => _handler.Handle(command, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().Contain(e => e.PropertyName == "Token");
    }
}
