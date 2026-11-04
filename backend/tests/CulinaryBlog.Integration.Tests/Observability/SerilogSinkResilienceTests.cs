using System.Collections.Generic;
using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Observability;

/// <summary>
/// ST-01: the host must start and serve requests with the Seq sink unset and
/// with it pointing at an unreachable endpoint (closed port). A configured but
/// unreachable Seq must never fail startup or requests (§0.5), and the
/// collecting sink registered through ReadFrom.Services must still receive
/// events, proving the rest of the pipeline is intact.
/// </summary>
[Collection("SerilogSinks")]
public sealed class SerilogSinkResilienceTests : IClassFixture<SerilogSinkResilienceTests.SeqUnsetFactory>
{
    private readonly SeqUnsetFactory seqUnsetFactory;

    public SerilogSinkResilienceTests(SeqUnsetFactory seqUnsetFactory)
    {
        this.seqUnsetFactory = seqUnsetFactory;
    }

    public sealed class SeqUnsetFactory : SerilogWebApplicationFactory
    {
        public SeqUnsetFactory()
        {
            // Seq:ServerUrl deliberately left unset (default).
        }

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Serilog:MinimumLevel"] = "Debug"
                });
            });
        }
    }

    private sealed class SeqClosedPortFactory : SerilogWebApplicationFactory
    {
        public SeqClosedPortFactory()
        {
            SeqServerUrl = "http://127.0.0.1:1";
        }

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Serilog:MinimumLevel"] = "Debug"
                });
            });
        }
    }

    [Fact]
    public async Task Host_WithSeqUnset_StartsAndReceivesEvents()
    {
        var client = seqUnsetFactory.CreateClient();
        seqUnsetFactory.Sink.Clear();

        var response = await client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await WaitForEventsAsync(seqUnsetFactory.Sink)).Should().BeTrue(
            "the collecting sink registered in DI must still receive events");
    }

    [Fact]
    public async Task Host_WithSeqOnClosedPort_StartsServesAndStillCollects()
    {
        using var selfLog = new StringWriter();
        Serilog.Debugging.SelfLog.Enable(selfLog);
        try
        {
            using var factory = new SeqClosedPortFactory();
            var client = factory.CreateClient();
            factory.Sink.Clear();

            var response = await client.GetAsync("/health/live");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var received = await WaitForEventsAsync(factory.Sink, seconds: 15);
            received.Should().BeTrue(
                "an unreachable Seq must not break the other sinks; SelfLog: {0}",
                selfLog.ToString());

            var second = await client.GetAsync("/health/live");
            second.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            Serilog.Debugging.SelfLog.Disable();
        }
    }

    private static async Task<bool> WaitForEventsAsync(CollectingLogSink sink, int seconds = 5)
    {
        // Events are emitted asynchronously relative to the HTTP response
        // completion; poll briefly instead of asserting immediately.
        for (var attempt = 0; attempt < seconds * 10; attempt++)
        {
            if (sink.Events.Count > 0)
            {
                return true;
            }

            await Task.Delay(100);
        }

        return sink.Events.Count > 0;
    }
}
