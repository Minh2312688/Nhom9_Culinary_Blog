using System.Net;
using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.Contracts.Notifications;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Features.Auth.ForgotPassword;

public sealed class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand>
{
    private readonly IIdentityService _identityService;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<ForgotPasswordCommandHandler> _logger;
    private readonly IConfiguration? _configuration;

    public ForgotPasswordCommandHandler(
        IIdentityService identityService,
        IEmailSender emailSender,
        ILogger<ForgotPasswordCommandHandler> logger,
        IConfiguration? configuration = null)
    {
        _identityService = identityService;
        _emailSender = emailSender;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var result = await _identityService.GeneratePasswordResetTokenAsync(request.Email, cancellationToken);

        // Security: Always succeed outwardly to prevent email enumeration.
        if (string.IsNullOrWhiteSpace(result.ResetToken))
        {
            return;
        }

        var frontendBaseUrl = _configuration?["Frontend:BaseUrl"]
            ?? _configuration?["Sitemap:PublicBaseUrl"]
            ?? "http://localhost:3000";
        frontendBaseUrl = frontendBaseUrl.TrimEnd('/');

        var resetLink = $"{frontendBaseUrl}/auth/reset-password?email={Uri.EscapeDataString(request.Email)}&token={Uri.EscapeDataString(result.ResetToken)}";

        try
        {
            var emailMessage = new EmailMessage(
                To: request.Email,
                Subject: "Đặt lại mật khẩu - Culinary Blog",
                HtmlBody: $"""
                    <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; color: #333; line-height: 1.6;">
                        <h2 style="color: #ea580c; margin-bottom: 16px;">Yêu cầu đặt lại mật khẩu</h2>
                        <p>Chào bạn,</p>
                        <p>Hệ thống nhận được yêu cầu đặt lại mật khẩu cho tài khoản <strong>{WebUtility.HtmlEncode(request.Email)}</strong> tại Culinary Blog.</p>
                        <p>Vui lòng bấm vào nút bên dưới để tiến hành đặt lại mật khẩu của bạn:</p>
                        <div style="margin: 28px 0; text-align: center;">
                            <a href="{WebUtility.HtmlEncode(resetLink)}" style="background-color: #ea580c; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 8px; font-weight: bold; display: inline-block;">
                                Đặt lại mật khẩu
                            </a>
                        </div>
                        <p style="font-size: 13px; color: #666;">Hoặc bạn có thể sao chép liên kết này vào trình duyệt:</p>
                        <p style="font-size: 13px; word-break: break-all; background: #f3f4f6; padding: 10px; border-radius: 6px;"><a href="{WebUtility.HtmlEncode(resetLink)}" style="color: #ea580c;">{WebUtility.HtmlEncode(resetLink)}</a></p>
                        <p style="font-size: 13px; color: #666; margin-top: 20px;">Mã xác thực (Token) dự phòng của bạn:</p>
                        <p style="font-family: monospace; font-size: 14px; background: #f3f4f6; padding: 8px 12px; border-radius: 6px; word-break: break-all;">{WebUtility.HtmlEncode(result.ResetToken)}</p>
                        <hr style="border: none; border-top: 1px solid #e5e7eb; margin: 24px 0;" />
                        <p style="font-size: 12px; color: #9ca3af;">Liên kết này có hiệu lực trong vòng 24 giờ. Nếu bạn không gửi yêu cầu này, bạn có thể an tâm bỏ qua email này.</p>
                    </div>
                    """,
                TextBody: $"""
                    Yêu cầu đặt lại mật khẩu - Culinary Blog
                    Chào bạn, chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản {request.Email}.
                    Vui lòng truy cập liên kết sau để đặt lại mật khẩu:
                    {resetLink}

                    Mã xác thực (Token) dự phòng: {result.ResetToken}
                    """);

            await _emailSender.SendAsync(emailMessage, cancellationToken);
        }
        catch (Exception ex)
        {
            // Do not fail user request if mail server is unconfigured in development/test
            _logger.LogWarning(ex, "Could not send password reset email to recipient.");
        }
    }
}
