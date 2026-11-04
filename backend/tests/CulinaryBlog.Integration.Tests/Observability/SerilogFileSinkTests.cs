using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Observability;

/// <summary>
/// ST-02: with a temporary test-owned Serilog:FilePath, every line of the file
/// parses as JSON (Serilog does not write its own diagnostics into the file)
/// and the request event contains RequestPath, HttpMethod, StatusCode and
/// Elapsed (ms) under Properties. With Serilog:FilePath unset no file is
/// created. TraceId / SpanId are asserted in OT-01, not here.
/// </summary>
[Collection("SerilogSinks")]
public sealed class SerilogFileSinkTests : IDisposable
{
    private readonly string tempDirectory;

    public SerilogFileSinkTests()
    {
        tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "serilog-st02-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup; a locked file from a still-open sink must not fail the test.
        }
    }

    private string FilePath => Path.Combine(tempDirectory, "log-.json");

    [Fact]
    public async Task FileSink_WithFilePathSet_WritesJsonLinesIncludingRequestEvent()
    {
        using var selfLog = new StringWriter();
        Serilog.Debugging.SelfLog.Enable(selfLog);
        try
        {
            using var factory = new SerilogWebApplicationFactory { SerilogFilePath = FilePath };
            using var host = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Serilog:MinimumLevel"] = "Debug"
                    });
                });
            });
            var client = host.CreateClient();

            var response = await client.GetAsync("/health/live");
            response.EnsureSuccessStatusCode();

            var logFile = await WaitForFileAsync();
            if (logFile is null)
            {
                throw new Xunit.Sdk.XunitException(
                    $"File sink never created the log file in {tempDirectory}. " +
                    $"Files present: [{string.Join(", ", Directory.GetFiles(tempDirectory, "*", SearchOption.AllDirectories))}]. " +
                    $"SelfLog: {selfLog}");
            }

            var createdFiles = Directory.GetFiles(tempDirectory, "*.json");
            createdFiles.Should().ContainSingle();
            var actualFileName = Path.GetFileName(createdFiles.Single());
            var expectedDate = DateTime.Now.ToString("yyyyMMdd");
            actualFileName.Should().Contain(expectedDate);

            await AssertFileContentAsync(logFile);
        }
        finally
        {
            Serilog.Debugging.SelfLog.Disable();
        }
    }

    private static async Task AssertFileContentAsync(string logFile)
    {
        // The sink keeps the file open while the host runs; open with
        // FileShare.ReadWrite so reading never fights the writer.
        List<string> lines = new();
        for (var attempt = 0; attempt < 50; attempt++)
        {
            lines.Clear();
            await using (var stream = new FileStream(
                logFile!, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                while (await reader.ReadLineAsync() is { } line)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        lines.Add(line);
                    }
                }
            }

            if (lines.Count > 0)
            {
                break;
            }

            await Task.Delay(100);
        }

        foreach (var line in lines)
        {
            var act = () => JsonSerializer.Deserialize<JsonElement>(line);
            act.Should().NotThrow(
                "every line the File sink writes must be a complete JSON event");
        }

        // Request-summary event (checklist ST-02), written by
        // UseSerilogRequestLogging — task S-06. Identification must NOT rely on
        // RequestPath alone: ASP.NET Core's own "Request starting" hosting event
        // also carries RequestPath. The request-summary event is uniquely
        // identified by the property set that only it carries: the pairing of
        // StatusCode and Elapsed (ms) with HttpMethod.
        JsonElement summaryProps = default;
        string? summaryLine = null;
        foreach (var line in lines)
        {
            using var doc = JsonDocument.Parse(line);
            if (!doc.RootElement.TryGetProperty("Properties", out var props))
            {
                continue;
            }

            if (props.TryGetProperty("StatusCode", out _) &&
                props.TryGetProperty("Elapsed", out _) &&
                props.TryGetProperty("HttpMethod", out _) &&
                props.TryGetProperty("RequestPath", out _))
            {
                summaryProps = props.Clone();
                summaryLine = line;
                break;
            }
        }

        // Failure here BEFORE S-06 is the expected, explicit outcome: the
        // summary event does not exist yet. Assertions below stay unchanged;
        // rerun ST-02 unchanged once S-06 lands.
        summaryProps.ValueKind.Should().Be(
            JsonValueKind.Object,
            "the UseSerilogRequestLogging (S-06) request-summary event carrying " +
            "RequestPath/HttpMethod/StatusCode/Elapsed must reach the File sink; " +
            "template of the event that carried RequestPath instead: {0}",
            lines.FirstOrDefault(line =>
                line.Contains("\"RequestPath\"", StringComparison.Ordinal)) ?? "<none>");
        if (summaryProps.ValueKind == JsonValueKind.Object)
        {
            summaryProps.GetProperty("RequestPath").GetString().Should().Be("/health/live");
            summaryProps.GetProperty("HttpMethod").GetString().Should().Be("GET");
            summaryLine.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task FileSink_WithFilePathUnset_CreatesNoFile()
    {
        using var factory = new SerilogWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");
        response.EnsureSuccessStatusCode();

        // Give any (incorrectly configured) File sink time to flush before asserting absence.
        await Task.Delay(500);

        Directory.GetFiles(tempDirectory, "*", SearchOption.AllDirectories)
            .Should().BeEmpty("no File sink may exist without Serilog:FilePath");
    }

    [Fact]
    public async Task FileSink_ProbeFails_SkipsSinkServesRequestsAndDoesNotCrash()
    {
        // Make the probe fail without touching ACLs: occupy the would-be log
        // directory's path with a regular FILE, so Directory.CreateDirectory
        // inside SerilogSetup throws IOException. The host must skip the File
        // sink, serve requests, and stay up (no crash).
        var blockedDirectory = Path.Combine(tempDirectory, "blocked");
        await File.WriteAllTextAsync(blockedDirectory, "a plain file blocks the directory path");

        using var factory = new SerilogWebApplicationFactory
        {
            SerilogFilePath = Path.Combine(blockedDirectory, "log-.json")
        };
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "a failed File-sink probe must never crash the host");
        Directory.GetFiles(tempDirectory, "*.json", SearchOption.AllDirectories)
            .Should().BeEmpty("no log file may be written when the probe fails");
    }

    private async Task<string?> WaitForFileAsync()
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var directory = Path.GetDirectoryName(FilePath)!;
            if (Directory.Exists(directory))
            {
                // RollingInterval.Day appends a yyyyMMdd date suffix inside the file name.
                var files = Directory.GetFiles(
                    directory,
                    Path.GetFileNameWithoutExtension(FilePath) + "*.json");
                if (files.Length > 0)
                {
                    return files[0];
                }
            }

            await Task.Delay(100);
        }

        return null;
    }
}
