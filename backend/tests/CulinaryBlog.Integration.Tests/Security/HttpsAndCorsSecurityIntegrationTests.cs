using System.Net;
using CulinaryBlog.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Security;

public class CorsSecurityWebApplicationFactory : WebApplicationFactory<Program>
{
    static CorsSecurityWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("Jwt__Key", "TestSecretKeyForIntegrationTestingOnly_MustBeAtLeast256Bits!");
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", "Host=localhost;Port=5432;Database=dummy_test;Username=test;Password=test");
        Environment.SetEnvironmentVariable("Cors__AllowedOrigins__0", "http://localhost:3000");
        Environment.SetEnvironmentVariable("Cors__AllowedOrigins__1", "http://localhost:3001");
        Environment.SetEnvironmentVariable("Cors__AllowedOrigins__2", "https://culinaryblog.example.com");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            var dbContextOptionsDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (dbContextOptionsDescriptor != null)
            {
                services.Remove(dbContextOptionsDescriptor);
            }
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase($"CorsTestDb_{Guid.NewGuid():N}"));
        });
    }
}

/// <summary>
/// NFR-SEC-005 Integration Tests:
/// Verifies CORS allowed origins, rejected origins, preflight requests,
/// strict rejection of wildcards, and HSTS baseline options.
/// </summary>
public class HttpsAndCorsSecurityIntegrationTests : IClassFixture<CorsSecurityWebApplicationFactory>
{
    private readonly CorsSecurityWebApplicationFactory _factory;

    public HttpsAndCorsSecurityIntegrationTests(CorsSecurityWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Cors_AllowedOrigin_ShouldReturnAllowOriginHeader()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Origin", "http://localhost:3000");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeTrue();
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Contain("http://localhost:3000");
    }

    [Fact]
    public async Task Cors_UnauthorizedOrigin_ShouldNotReturnAllowOriginHeader()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Origin", "http://unauthorized-malicious-domain.com");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        if (response.Headers.Contains("Access-Control-Allow-Origin"))
        {
            response.Headers.GetValues("Access-Control-Allow-Origin")
                .Should().NotContain("http://unauthorized-malicious-domain.com");
        }
    }

    [Fact]
    public async Task Cors_PreflightOptionsRequest_AllowedOrigin_ShouldReturn204Or200WithHeaders()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        var response = await client.SendAsync(request);

        ((int)response.StatusCode).Should().BeOneOf(200, 204);
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeTrue();
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Contain("http://localhost:3000");
    }

    [Fact]
    public async Task Cors_ConfiguredProductionOrigin_ShouldBeAllowedAndOthersRejected()
    {
        var client = _factory.CreateClient();

        // Configured prod origin allowed
        var reqProd = new HttpRequestMessage(HttpMethod.Get, "/");
        reqProd.Headers.Add("Origin", "https://culinaryblog.example.com");
        var resProd = await client.SendAsync(reqProd);
        resProd.Headers.Contains("Access-Control-Allow-Origin").Should().BeTrue();
        resProd.Headers.GetValues("Access-Control-Allow-Origin").Should().Contain("https://culinaryblog.example.com");

        // Random external origin rejected
        var reqOther = new HttpRequestMessage(HttpMethod.Get, "/");
        reqOther.Headers.Add("Origin", "https://attacker.com");
        var resOther = await client.SendAsync(reqOther);
        if (resOther.Headers.Contains("Access-Control-Allow-Origin"))
        {
            resOther.Headers.GetValues("Access-Control-Allow-Origin").Should().NotContain("https://attacker.com");
        }
    }

    [Fact]
    public void Cors_WildcardOriginInConfiguration_ShouldThrowInvalidOperationException()
    {
        // Testing that configuring '*' violates SRS NFR-SEC-005
        var act = () =>
        {
            CulinaryBlog.Application.Common.Security.CorsPolicyValidator.ValidateAndFilterAllowedOrigins(
                new[] { "*" },
                isDevelopmentOrTesting: false);
        };

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Wildcard origins are strictly prohibited*");
    }

    [Fact]
    public void HstsOptions_VerificationOrBaselineEvaluation()
    {
        using var scope = _factory.Services.CreateScope();
        var hstsOptions = scope.ServiceProvider.GetService<IOptions<HstsOptions>>();

        hstsOptions.Should().NotBeNull();
        hstsOptions!.Value.MaxAge.TotalSeconds.Should().BeOneOf(31536000, 2592000);
    }
}
