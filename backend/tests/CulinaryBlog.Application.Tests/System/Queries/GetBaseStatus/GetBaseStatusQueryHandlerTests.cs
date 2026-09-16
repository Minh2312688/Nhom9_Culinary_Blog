using CulinaryBlog.Application.System.Queries.GetBaseStatus;
using Xunit;

namespace CulinaryBlog.Application.Tests.System.Queries.GetBaseStatus;

public sealed class GetBaseStatusQueryHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReturnBaseStatusDto_WithExpectedProperties()
    {
        // Arrange
        var handler = new GetBaseStatusQueryHandler();
        var query = new GetBaseStatusQuery("Testing");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("CulinaryBlog.API", result.Application);
        Assert.Equal("Testing", result.Environment);
        Assert.True(result.TimestampUtc <= DateTimeOffset.UtcNow);
    }
}
