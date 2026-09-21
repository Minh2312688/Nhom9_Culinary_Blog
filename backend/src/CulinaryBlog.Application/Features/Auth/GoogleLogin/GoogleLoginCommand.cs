using CulinaryBlog.Application.DTOs.Auth;
using MediatR;

namespace CulinaryBlog.Application.Features.Auth.GoogleLogin;

public sealed record GoogleLoginCommand(
    string IdToken) : IRequest<AuthResponseDto>;
