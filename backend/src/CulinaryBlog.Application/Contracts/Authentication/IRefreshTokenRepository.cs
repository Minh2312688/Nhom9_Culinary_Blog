using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Application.Contracts.Authentication;

public interface IRefreshTokenRepository
{
    Task SaveRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken = default);
}
