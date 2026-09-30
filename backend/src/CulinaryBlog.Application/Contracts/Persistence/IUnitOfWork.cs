namespace CulinaryBlog.Application.Contracts.Persistence;

/// <summary>
/// Unit of Work contract to coordinate transaction and database persistence.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
