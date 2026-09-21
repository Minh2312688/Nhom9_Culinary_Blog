using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.DTOs.Auth;
using CulinaryBlog.Domain.Constants;
using CulinaryBlog.Domain.Entities;
using MediatR;

namespace CulinaryBlog.Application.Features.Auth.GoogleLogin;

public sealed class GoogleLoginCommandHandler : IRequestHandler<GoogleLoginCommand, AuthResponseDto>
{
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IRefreshTokenRepository _refreshTokenRepository;

    public GoogleLoginCommandHandler(
        IIdentityService identityService,
        IJwtTokenGenerator jwtTokenGenerator,
        IRefreshTokenRepository refreshTokenRepository)
    {
        _identityService = identityService;
        _jwtTokenGenerator = jwtTokenGenerator;
        _refreshTokenRepository = refreshTokenRepository;
    }

    public async Task<AuthResponseDto> Handle(GoogleLoginCommand request, CancellationToken cancellationToken)
    {
        var authResult = await _identityService.AuthenticateGoogleUserAsync(
            request.IdToken,
            AppRoles.Author,
            cancellationToken);

        if (!authResult.Succeeded)
        {
            if (authResult.IsInvalidToken)
            {
                throw new InvalidGoogleTokenException("Invalid or expired Google token.");
            }

            throw new InvalidOperationException(authResult.ErrorMessage ?? "Google authentication failed.");
        }

        var roles = authResult.Roles ?? Array.Empty<string>();
        var accessToken = _jwtTokenGenerator.GenerateAccessToken(authResult.UserId!, authResult.Email!, roles);
        var (rawRefreshToken, tokenHash) = _jwtTokenGenerator.GenerateRefreshToken();

        var refreshToken = new RefreshToken
        {
            UserId = authResult.UserId!,
            TokenHash = tokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _refreshTokenRepository.SaveRefreshTokenAsync(refreshToken, cancellationToken);

        return new AuthResponseDto(accessToken, rawRefreshToken, 900);
    }
}
