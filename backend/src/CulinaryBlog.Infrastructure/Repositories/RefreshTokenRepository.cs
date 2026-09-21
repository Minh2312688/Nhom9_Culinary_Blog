using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;

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
}
