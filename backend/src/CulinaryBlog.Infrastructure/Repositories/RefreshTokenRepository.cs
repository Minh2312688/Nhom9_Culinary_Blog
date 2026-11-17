using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.Common.Contracts;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private static readonly SemaphoreSlim InMemoryRotationLock = new(1, 1);
    private readonly ApplicationDbContext _context;
    private readonly IAuditTrail _auditTrail;

    public RefreshTokenRepository(ApplicationDbContext context, IAuditTrail auditTrail)
    {
        _context = context;
        _auditTrail = auditTrail;
    }

    public async Task SaveRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.RefreshTokens.AddAsync(token, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            _auditTrail.LogSuccess("Auth.RefreshToken.Create", "RefreshToken", token.Id);
        }
        catch (OperationCanceledException exception)
        {
            _auditTrail.LogCanceled("Auth.RefreshToken.Create", "RefreshToken", token.Id, exception);
            throw;
        }
        catch (Exception exception)
        {
            _auditTrail.LogFailure("Auth.RefreshToken.Create", "RefreshToken", token.Id, exception);
            throw;
        }
    }

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        _context.RefreshTokens.SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public async Task UpdateRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        try
        {
            _context.RefreshTokens.Update(token);
            await _context.SaveChangesAsync(cancellationToken);
            _auditTrail.LogSuccess("Auth.RefreshToken.Update", "RefreshToken", token.Id);
        }
        catch (OperationCanceledException exception)
        {
            _auditTrail.LogCanceled("Auth.RefreshToken.Update", "RefreshToken", token.Id, exception);
            throw;
        }
        catch (Exception exception)
        {
            _auditTrail.LogFailure("Auth.RefreshToken.Update", "RefreshToken", token.Id, exception);
            throw;
        }
    }

    public async Task<bool> TryRotateRefreshTokenAsync(RefreshToken current, RefreshToken replacement, CancellationToken cancellationToken = default)
    {
        const string action = "Auth.RefreshToken.Rotate";
        try
        {
            var rotated = await TryRotateRefreshTokenCoreAsync(current, replacement, cancellationToken);
            if (rotated)
            {
                _auditTrail.LogSuccess(action, "RefreshToken", current.Id);
            }
            else
            {
                _auditTrail.LogFailure(
                    action,
                    "RefreshToken",
                    current.Id,
                    new InvalidOperationException("Refresh token rotation was not applied."));
            }

            return rotated;
        }
        catch (OperationCanceledException exception)
        {
            _auditTrail.LogCanceled(action, "RefreshToken", current.Id, exception);
            throw;
        }
        catch (Exception exception)
        {
            _auditTrail.LogFailure(action, "RefreshToken", current.Id, exception);
            throw;
        }
    }

    private async Task<bool> TryRotateRefreshTokenCoreAsync(RefreshToken current, RefreshToken replacement, CancellationToken cancellationToken)
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
