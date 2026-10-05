namespace CulinaryBlog.Application.Contracts.Persistence;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    void SetOriginalRowVersion<T>(T entity, byte[] rowVersion) where T : class;
}
