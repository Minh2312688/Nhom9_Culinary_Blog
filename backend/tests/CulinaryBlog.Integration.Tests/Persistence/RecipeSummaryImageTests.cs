using System.Text.Json;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Features.Recipes.Queries;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Identity;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using ApplicationUser = CulinaryBlog.Infrastructure.Identity.ApplicationUser;

namespace CulinaryBlog.Integration.Tests.Persistence;

public sealed class RecipeSummaryImageTests
{
    [Fact]
    public async Task GetRecipes_includes_primary_image_url_and_alt_text_in_summary()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"RecipeSummaryImages_{Guid.NewGuid():N}")
            .Options;
        await using var context = new ApplicationDbContext(options);
        var author = new ApplicationUser
        {
            Id = "recipe-summary-author",
            UserName = "recipe-summary-author",
            Email = "recipe-summary-author@example.test",
            DisplayName = "Recipe Author"
        };
        var category = Category.Create("Test meals");
        var recipe = new Recipe
        {
            Title = "Test soup",
            Slug = "test-soup",
            Category = category,
            CategoryId = category.Id,
            AuthorId = author.Id,
            Status = RecipeStatus.Published,
            PublishedAt = DateTimeOffset.UtcNow
        };
        recipe.Images.Add(new RecipeImage
        {
            Recipe = recipe,
            OriginalUrl = "https://media.example.test/non-primary.webp",
            AltText = "Non-primary",
            IsPrimary = false,
            OrderIndex = 0
        });
        recipe.Images.Add(new RecipeImage
        {
            Recipe = recipe,
            OriginalUrl = "https://media.example.test/main.webp",
            AltText = "Main soup photo",
            IsPrimary = true,
            OrderIndex = 1
        });
        context.Users.Add(author);
        context.Categories.Add(category);
        context.Recipes.Add(recipe);
        await context.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        var handler = new GetRecipesQueryHandler(new RecipeRepository(context), currentUser.Object);

        var result = await handler.Handle(new GetRecipesQuery(), CancellationToken.None);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result.Items.Single(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var summary = json.RootElement;

        Assert.Equal("https://media.example.test/main.webp", summary.GetProperty("primaryImageUrl").GetString());
        Assert.Equal("Main soup photo", summary.GetProperty("primaryImageAltText").GetString());
    }
}
