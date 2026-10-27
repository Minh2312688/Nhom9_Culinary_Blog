using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.Contracts.Notifications;
using CulinaryBlog.Application.Features.Auth.ForgotPassword;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Auth.ForgotPassword;

public class ForgotPasswordTests
{
    private readonly Mock<IIdentityService> _identityServiceMock = new();
    private readonly Mock<IEmailSender> _emailSenderMock = new();
    private readonly Mock<ILogger<ForgotPasswordCommandHandler>> _loggerMock = new();
    private readonly ForgotPasswordCommandHandler _handler;
    private readonly ForgotPasswordCommandValidator _validator = new();

    public ForgotPasswordTests()
    {
        _handler = new ForgotPasswordCommandHandler(
            _identityServiceMock.Object,
            _emailSenderMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public void Validator_ValidEmail_ShouldPassValidation()
    {
        var command = new ForgotPasswordCommand("chef@example.com");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("@missing-user.com")]
    public void Validator_InvalidEmail_ShouldFailValidation(string email)
    {
        var command = new ForgotPasswordCommand(email);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_UserExists_ShouldGenerateTokenAndSendEmail()
    {
        var command = new ForgotPasswordCommand("chef@example.com");

        _identityServiceMock
            .Setup(x => x.GeneratePasswordResetTokenAsync("chef@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ForgotPasswordResult(true, "sample-reset-token-123"));

        _emailSenderMock
            .Setup(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _handler.Handle(command, CancellationToken.None);

        _emailSenderMock.Verify(
            x => x.SendAsync(
                It.Is<EmailMessage>(m => m.To == "chef@example.com" && m.HtmlBody.Contains("sample-reset-token-123")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_UserDoesNotExist_ShouldNotSendEmailAndNotThrow()
    {
        var command = new ForgotPasswordCommand("unknown@example.com");

        _identityServiceMock
            .Setup(x => x.GeneratePasswordResetTokenAsync("unknown@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ForgotPasswordResult(true, null));

        await _handler.Handle(command, CancellationToken.None);

        _emailSenderMock.Verify(
            x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_EmailSenderThrows_ShouldCatchAndNotThrowToCaller()
    {
        var command = new ForgotPasswordCommand("chef@example.com");

        _identityServiceMock
            .Setup(x => x.GeneratePasswordResetTokenAsync("chef@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ForgotPasswordResult(true, "reset-token"));

        _emailSenderMock
            .Setup(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP connection refused"));

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
