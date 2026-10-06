using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private static readonly SemaphoreSlim InMemoryRotationLock = new(1, 1);
    private readonly ApplicationDbContext _context;

    public RefreshTokenRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task SaveRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        await _context.RefreshTokens.AddAsync(token, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        _context.RefreshTokens.SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public async Task UpdateRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        _context.RefreshTokens.Update(token);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TryRotateRefreshTokenAsync(RefreshToken current, RefreshToken replacement, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(
                _context.Database.ProviderName,
                "Npgsql.EntityFrameworkCore.PostgreSQL",
                StringComparison.Ordinal))
        {
            _context.Entry(current).State = EntityState.Detached;
            await InMemoryRotationLock.WaitAsync(cancellationToken);
            try
            {
                var stored = await _context.RefreshTokens.SingleOrDefaultAsync(
                    token => token.Id == current.Id &&
                        token.RevokedAt == null &&
                        token.ExpiresAt > DateTimeOffset.UtcNow,
                    cancellationToken);
                if (stored is null) return false;

                stored.RevokedAt = current.RevokedAt;
                stored.ReplacedByTokenHash = current.ReplacedByTokenHash;
                await _context.RefreshTokens.AddAsync(replacement, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                return true;
            }
            finally
            {
                InMemoryRotationLock.Release();
            }
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        _context.Entry(current).State = EntityState.Detached;
        var updated = await _context.RefreshTokens
            .Where(token => token.Id == current.Id &&
                token.RevokedAt == null &&
                token.ExpiresAt > DateTimeOffset.UtcNow)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(token => token.RevokedAt, current.RevokedAt)
                .SetProperty(token => token.ReplacedByTokenHash, current.ReplacedByTokenHash),
                cancellationToken);

        if (updated != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await _context.RefreshTokens.AddAsync(replacement, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
