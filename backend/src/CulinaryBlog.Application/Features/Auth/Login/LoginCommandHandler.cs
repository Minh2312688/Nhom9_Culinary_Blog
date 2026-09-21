using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.DTOs.Auth;
using CulinaryBlog.Domain.Entities;
using MediatR;

namespace CulinaryBlog.Application.Features.Auth.Login;

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResponseDto>
{
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IRefreshTokenRepository _refreshTokenRepository;

    public LoginCommandHandler(
        IIdentityService identityService,
        IJwtTokenGenerator jwtTokenGenerator,
        IRefreshTokenRepository refreshTokenRepository)
    {
        _identityService = identityService;
        _jwtTokenGenerator = jwtTokenGenerator;
        _refreshTokenRepository = refreshTokenRepository;
    }

    public async Task<AuthResponseDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var loginResult = await _identityService.ValidateCredentialsAsync(
            request.Email,
            request.Password,
            cancellationToken);

        if (loginResult.IsLockedOut)
        {
            throw new AccountLockedException(loginResult.LockoutEnd);
        }

        if (!loginResult.Succeeded)
        {
            throw new UnauthorizedException("Invalid email or password.");
        }

        var roles = loginResult.Roles ?? Array.Empty<string>();
        var accessToken = _jwtTokenGenerator.GenerateAccessToken(loginResult.UserId!, loginResult.Email!, roles);
        var (rawRefreshToken, tokenHash) = _jwtTokenGenerator.GenerateRefreshToken();

        var refreshToken = new RefreshToken
        {
            UserId = loginResult.UserId!,
            TokenHash = tokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _refreshTokenRepository.SaveRefreshTokenAsync(refreshToken, cancellationToken);

        // Access token TTL = 15 minutes = 900 seconds (CONS-004 / NFR-SEC-002)
        return new AuthResponseDto(accessToken, rawRefreshToken, 900);
    }
}
