using System.Buffers.Binary;
using CulinaryBlog.Application.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CulinaryBlog.Infrastructure.Persistence;

/// <summary>
/// Takes a PostgreSQL transaction-scoped advisory lock per recipe so concurrent step additions
/// and deletions observe a single ordered mutation at a time.
/// </summary>
public sealed class PostgresRecipeMutationLock(AuthDbContext context) : IRecipeMutationLock
{
    public async Task<IRecipeMutationLease> AcquireAsync(Guid recipeId, CancellationToken cancellationToken = default)
    {
        if (!context.Database.IsRelational())
            return NoOpRecipeMutationLease.Instance;

        var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var bytes = recipeId.ToByteArray();
        var lockHigh = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(0, 4));
        var lockLow = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4, 4));
        try
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({lockHigh}, {lockLow})", cancellationToken);
            return new RecipeMutationLease(transaction);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private sealed class RecipeMutationLease(IDbContextTransaction transaction) : IRecipeMutationLease
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }

    private sealed class NoOpRecipeMutationLease : IRecipeMutationLease
    {
        public static readonly NoOpRecipeMutationLease Instance = new();
        private NoOpRecipeMutationLease() { }
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
