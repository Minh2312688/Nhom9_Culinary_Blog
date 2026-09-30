using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.Contracts.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Features.Auth.Logout;

public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ILogger<LogoutCommandHandler> _logger;
    private readonly IUnitOfWork _unitOfWork;

    public LogoutCommandHandler(
        IRefreshTokenRepository refreshTokenRepository,
        IJwtTokenGenerator jwtTokenGenerator,
        ILogger<LogoutCommandHandler> logger,
        IUnitOfWork unitOfWork)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _jwtTokenGenerator = jwtTokenGenerator;
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var incomingHash = _jwtTokenGenerator.HashRefreshToken(request.RefreshToken);
        var existingToken = await _refreshTokenRepository.GetByHashAsync(incomingHash, cancellationToken);

        // IDEMPOTENCY:
        // 1. If token is not found -> return normally (HTTP 204)
        if (existingToken == null)
        {
            return;
        }

        // 2. If token belongs to another user -> DO NOT revoke other user's token. Return normally without leaking status.
        if (existingToken.UserId != request.CurrentUserId)
        {
            _logger.LogWarning("Security Warning: User {CurrentUserId} attempted to logout token belonging to another user.", request.CurrentUserId);
            return;
        }

        // 3. If token is not revoked -> mark revoked. If already revoked, return normally.
        if (existingToken.RevokedAt == null)
        {
            existingToken.Revoke(DateTimeOffset.UtcNow);
            _refreshTokenRepository.Update(existingToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
