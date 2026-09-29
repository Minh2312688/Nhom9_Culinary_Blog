using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Application.Contracts.Authentication;

public interface IRefreshTokenRepository
{
    Task SaveRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken = default);

    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task UpdateRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken = default);

    Task RotateRefreshTokenAsync(RefreshToken oldToken, RefreshToken newToken, CancellationToken cancellationToken = default);

    Task<bool> TryRotateRefreshTokenAsync(RefreshToken oldToken, RefreshToken newToken, CancellationToken cancellationToken = default);
}
