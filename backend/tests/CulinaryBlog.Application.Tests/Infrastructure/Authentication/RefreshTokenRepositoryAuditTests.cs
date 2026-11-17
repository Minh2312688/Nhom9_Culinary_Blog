using CulinaryBlog.Application.Common.Contracts;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.Application.Tests.Infrastructure.Authentication;

public sealed class RefreshTokenRepositoryAuditTests
{
    [Fact]
    public async Task SaveRefreshTokenAsync_LogsSuccessfulWriteWithoutTokenPayload()
    {
        await using var context = CreateContext();
        var audit = new RecordingAuditTrail();
        var repository = new RefreshTokenRepository(context, audit);
        var token = CreateToken();

        await repository.SaveRefreshTokenAsync(token);

        var entry = Assert.Single(audit.Successes);
        Assert.Equal("Auth.RefreshToken.Create", entry.Action);
        Assert.Equal("RefreshToken", entry.EntityType);
        Assert.Equal(token.Id, entry.EntityId);
        Assert.Empty(audit.Failures);
    }

    [Fact]
    public async Task SaveRefreshTokenAsync_LogsFailureAndRethrowsOriginalException()
    {
        await using var context = CreateContext();
        var audit = new RecordingAuditTrail();
        var repository = new RefreshTokenRepository(context, audit);
        await context.DisposeAsync();
        var token = CreateToken();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => repository.SaveRefreshTokenAsync(token));

        var entry = Assert.Single(audit.Failures);
        Assert.Equal("Auth.RefreshToken.Create", entry.Action);
        Assert.Equal(nameof(ObjectDisposedException), entry.ExceptionType);
    }

    private static ApplicationDbContext CreateContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static RefreshToken CreateToken()
        => new()
        {
            UserId = "user-1",
            TokenHash = "hash-1",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };

    private sealed class RecordingAuditTrail : IAuditTrail
    {
        public List<(string Action, string EntityType, Guid? EntityId)> Successes { get; } = [];
        public List<(string Action, string EntityType, Guid? EntityId, string ExceptionType)> Failures { get; } = [];

        public void LogSuccess(string action, string entityType, Guid? entityId)
            => Successes.Add((action, entityType, entityId));

        public void LogFailure(string action, string entityType, Guid? entityId, Exception exception)
            => Failures.Add((action, entityType, entityId, exception.GetType().Name));

        public void LogCanceled(string action, string entityType, Guid? entityId, OperationCanceledException exception)
        {
        }
    }
}
