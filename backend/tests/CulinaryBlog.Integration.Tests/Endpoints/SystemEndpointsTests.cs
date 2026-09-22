using System.Net;
using System.Net.Http.Json;
using CulinaryBlog.Application.DTOs.System;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Endpoints;

public sealed class SystemEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SystemEndpointsTests(WebApplicationFactory<Program> factory)
    {
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
}
