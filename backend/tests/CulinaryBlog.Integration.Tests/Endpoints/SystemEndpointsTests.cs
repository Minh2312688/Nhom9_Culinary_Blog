using System.Net;
using System.Net.Http.Json;
using CulinaryBlog.Application.DTOs.System;
using CulinaryBlog.Integration.Tests.Observability;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Endpoints;

public sealed class SystemEndpointsTests : IClassFixture<ObservabilityWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly ObservabilityWebApplicationFactory _factory;

    public SystemEndpointsTests(ObservabilityWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetRoot_ShouldReturnOk_WithRunningStatus()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetBaseStatus_ShouldReturnOk_WithBaseStatusDto()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/system/base-status");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<BaseStatusDto>();
        Assert.NotNull(body);
        Assert.Equal("CulinaryBlog.API", body.Application);
        Assert.False(string.IsNullOrWhiteSpace(body.Environment));
    }

    [Fact]
    public async Task GetHealthLive_ShouldReturnHealthy_WithNoCacheHeaders()
    {
        // Act
        var response = await _client.GetAsync("/health/live");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("Healthy", body);

        Assert.True(response.Headers.CacheControl?.NoStore ?? false);
        Assert.True(response.Headers.CacheControl?.NoCache ?? false);
    }

    [Fact]
    public async Task GetHealthLive_ShouldBeAnonymousTextAndNotCallDependencyProbes()
    {
        // Arrange
        _factory.ResetProbeCalls();

        // Act
        var response = await _client.GetAsync("/health/live");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-cache", response.Headers.Pragma.Single().Name);
        Assert.Equal(0, _factory.PostgresProbe.CallCount);
        Assert.Equal(0, _factory.RedisProbe.CallCount);
        Assert.Equal(0, _factory.MinioProbe.CallCount);
    }
}
