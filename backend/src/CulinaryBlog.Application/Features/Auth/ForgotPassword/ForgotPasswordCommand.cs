using MediatR;

namespace CulinaryBlog.Application.Features.Auth.ForgotPassword;

public sealed record ForgotPasswordCommand(string Email) : IRequest;
