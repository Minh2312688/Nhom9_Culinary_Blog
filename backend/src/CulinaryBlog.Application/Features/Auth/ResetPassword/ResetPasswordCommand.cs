using MediatR;

namespace CulinaryBlog.Application.Features.Auth.ResetPassword;

public sealed record ResetPasswordCommand(
    string Email,
    string Token,
    string NewPassword,
    string ConfirmPassword) : IRequest;
