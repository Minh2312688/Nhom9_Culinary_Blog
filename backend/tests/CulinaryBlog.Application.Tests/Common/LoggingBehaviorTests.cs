using CulinaryBlog.Application.Common.Behaviors;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CulinaryBlog.Application.Tests.Common;

public sealed class ListLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Entries.Add((logLevel, formatter(state, exception)));
    }
}

public class LoggingBehaviorWarningTests
{
    [Fact]
    public async Task LoggingBehavior_Over500Ms_LogsExactlyOneWarning()
    {
        var logger = new ListLogger<LoggingBehavior<TestPingCommand, string>>();
        var behavior = new LoggingBehavior<TestPingCommand, string>(logger);
        var command = new TestPingCommand("test");

        var result = await behavior.Handle(command, async _ =>
        {
            await Task.Delay(600);
            return "pong";
        }, CancellationToken.None);

        Assert.Equal("pong", result);
        Assert.Equal(1, logger.Entries.Count(e => e.Level == LogLevel.Warning));
    }

    [Fact]
    public async Task LoggingBehavior_Under500Ms_LogsNoWarning()
    {
        var logger = new ListLogger<LoggingBehavior<TestPingCommand, string>>();
        var behavior = new LoggingBehavior<TestPingCommand, string>(logger);
        var command = new TestPingCommand("test");

        var result = await behavior.Handle(command, _ => Task.FromResult("pong"), CancellationToken.None);

        Assert.Equal("pong", result);
        Assert.Equal(0, logger.Entries.Count(e => e.Level == LogLevel.Warning));
    }

    [Fact]
    public async Task LoggingBehavior_HandlerThrows_RethrowsAndLogsOneErrorWithFailure()
    {
        var logger = new ListLogger<LoggingBehavior<TestPingCommand, string>>();
        var behavior = new LoggingBehavior<TestPingCommand, string>(logger);
        var command = new TestPingCommand("test");

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await behavior.Handle(command, _ => throw new InvalidOperationException("boom"), CancellationToken.None));

        var errors = logger.Entries.Where(e => e.Level == LogLevel.Error).ToList();
        Assert.Single(errors);
        Assert.Contains("Failure", errors[0].Message);
    }
}
