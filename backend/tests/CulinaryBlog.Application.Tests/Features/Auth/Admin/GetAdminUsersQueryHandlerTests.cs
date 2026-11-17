using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.Features.Auth.Admin;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Auth.Admin;

public sealed class GetAdminUsersQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsAllUsersWithRolesAndStatusMetadata()
    {
        var identity = new Mock<IIdentityService>();
        identity
            .Setup(service => service.GetAllUserProfilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new UserProfileResult(
                    true, "admin-1", "admin@example.com", "Admin", null, null,
                    true, DateTimeOffset.UtcNow, true, false, new List<string> { "Admin" }),
                new UserProfileResult(
                    true, "author-1", "author@example.com", "Author", null, null,
                    false, DateTimeOffset.UtcNow, true, true, new List<string> { "Author" })
            });

        var handler = new GetAdminUsersQueryHandler(identity.Object);
        var result = await handler.Handle(new GetAdminUsersQuery(), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("admin@example.com", result[0].Email);
        Assert.Equal(new[] { "Admin" }, result[0].Roles);
        Assert.True(result[0].IsActive);
        Assert.False(result[0].IsLockedOut);
        Assert.Equal(new[] { "Author" }, result[1].Roles);
        Assert.True(result[1].IsLockedOut);
    }
}
