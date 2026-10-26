using MediatR;

namespace CulinaryBlog.Application.Features.Auth.ChangePassword;

public sealed record ChangePasswordCommand(
    string UserId,
    string CurrentPassword,
    string NewPassword,
    string ConfirmPassword) : IRequest;
