using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Persistence;

public class UnitOfWorkIntegrationTests
{
    [Fact]
    public async Task SaveChangesAsync_ShouldPersistChangesToDatabase()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(databaseName: $"UowIntegrationDb_{Guid.NewGuid():N}")
            .Options;

        await using var dbContext = new AuthDbContext(options);
        var unitOfWork = new UnitOfWork(dbContext);

        var token = new RefreshToken
        {
            UserId = "user-uow-integration",
            TokenHash = "uow-hash-integration-999",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow
        };

        await dbContext.RefreshTokens.AddAsync(token);

        // Act
        var affectedRows = await unitOfWork.SaveChangesAsync();

        // Assert
        affectedRows.Should().BeGreaterThan(0);
        var savedToken = await dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.UserId == "user-uow-integration");
        savedToken.Should().NotBeNull();
        savedToken!.TokenHash.Should().Be("uow-hash-integration-999");
    }
}
