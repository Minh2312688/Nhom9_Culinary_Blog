using System.Net;
using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.Contracts.Notifications;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Features.Auth.ForgotPassword;

public sealed class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand>
{
    private readonly IIdentityService _identityService;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<ForgotPasswordCommandHandler> _logger;

    public ForgotPasswordCommandHandler(
        IIdentityService identityService,
        IEmailSender emailSender,
        ILogger<ForgotPasswordCommandHandler> logger)
    {
        _identityService = identityService;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var result = await _identityService.GeneratePasswordResetTokenAsync(request.Email, cancellationToken);

        // Security: Always succeed outwardly to prevent email enumeration.
        if (string.IsNullOrWhiteSpace(result.ResetToken))
        {
            return;
        }

        try
        {
            var emailMessage = new EmailMessage(
                To: request.Email,
                Subject: "Đặt lại mật khẩu - Culinary Blog",
                HtmlBody: $"""
                    <h2>Yêu cầu đặt lại mật khẩu</h2>
                    <p>Chào bạn,</p>
                    <p>Hệ thống nhận được yêu cầu đặt lại mật khẩu cho tài khoản <strong>{WebUtility.HtmlEncode(request.Email)}</strong>.</p>
                    <p>Mã xác nhận đặt lại mật khẩu của bạn là:</p>
                    <p style="font-size: 16px; font-weight: bold; background: #f3f4f6; padding: 10px; border-radius: 6px;">{WebUtility.HtmlEncode(result.ResetToken)}</p>
                    <p>Mã này có hiệu lực trong thời gian giới hạn. Nếu bạn không yêu cầu, vui lòng bỏ qua email này.</p>
                    """,
                TextBody: $"Ma dat lai mat khau cho {request.Email}: {result.ResetToken}");

            await _emailSender.SendAsync(emailMessage, cancellationToken);
        }
        catch (Exception ex)
        {
            // Do not fail user request if mail server is unconfigured in development/test
            _logger.LogWarning(ex, "Could not send password reset email to recipient.");
        }
    }
}
