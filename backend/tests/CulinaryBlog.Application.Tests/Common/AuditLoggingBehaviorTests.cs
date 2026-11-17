using System.Linq;
using CulinaryBlog.Application.Common.Behaviors;
using CulinaryBlog.Application.Common.Contracts;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Features.Auth.Logout;
using CulinaryBlog.Application.Features.Recipes.Commands;
using FluentValidation;
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

    private sealed record UnmarkedCommand(string Password) : IRequest<string>;

    private sealed class ThrowingLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            throw new InvalidOperationException("audit sink failure");
        }
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
        Assert.Contains(" at ", log.Message);
        Assert.Contains("Success", log.Message);
    }

    [Fact]
    public async Task UnmarkedCommand_SuccessfulHandler_LogsAuditWithoutRequestPayload()
    {
        var userMock = new Mock<ICurrentUserService>();
        userMock.Setup(u => u.UserId).Returns("user-987");
        var logger = new ListLogger<AuditLoggingBehavior<UnmarkedCommand, string>>();
        var behavior = new AuditLoggingBehavior<UnmarkedCommand, string>(logger, userMock.Object);
        var command = new UnmarkedCommand("do-not-log-this-password");

        var result = await behavior.Handle(command, _ => Task.FromResult("created"), CancellationToken.None);

        Assert.Equal("created", result);
        var auditLogs = logger.Entries.Where(e => e.Message.StartsWith("Audit", StringComparison.Ordinal)).ToList();
        Assert.Single(auditLogs);
        Assert.Contains("UnmarkedCommand", auditLogs[0].Message);
        Assert.Contains("user-987", auditLogs[0].Message);
        Assert.Contains("Success", auditLogs[0].Message);
        Assert.DoesNotContain(command.Password, auditLogs[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LogoutCommand_SuccessfulHandler_AuditsWithoutLoggingRefreshToken()
    {
        var userMock = new Mock<ICurrentUserService>();
        userMock.Setup(u => u.UserId).Returns("user-logout");
        var logger = new ListLogger<AuditLoggingBehavior<LogoutCommand, Unit>>();
        var behavior = new AuditLoggingBehavior<LogoutCommand, Unit>(logger, userMock.Object);
        var command = new LogoutCommand("secret-refresh-token", "user-logout");

        await behavior.Handle(command, _ => Task.FromResult(Unit.Value), CancellationToken.None);

        var auditLogs = logger.Entries.Where(e => e.Message.StartsWith("Audit", StringComparison.Ordinal)).ToList();
        Assert.Single(auditLogs);
        Assert.Contains("LogoutCommand", auditLogs[0].Message);
        Assert.Contains("user-logout", auditLogs[0].Message);
        Assert.DoesNotContain(command.RefreshToken, auditLogs[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuditCommand_HandlerThrows_LogsFailureWithoutExceptionMessageAndRethrows()
    {
        var userMock = new Mock<ICurrentUserService>();
        userMock.Setup(u => u.UserId).Returns("user-456");
        var logger = new ListLogger<AuditLoggingBehavior<PublishRecipeCommand, Unit>>();
        var behavior = new AuditLoggingBehavior<PublishRecipeCommand, Unit>(logger, userMock.Object);
        var command = new PublishRecipeCommand(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        var expectedException = new InvalidOperationException("do-not-log-this-detail");

        var actualException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await behavior.Handle(command, _ => throw expectedException, CancellationToken.None));

        Assert.Same(expectedException, actualException);
        var auditLogs = logger.Entries
            .Where(e => e.Message.StartsWith("Audit", StringComparison.Ordinal))
            .ToList();

        Assert.Single(auditLogs);
        Assert.Contains("Failure", auditLogs[0].Message);
        Assert.Contains(nameof(InvalidOperationException), auditLogs[0].Message);
        Assert.DoesNotContain("do-not-log-this-detail", auditLogs[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnmarkedCommand_ValidationFailure_LogsFailureWithoutRequestPayload()
    {
        var userMock = new Mock<ICurrentUserService>();
        userMock.Setup(u => u.UserId).Returns("user-654");
        var logger = new ListLogger<AuditLoggingBehavior<UnmarkedCommand, string>>();
        var behavior = new AuditLoggingBehavior<UnmarkedCommand, string>(logger, userMock.Object);
        var command = new UnmarkedCommand("secret-password");
        var exception = new ValidationException("Invalid command.");

        await Assert.ThrowsAsync<ValidationException>(async () =>
            await behavior.Handle(command, _ => throw exception, CancellationToken.None));

        var auditLogs = logger.Entries.Where(e => e.Message.StartsWith("Audit", StringComparison.Ordinal)).ToList();
        Assert.Single(auditLogs);
        Assert.Contains("Failure", auditLogs[0].Message);
        Assert.Contains(nameof(ValidationException), auditLogs[0].Message);
        Assert.DoesNotContain(command.Password, auditLogs[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnmarkedCommand_CanceledHandler_LogsCanceledAndRethrows()
    {
        var userMock = new Mock<ICurrentUserService>();
        userMock.Setup(u => u.UserId).Returns("user-321");
        var logger = new ListLogger<AuditLoggingBehavior<UnmarkedCommand, string>>();
        var behavior = new AuditLoggingBehavior<UnmarkedCommand, string>(logger, userMock.Object);
        var command = new UnmarkedCommand("password");
        var exception = new OperationCanceledException();

        var actualException = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await behavior.Handle(command, _ => throw exception, CancellationToken.None));

        Assert.Same(exception, actualException);
        var auditLogs = logger.Entries.Where(e => e.Message.StartsWith("Audit", StringComparison.Ordinal)).ToList();
        Assert.Single(auditLogs);
        Assert.Contains("Canceled", auditLogs[0].Message);
    }

    [Fact]
    public async Task AuditLoggerThrows_HandlerExceptionRemainsTheExceptionRethrown()
    {
        var userMock = new Mock<ICurrentUserService>();
        var logger = new ThrowingLogger<AuditLoggingBehavior<UnmarkedCommand, string>>();
        var behavior = new AuditLoggingBehavior<UnmarkedCommand, string>(logger, userMock.Object);
        var handlerException = new InvalidOperationException("handler failure");

        var actualException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await behavior.Handle(new UnmarkedCommand("password"), _ => throw handlerException, CancellationToken.None));

        Assert.Same(handlerException, actualException);
        Assert.Equal("InvalidOperationException", handlerException.Data["AuditLoggingFailureType"]);
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