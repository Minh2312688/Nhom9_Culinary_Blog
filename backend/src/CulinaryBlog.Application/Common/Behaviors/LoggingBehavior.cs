using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Common.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const long SlowThresholdMilliseconds = 500;

    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Handling {RequestName}", requestName);

        try
        {
            var response = await next(cancellationToken);
            stopwatch.Stop();

            if (stopwatch.ElapsedMilliseconds > SlowThresholdMilliseconds)
            {
                _logger.LogWarning(
                    "Handled {RequestName} in {ElapsedMilliseconds} ms ({Outcome})",
                    requestName,
                    stopwatch.ElapsedMilliseconds,
                    "Success");
            }
            else
            {
                _logger.LogInformation(
                    "Handled {RequestName} in {ElapsedMilliseconds} ms ({Outcome})",
                    requestName,
                    stopwatch.ElapsedMilliseconds,
                    "Success");
            }

            return response;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            _logger.LogError(
                exception,
                "Handled {RequestName} in {ElapsedMilliseconds} ms ({Outcome})",
                requestName,
                stopwatch.ElapsedMilliseconds,
                "Failure");
            throw;
        }
    }
}
