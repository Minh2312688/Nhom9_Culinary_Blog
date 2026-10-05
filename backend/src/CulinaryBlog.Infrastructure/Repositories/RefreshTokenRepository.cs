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

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        _context.RefreshTokens.SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public async Task UpdateRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        _context.RefreshTokens.Update(token);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TryRotateRefreshTokenAsync(RefreshToken current, RefreshToken replacement, CancellationToken cancellationToken = default)
    {
        var stored = await _context.RefreshTokens.SingleOrDefaultAsync(
            token => token.Id == current.Id && token.RevokedAt == null, cancellationToken);
        if (stored is null) return false;

        stored.RevokedAt = current.RevokedAt;
        stored.ReplacedByTokenHash = current.ReplacedByTokenHash;
        await _context.RefreshTokens.AddAsync(replacement, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
