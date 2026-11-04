using System.Diagnostics;
using System.Net;
using System.Net.Http;
using CulinaryBlog.Infrastructure.Sitemap;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Observability;

[Collection("OutboundHttpTracing")]
public sealed class OutboundHttpTracingTests
{
    private const string Sentinel = "SENTINEL-SITEMAP-123";
    private const string SitemapHost = "https://example.com";

    public sealed class OutboundHttpTracingFactory : WebApplicationFactory<Program>
    {
        public List<Activity> Activities { get; } = new();
        public FakeHttpMessageHandler FakeHandler { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((context, configuration) =>
            {
                var dict = new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Postgres"] = "Host=localhost;Port=5432;Database=observability_test;Username=test;Password=test",
                    ["Jwt:Key"] = "TestSecretKeyForIntegrationTestingOnly_MustBeAtLeast256Bits!",
                    ["Redis:ConnectionString"] = ""
                };
                configuration.AddInMemoryCollection(dict);
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<ApplicationDbContext>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase("OutboundTracingDb"));

                services.ConfigureOpenTelemetryTracerProvider(b => b.AddProcessor(new CollectingActivityProcessor(Activities)));

                services.AddHttpClient<GoogleSitemapPingClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => FakeHandler);
            });
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(config =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Postgres"] = "Host=localhost;Port=5432;Database=observability_test;Username=test;Password=test",
                    ["Jwt:Key"] = "TestSecretKeyForIntegrationTestingOnly_MustBeAtLeast256Bits!",
                    ["Redis:ConnectionString"] = ""
                });
            });
            return base.CreateHost(builder);
        }
    }

    public sealed class CollectingActivityProcessor : BaseProcessor<Activity>
    {
        private readonly List<Activity> _activities;
        public CollectingActivityProcessor(List<Activity> activities) { _activities = activities; }

        public override void OnEnd(Activity activity)
        {
            lock (_activities)
            {
                _activities.Add(activity);
            }

            base.OnEnd(activity);
        }
    }

    public sealed class FakeHttpMessageHandler : DelegatingHandler
    {
        public string? RecordedUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RecordedUri = request.RequestUri?.ToString() ?? string.Empty;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") });
        }
    }

    [Fact]
    public async Task OutboundClientActivity_DoesNotContainQueryStringSentinel()
    {
        using var factory = new OutboundHttpTracingFactory();
        var _ = factory.CreateClient();
        await Task.Delay(1500);

        var client = factory.Services.GetRequiredService<GoogleSitemapPingClient>();
        var sitemapUri = new Uri($"{SitemapHost}/site/{Sentinel}");

        await client.NotifyAsync(sitemapUri, cancellationToken: CancellationToken.None);

        Assert.NotNull(factory.FakeHandler.RecordedUri);
        Assert.Contains("sitemap=", factory.FakeHandler.RecordedUri);
        Assert.Contains(Sentinel, factory.FakeHandler.RecordedUri);

        var clientActivities = factory.Activities.Where(a => 
            a.Parent == null && a.Kind == ActivityKind.Client).ToList();
        Assert.NotEmpty(clientActivities);

        foreach (var activity in clientActivities)
        {
            foreach (var tag in activity.Tags)
            {
                AssertNoSentinel(tag.Value);
            }
        }
    }

    private static void AssertNoSentinel(object? value)
    {
        if (value == null) return;
        var str = value.ToString();
        if (string.IsNullOrEmpty(str)) return;
        Assert.DoesNotContain(Sentinel, str);
    }
}
