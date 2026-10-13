using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.DTOs.Auth;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Integration.Tests.Endpoints;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Security;

/// <summary>
/// NFR-SEC-004 Integration Tests:
/// Verifies that HTTP API endpoints reject HTML markup before handlers execute,
/// returning HTTP 400 Problem Details according to API contracts,
/// while successfully accepting Vietnamese text and harmless comparison symbols.
/// </summary>
public class InputValidationSecurityIntegrationTests : IClassFixture<CustomAuthWebApplicationFactory>
{
    private readonly CustomAuthWebApplicationFactory _authFactory;

    public InputValidationSecurityIntegrationTests(CustomAuthWebApplicationFactory authFactory)
    {
        _authFactory = authFactory;
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("<svg onload=alert(1)>")]
    public async Task Register_WithHtmlMarkupDisplayName_ShouldReturn400ProblemDetails(string markup)
    {
        var client = _authFactory.CreateIsolatedClient();
        var request = new RegisterRequestDto(
            $"sec_reg_{Guid.NewGuid():N}@example.com",
            "P@ssword123",
            markup);

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("DisplayName");
        content.Should().Contain("must not contain HTML markup");
    }

    [Fact]
    public async Task Register_WithVietnameseNameAndComparisonSymbols_ShouldReturn201Created()
    {
        var client = _authFactory.CreateIsolatedClient();
        var request = new RegisterRequestDto(
            $"sec_reg_{Guid.NewGuid():N}@example.com",
            "P@ssword123",
            "Nguyễn Đầu Bếp <3");

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("<svg onload=alert(1)>")]
    public async Task PostCategory_WithHtmlMarkupDescription_ShouldReturn400ProblemDetails(string markup)
    {
        await using var catFactory = new CategoryWebApplicationFactory();
        var client = catFactory.CreateClientAs("Admin");

        var response = await client.PostAsJsonAsync(
            "/api/v1/categories",
            new { name = "Món Tráng Miệng", description = markup });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        root.GetProperty("status").GetInt32().Should().Be(400);
        root.GetProperty("errors").TryGetProperty("Description", out var descErrors).Should().BeTrue();
        descErrors.EnumerateArray().Should().NotBeEmpty();
    }

    [Fact]
    public async Task PostCategory_WithComparisonSymbolInDescription_ShouldReturn201Created()
    {
        await using var catFactory = new CategoryWebApplicationFactory();
        var client = catFactory.CreateClientAs("Admin");

        var response = await client.PostAsJsonAsync(
            "/api/v1/categories",
            new { name = "Món Ăn Nhanh", description = "Thời gian chế biến < 30 phút, phù hợp bữa tối <3" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CategoryDto>();
        body!.Description.Should().Be("Thời gian chế biến < 30 phút, phù hợp bữa tối <3");
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    public async Task PutCategory_WithHtmlMarkupDescription_ShouldReturn400ProblemDetails(string markup)
    {
        await using var catFactory = new CategoryWebApplicationFactory();
        await catFactory.SeedAsync(async context =>
        {
            context.Categories.Add(Category.Create("Khai Vị", "Mô tả ban đầu"));
            await context.SaveChangesAsync();
        });
        var id = catFactory.Query(context => context.Categories.Single().Id);
        var client = catFactory.CreateClientAs("Admin");

        var response = await client.PutAsJsonAsync(
            $"/api/v1/categories/{id}",
            new { name = "Khai Vị", description = markup, imageUrl = (string?)null, orderIndex = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        root.GetProperty("errors").TryGetProperty("Description", out var descErrors).Should().BeTrue();
        descErrors.EnumerateArray().Should().NotBeEmpty();
    }
}
