using MediatR;

namespace CulinaryBlog.Application.Features.Auth.Logout;

public sealed record LogoutCommand(string RefreshToken, string CurrentUserId) : IRequest;
