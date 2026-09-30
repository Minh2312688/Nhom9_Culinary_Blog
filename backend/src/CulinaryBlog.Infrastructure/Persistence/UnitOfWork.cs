using CulinaryBlog.Application.Contracts.Persistence;

namespace CulinaryBlog.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core implementation of Unit of Work pattern using AuthDbContext.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly AuthDbContext _context;

    public UnitOfWork(AuthDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
