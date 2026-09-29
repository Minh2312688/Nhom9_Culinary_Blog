using CulinaryBlog.Application.Contracts.Persistence;

namespace CulinaryBlog.Infrastructure.Persistence;

public sealed class UnitOfWork(IApplicationDbContext context) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    public void SetOriginalRowVersion<T>(T entity, byte[] rowVersion) where T : class =>
        context.SetOriginalRowVersion(entity, rowVersion);
}
