using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AuthDbContext _context;

    public RefreshTokenRepository(AuthDbContext context)
    {
        _context = context;
    }

    public async Task SaveRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        await _context.RefreshTokens.AddAsync(token, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
    }

    public async Task UpdateRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        _context.RefreshTokens.Update(token);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static readonly object _inMemorySyncLock = new();

    public async Task RotateRefreshTokenAsync(RefreshToken oldToken, RefreshToken newToken, CancellationToken cancellationToken = default)
    {
        await TryRotateRefreshTokenAsync(oldToken, newToken, cancellationToken);
    }

    public async Task<bool> TryRotateRefreshTokenAsync(RefreshToken oldToken, RefreshToken newToken, CancellationToken cancellationToken = default)
    {
        var now = oldToken.RevokedAt ?? DateTimeOffset.UtcNow;
        var newHash = oldToken.ReplacedByTokenHash ?? newToken.TokenHash;

        if (_context.Database.IsNpgsql())
        {
            var trackedEntry = _context.ChangeTracker.Entries<RefreshToken>().FirstOrDefault(e => e.Entity.Id == oldToken.Id);
            if (trackedEntry != null)
            {
                trackedEntry.State = EntityState.Detached;
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // Atomic conditional update on PostgreSQL:
                // Only 1 concurrent request can match RevokedAt == null && ReplacedByTokenHash == null
                var affected = await _context.RefreshTokens
                    .Where(t => t.Id == oldToken.Id && t.RevokedAt == null && t.ReplacedByTokenHash == null)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(t => t.RevokedAt, now)
                        .SetProperty(t => t.ReplacedByTokenHash, newHash),
                        cancellationToken);

                if (affected != 1)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return false;
                }

                await _context.RefreshTokens.AddAsync(newToken, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                return true;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        else
        {
            // Non-relational (e.g. EF In-Memory test database):
            lock (_inMemorySyncLock)
            {
                _context.ChangeTracker.Clear();

                var existing = _context.RefreshTokens
                    .FirstOrDefault(t => t.Id == oldToken.Id && t.RevokedAt == null && t.ReplacedByTokenHash == null);

                if (existing == null)
                {
                    return false;
                }

                existing.RevokedAt = now;
                existing.ReplacedByTokenHash = newHash;
                _context.RefreshTokens.Add(newToken);
                _context.SaveChanges();
                return true;
            }
        }
    }
}
