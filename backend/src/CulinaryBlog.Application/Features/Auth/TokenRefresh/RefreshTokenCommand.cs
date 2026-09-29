using CulinaryBlog.Application.DTOs.Auth;
using MediatR;

namespace CulinaryBlog.Application.Features.Auth.TokenRefresh;

public sealed record RefreshTokenCommand(string RefreshToken, string? IpAddress = null) : IRequest<AuthResponseDto>;
