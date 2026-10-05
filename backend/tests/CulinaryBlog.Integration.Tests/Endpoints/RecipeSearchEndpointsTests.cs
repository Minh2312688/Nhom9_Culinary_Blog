using System.Net;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Endpoints;

public sealed class RecipeSearchEndpointsTests : IClassFixture<CustomAuthWebApplicationFactory>
{
    private readonly CustomAuthWebApplicationFactory factory;

    public RecipeSearchEndpointsTests(CustomAuthWebApplicationFactory factory) => this.factory = factory;

    [Theory]
    [InlineData("maxCookTime=0")]
    [InlineData("maxCookTime=-1")]
    [InlineData("minServings=0")]
    [InlineData("minServings=-1")]
    [InlineData("difficulty=Expert")]
    public async Task GetRecipes_InvalidFilter_Returns400ProblemDetails(string filter)
    {
        var client = factory.CreateIsolatedClient();

        var response = await client.GetAsync($"/api/v1/recipes/?{filter}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task GetRecipes_SearchLongerThan100Characters_Returns400ProblemDetails()
    {
        var client = factory.CreateIsolatedClient();
        var query = new string('a', 101);

        var response = await client.GetAsync($"/api/v1/recipes/?search={query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.ToString());
    }
}