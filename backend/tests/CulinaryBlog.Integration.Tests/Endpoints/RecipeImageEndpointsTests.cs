using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CulinaryBlog.Application.DTOs.Auth;
using CulinaryBlog.Application.DTOs.Recipes;
using CulinaryBlog.Domain.Entities;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Endpoints;

public sealed class RecipeImageEndpointsTests : IClassFixture<CustomAuthWebApplicationFactory>
{
    private readonly CustomAuthWebApplicationFactory factory;

    public RecipeImageEndpointsTests(CustomAuthWebApplicationFactory factory) => this.factory = factory;

    [Fact]
    public async Task ImageMutationRoutes_RequireAuthentication()
    {
        var client = factory.CreateIsolatedClient();
        var recipeId = Guid.NewGuid();
        var imageId = Guid.NewGuid();
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([0xff, 0xd8, 0xff]), "file", "image.jpg");

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsync($"/api/v1/recipes/{recipeId}/images", form)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PatchAsync($"/api/v1/recipes/{recipeId}/images/{imageId}/primary", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.DeleteAsync($"/api/v1/recipes/{recipeId}/images/{imageId}")).StatusCode);
    }

    [Fact]
    public async Task Upload_InvalidPrimaryFlag_Returns400ProblemDetails()
    {
        var client = await CreateAuthenticatedClientAsync();
        var recipeId = Guid.NewGuid();
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([0xff, 0xd8, 0xff]), "file", "image.jpg");
        form.Add(new StringContent("sometimes"), "isPrimary");

        var response = await client.PostAsync($"/api/v1/recipes/{recipeId}/images", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.ToString());
        Assert.Contains("isPrimary", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Upload_WithStepNumber_AssociatesImageWithRecipeStep()
    {
        await using var testFactory = new CategoryWebApplicationFactory();
        var recipeId = Guid.NewGuid();

        await testFactory.SeedAsync(async db =>
        {
            var category = Category.Create("Test category");
            db.Categories.Add(category);
            var recipe = new Recipe
            {
                Id = recipeId,
                Title = "Recipe with step images",
                Slug = "recipe-with-step-images",
                CategoryId = category.Id,
                AuthorId = "test-author",
                Difficulty = RecipeDifficulty.Easy,
                Servings = 2
            };
            recipe.Steps.Add(new RecipeStep
            {
                StepNumber = 1,
                Description = "Prepare ingredients."
            });
            recipe.Steps.Add(new RecipeStep
            {
                StepNumber = 2,
                Description = "Prepare ingredients."
            });
            db.Recipes.Add(recipe);
            await db.SaveChangesAsync();
        });

        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([0xff, 0xd8, 0xff]), "file", "step-image.jpg");
        form.Add(new StringContent("2"), "stepNumber");
        var response = await testFactory.CreateClientAs("Author")
            .PostAsync($"/api/v1/recipes/{recipeId}/images", form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var image = await response.Content.ReadFromJsonAsync<RecipeImageUploadDto>();
        Assert.NotNull(image);
        Assert.Equal(2, image.StepNumber);
        Assert.False(image.IsPrimary);
        Assert.Equal(2, testFactory.Query(db => db.RecipeImages.Single().StepNumber));
    }

    [Fact]
    public async Task Upload_WithUnknownStepNumber_Returns400WithoutSavingImage()
    {
        await using var testFactory = new CategoryWebApplicationFactory();
        var recipeId = Guid.NewGuid();

        await testFactory.SeedAsync(async db =>
        {
            var category = Category.Create("Test category");
            db.Categories.Add(category);
            var recipe = new Recipe
            {
                Id = recipeId,
                Title = "Recipe with one step",
                Slug = "recipe-with-one-step",
                CategoryId = category.Id,
                AuthorId = "test-author",
                Difficulty = RecipeDifficulty.Easy,
                Servings = 2
            };
            recipe.Steps.Add(new RecipeStep
            {
                StepNumber = 1,
                Description = "Prepare ingredients."
            });
            db.Recipes.Add(recipe);
            await db.SaveChangesAsync();
        });

        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([0xff, 0xd8, 0xff]), "file", "step-image.jpg");
        form.Add(new StringContent("2"), "stepNumber");
        var response = await testFactory.CreateClientAs("Author")
            .PostAsync($"/api/v1/recipes/{recipeId}/images", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(testFactory.Query(db => db.RecipeImages.ToArray()));
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = factory.CreateIsolatedClient();
        var email = $"image_{Guid.NewGuid():N}@example.com";
        const string password = "P@ssword123";
        var register = await client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequestDto(email, password, "Image Tester"));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = await login.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(tokens);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        return client;
    }
}