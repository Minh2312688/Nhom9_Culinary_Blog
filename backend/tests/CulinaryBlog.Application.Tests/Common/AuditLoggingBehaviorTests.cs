using System.Linq;
using CulinaryBlog.Application.Common.Behaviors;
using CulinaryBlog.Application.Common.Contracts;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Features.Recipes.Commands;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests.Common;

public class AuditLoggingBehaviorTests
{
    private sealed class TestQuery : IRequest<string>
    {
    }

    [Fact]
    public async Task AuditCommand_SuccessfulHandler_LogsExactlyOneAuditWithSuccessOutcome()
    {
        var userMock = new Mock<ICurrentUserService>();
        userMock.Setup(u => u.UserId).Returns("user-123");
        var logger = new ListLogger<AuditLoggingBehavior<PublishRecipeCommand, Unit>>();
        var behavior = new AuditLoggingBehavior<PublishRecipeCommand, Unit>(logger, userMock.Object);
        var command = new PublishRecipeCommand(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

        await behavior.Handle(command, _ => Task.FromResult(Unit.Value), CancellationToken.None);

        var auditLogs = logger.Entries
            .Where(e => e.Message.StartsWith("Audit", StringComparison.Ordinal))
            .ToList();

        Assert.Single(auditLogs);
        var log = auditLogs[0];
        Assert.Contains("Recipe.Publish", log.Message);
        Assert.Contains("Recipe", log.Message);
        Assert.Contains("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", log.Message);
        Assert.Contains("user-123", log.Message);
        Assert.Contains("Success", log.Message);
    }

    [Fact]
    public async Task AuditCommand_HandlerThrows_RethrowsAndDoesNotLogAudit()
    {
        var userMock = new Mock<ICurrentUserService>();
        userMock.Setup(u => u.UserId).Returns("user-456");
        var logger = new ListLogger<AuditLoggingBehavior<PublishRecipeCommand, Unit>>();
        var behavior = new AuditLoggingBehavior<PublishRecipeCommand, Unit>(logger, userMock.Object);
        var command = new PublishRecipeCommand(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await behavior.Handle(command, _ => throw new InvalidOperationException("boom"), CancellationToken.None));

        var auditLogs = logger.Entries
            .Where(e => e.Message.StartsWith("Audit", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(auditLogs);
    }

    [Fact]
    public async Task NonAuditCommandQuery_DoesNotLogAudit()
    {
        var userMock = new Mock<ICurrentUserService>();
        userMock.Setup(u => u.UserId).Returns("user-789");
        var logger = new ListLogger<AuditLoggingBehavior<TestQuery, string>>();
        var behavior = new AuditLoggingBehavior<TestQuery, string>(logger, userMock.Object);
        var query = new TestQuery();

        var result = await behavior.Handle(query, _ => Task.FromResult("pong"), CancellationToken.None);

        Assert.Equal("pong", result);
        var auditLogs = logger.Entries
            .Where(e => e.Message.StartsWith("Audit", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(auditLogs);
    }
}