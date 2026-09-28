namespace CulinaryBlog.Application.Contracts;

/// <summary>Serializes changes to a recipe's ordered child rows in a database transaction.</summary>
public interface IRecipeMutationLock
{
    Task<IRecipeMutationLease> AcquireAsync(Guid recipeId, CancellationToken cancellationToken = default);
}

public interface IRecipeMutationLease : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
