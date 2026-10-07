using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.DTOs.Auth;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Features.Auth.TokenRefresh;

public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResponseDto>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        IRefreshTokenRepository refreshTokenRepository,
        IIdentityService identityService,
        IJwtTokenGenerator jwtTokenGenerator,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _identityService = identityService;
        _jwtTokenGenerator = jwtTokenGenerator;
        _logger = logger;
    }

    public async Task<AuthResponseDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var incomingHash = _jwtTokenGenerator.HashRefreshToken(request.RefreshToken);
        var existingToken = await _refreshTokenRepository.GetByHashAsync(incomingHash, cancellationToken);

        if (existingToken == null)
        {
            throw new UnauthorizedException("Invalid or expired refresh token.");
        }

        // REUSE DETECTION: Token was already revoked or replaced
        if (existingToken.RevokedAt != null || !string.IsNullOrEmpty(existingToken.ReplacedByTokenHash))
        {
            var prefix = incomingHash.Length > 8 ? incomingHash[..8] : incomingHash;
            _logger.LogWarning("Security Warning: Refresh token reuse detected for TokenHash prefix {TokenHashPrefix}", prefix);

            // NFR-SEC-002: Invalidate ALL active descendants in the compromised token family lineage
            var revocationTime = DateTimeOffset.UtcNow;
            var currentDescendantHash = existingToken.ReplacedByTokenHash;
            var visitedHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { incomingHash };

            while (!string.IsNullOrEmpty(currentDescendantHash) && visitedHashes.Add(currentDescendantHash))
            {
                var descendant = await _refreshTokenRepository.GetByHashAsync(currentDescendantHash, cancellationToken);
                if (descendant == null)
                {
                    break;
                }

                if (descendant.RevokedAt == null)
                {
                    descendant.RevokedAt = revocationTime;
                    await _refreshTokenRepository.UpdateRefreshTokenAsync(descendant, cancellationToken);
                    _logger.LogWarning("Security Action: Compromised descendant token revoked due to rotation reuse.");
                }

                currentDescendantHash = descendant.ReplacedByTokenHash;
            }

            throw new UnauthorizedException("Invalid or expired refresh token.");
        }

        // Check expiration
        if (DateTimeOffset.UtcNow >= existingToken.ExpiresAt)
        {
            throw new UnauthorizedException("Invalid or expired refresh token.");
        }

        // Check user existence and activity
        var userProfile = await _identityService.GetUserProfileByIdAsync(existingToken.UserId, cancellationToken);
        if (userProfile == null || !userProfile.IsActive || userProfile.IsLockedOut)
        {
            throw new UnauthorizedException("User account is inactive or locked.");
        }

        // Generate new Access and Refresh tokens
        var roles = userProfile.Roles ?? Array.Empty<string>();
        var newAccessToken = _jwtTokenGenerator.GenerateAccessToken(userProfile.UserId!, userProfile.Email!, roles);
        var (newRawRefreshToken, newRefreshTokenHash) = _jwtTokenGenerator.GenerateRefreshToken();

        // Mark old token revoked and record replacement
        existingToken.RevokedAt = DateTimeOffset.UtcNow;
        existingToken.ReplacedByTokenHash = newRefreshTokenHash;

        var newRefreshTokenEntity = new RefreshToken
        {
            UserId = userProfile.UserId!,
            TokenHash = newRefreshTokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByIp = request.IpAddress
        };

        // Atomically update old token and insert new token
        var rotated = await _refreshTokenRepository.TryRotateRefreshTokenAsync(
            existingToken, newRefreshTokenEntity, cancellationToken);

        if (!rotated)
        {
            var prefix = incomingHash.Length > 8 ? incomingHash[..8] : incomingHash;
            _logger.LogWarning("Security Warning: Concurrent refresh race or reuse detected for TokenHash prefix {TokenHashPrefix}", prefix);
            throw new UnauthorizedException("Invalid or expired refresh token.");
        }

        // Access token TTL = 15 minutes = 900 seconds (CONS-004 / NFR-SEC-002)
        return new AuthResponseDto(newAccessToken, newRawRefreshToken, 900);
    }
}
