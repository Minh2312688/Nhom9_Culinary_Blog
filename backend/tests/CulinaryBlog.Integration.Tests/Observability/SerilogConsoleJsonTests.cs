using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Observability;

/// <summary>
/// ST-03: the Console sink writes JSON. Console output is redirected with
/// Console.SetOut BEFORE the host is built (xUnit already redirects the console
/// for captured output, but we replace it again to own the buffer), then one
/// captured line must parse as JSON. Defined in the ConsoleOutput collection
/// with parallelization disabled, because Console is process-global.
/// </summary>
[CollectionDefinition("ConsoleOutput", DisableParallelization = true)]
public sealed class ConsoleOutputCollection
{
}

/// <summary>
/// Sink tests share process-global state (Serilog's static Log.Logger, the
/// console, environment variables), so they must not run in parallel.
/// </summary>
[CollectionDefinition("SerilogSinks", DisableParallelization = true)]
public sealed class SerilogSinksCollection
{
}

[Collection("ConsoleOutput")]
public sealed class SerilogConsoleJsonTests : IDisposable
{
    private readonly TextWriter originalOut;
    private readonly StringWriter capturedOut;

    public SerilogConsoleJsonTests()
    {
        originalOut = Console.Out;
        capturedOut = new StringWriter();
        Console.SetOut(capturedOut);
    }

    public void Dispose()
    {
        Console.SetOut(originalOut);
        capturedOut.Dispose();
    }

    [Fact]
    public async Task ConsoleSink_WritesParsableJsonLines()
    {
        using var factory = new SerilogWebApplicationFactory { RegisterSink = false };
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");
        response.EnsureSuccessStatusCode();

        // Flush pending events; the Console sink writes asynchronously at times,
        // so poll the captured buffer briefly.
        string captured = string.Empty;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            captured = capturedOut.ToString();
            if (captured.Split('\n').Any(line =>
                    line.TrimStart().StartsWith("{")))
            {
                break;
            }

            await Task.Delay(100);
        }

        var jsonLines = captured
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        jsonLines.Should().NotBeEmpty(
            "the Console sink must write to the redirected console writer");

        jsonLines.Count(line =>
        {
            try
            {
                JsonSerializer.Deserialize<JsonElement>(line);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }).Should().BeGreaterThan(0, "at least one captured console line must parse as JSON");
    }
}
