using System.Net;
using System.Net.Http.Json;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.DTOs.Recipes;
using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Endpoints;

public sealed class RecipePrivateDetailEndpointTests
{
    [Fact]
    public async Task UpdateRemovesNutrition_WhenNutritionIsNotIncluded_AndCanBeAddedAgain()
    {
        await using var factory = new CategoryWebApplicationFactory();
        var recipeId = Guid.NewGuid();
        var nutritionId = Guid.NewGuid();

        await factory.SeedAsync(async db =>
        {
            var category = Category.Create("Test category");
            db.Categories.Add(category);
            db.Recipes.Add(new Recipe
            {
                Id = recipeId,
                Title = "Recipe with nutrition",
                Slug = "recipe-with-nutrition",
                CategoryId = category.Id,
                AuthorId = "test-author",
                PrepTimeMinutes = 10,
                CookTimeMinutes = 20,
                Servings = 2,
                Difficulty = RecipeDifficulty.Easy,
                Nutrition = new RecipeNutrition
                {
                    Id = nutritionId,
                    Calories = 300,
                    Protein = 10,
                    Carbohydrates = 20,
                    Fat = 5,
                    Fiber = 3,
                    Sodium = 100
                }
            });
            await db.SaveChangesAsync();
        });

        var client = factory.CreateClientAs("Author");
        var endpoint = $"/api/v1/recipes/{recipeId}";
        var detailResponse = await client.GetAsync(endpoint);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var recipe = await detailResponse.Content.ReadFromJsonAsync<RecipeDetailDto>();
        Assert.NotNull(recipe);
        Assert.NotNull(recipe.Nutrition);

        var removeResponse = await client.PutAsJsonAsync(
            endpoint,
            CreateUpdatePayload(recipe, "Recipe without nutrition"));

        Assert.True(
            removeResponse.StatusCode == HttpStatusCode.OK,
            await removeResponse.Content.ReadAsStringAsync());
        var removedRecipe = await removeResponse.Content.ReadFromJsonAsync<RecipeDetailDto>();
        Assert.NotNull(removedRecipe);
        Assert.Null(removedRecipe.Nutrition);

        var removedRecords = factory.Query(db => db.RecipeNutritions
            .IgnoreQueryFilters()
            .Where(item => item.RecipeId == recipeId)
            .Select(item => new { item.Id, item.IsDeleted })
            .ToArray());
        var removedRecord = Assert.Single(removedRecords);
        Assert.Equal(nutritionId, removedRecord.Id);
        Assert.True(removedRecord.IsDeleted);

        var restoreResponse = await client.PutAsJsonAsync(
            endpoint,
            CreateUpdatePayload(
                removedRecipe,
                "Recipe with nutrition again",
                nutrition: new
                {
                    calories = 350,
                    protein = 12,
                    carbohydrates = 24,
                    fat = 6,
                    fiber = 4,
                    sodium = 110
                }));

        Assert.True(
            restoreResponse.StatusCode == HttpStatusCode.OK,
            await restoreResponse.Content.ReadAsStringAsync());
        var restoredRecipe = await restoreResponse.Content.ReadFromJsonAsync<RecipeDetailDto>();
        Assert.NotNull(restoredRecipe);
        Assert.NotNull(restoredRecipe.Nutrition);
        Assert.Equal(350, restoredRecipe.Nutrition.Calories);

        var restoredRecords = factory.Query(db => db.RecipeNutritions
            .IgnoreQueryFilters()
            .Where(item => item.RecipeId == recipeId)
            .Select(item => new { item.Id, item.IsDeleted })
            .ToArray());
        var restoredRecord = Assert.Single(restoredRecords);
        Assert.Equal(nutritionId, restoredRecord.Id);
        Assert.False(restoredRecord.IsDeleted);
    }

    [Fact]
    public async Task UpdateAddsNutrition_WhenRecipeDoesNotHaveNutrition()
    {
        await using var factory = new CategoryWebApplicationFactory();
        var recipeId = Guid.NewGuid();

        await factory.SeedAsync(async db =>
        {
            var category = Category.Create("Test category");
            db.Categories.Add(category);
            db.Recipes.Add(new Recipe
            {
                Id = recipeId,
                Title = "Recipe without nutrition",
                Slug = "recipe-without-nutrition",
                CategoryId = category.Id,
                AuthorId = "test-author",
                PrepTimeMinutes = 10,
                CookTimeMinutes = 20,
                Servings = 2,
                Difficulty = RecipeDifficulty.Easy
            });
            await db.SaveChangesAsync();
        });

        var client = factory.CreateClientAs("Author");
        var endpoint = $"/api/v1/recipes/{recipeId}";
        var detailResponse = await client.GetAsync(endpoint);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var recipe = await detailResponse.Content.ReadFromJsonAsync<RecipeDetailDto>();
        Assert.NotNull(recipe);
        Assert.Null(recipe.Nutrition);

        var updateResponse = await client.PutAsJsonAsync(
            endpoint,
            CreateUpdatePayload(
                recipe,
                "Recipe with nutrition now",
                nutrition: new
                {
                    calories = 120,
                    protein = 10,
                    carbohydrates = 10,
                    fat = 0,
                    fiber = 0,
                    sodium = 0
                }));

        Assert.True(
            updateResponse.StatusCode == HttpStatusCode.OK,
            await updateResponse.Content.ReadAsStringAsync());
        var updatedRecipe = await updateResponse.Content.ReadFromJsonAsync<RecipeDetailDto>();
        Assert.NotNull(updatedRecipe);
        Assert.NotNull(updatedRecipe.Nutrition);
        Assert.Equal(120, updatedRecipe.Nutrition.Calories);
    }

    [Fact]
    public async Task UpdateNutrition_UpdatesExistingRecordInsteadOfReplacingIt()
    {
        await using var factory = new CategoryWebApplicationFactory();
        var recipeId = Guid.NewGuid();
        var nutritionId = Guid.NewGuid();

        await factory.SeedAsync(async db =>
        {
            var category = Category.Create("Test category");
            db.Categories.Add(category);
            db.Recipes.Add(new Recipe
            {
                Id = recipeId,
                Title = "Recipe with nutrition",
                Slug = "recipe-with-nutrition",
                CategoryId = category.Id,
                AuthorId = "test-author",
                PrepTimeMinutes = 10,
                CookTimeMinutes = 20,
                Servings = 2,
                Difficulty = RecipeDifficulty.Easy,
                Nutrition = new RecipeNutrition
                {
                    Id = nutritionId,
                    Calories = 300,
                    Protein = 10,
                    Carbohydrates = 20,
                    Fat = 5,
                    Fiber = 3,
                    Sodium = 100
                }
            });
            await db.SaveChangesAsync();
        });

        var client = factory.CreateClientAs("Author");
        var endpoint = $"/api/v1/recipes/{recipeId}";
        var detailResponse = await client.GetAsync(endpoint);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var recipe = await detailResponse.Content.ReadFromJsonAsync<RecipeDetailDto>();
        Assert.NotNull(recipe);

        var updateResponse = await client.PutAsJsonAsync(
            endpoint,
            CreateUpdatePayload(
                recipe,
                "Updated recipe with nutrition",
                includeIngredients: false,
                includeSteps: false,
                nutrition: new
                {
                    calories = 450,
                    protein = 15,
                    carbohydrates = 25,
                    fat = 7,
                    fiber = 4,
                    sodium = 120
                }));

        Assert.True(
            updateResponse.StatusCode == HttpStatusCode.OK,
            await updateResponse.Content.ReadAsStringAsync());
        var nutritionRecords = factory.Query(db => db.RecipeNutritions
            .IgnoreQueryFilters()
            .Where(item => item.RecipeId == recipeId)
            .Select(item => new { item.Id, item.IsDeleted, item.Calories })
            .ToArray());
        var updatedNutrition = Assert.Single(nutritionRecords);
        Assert.Equal(nutritionId, updatedNutrition.Id);
        Assert.False(updatedNutrition.IsDeleted);
        Assert.Equal(450, updatedNutrition.Calories);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ReloadAfterConcurrentUpdate_AllowsNextUpdateWithLatestVersion(
        bool updateIngredients,
        bool updateSteps)
    {
        await using var factory = new CategoryWebApplicationFactory();
        var recipeId = Guid.NewGuid();

        await factory.SeedAsync(async db =>
        {
            var category = Category.Create("Test category");
            db.Categories.Add(category);
            db.Recipes.Add(new Recipe
            {
                Id = recipeId,
                Title = "Original recipe",
                Slug = "original-recipe",
                CategoryId = category.Id,
                AuthorId = "test-author",
                PrepTimeMinutes = 10,
                CookTimeMinutes = 20,
                Servings = 2,
                Difficulty = RecipeDifficulty.Easy,
                Ingredients = [new RecipeIngredient
                {
                    Name = "Tomato",
                    Quantity = 2,
                    Unit = "pcs",
                    OrderIndex = 0
                }],
                Steps = [new RecipeStep
                {
                    StepNumber = 1,
                    Title = "Prepare",
                    Description = "Wash the tomatoes."
                }]
            });
            await db.SaveChangesAsync();
        });

        var client = factory.CreateClientAs("Author");
        var endpoint = $"/api/v1/recipes/{recipeId}";
        var initialResponse = await client.GetAsync(endpoint);
        Assert.Equal(HttpStatusCode.OK, initialResponse.StatusCode);
        var initialRecipe = await initialResponse.Content.ReadFromJsonAsync<RecipeDetailDto>();
        Assert.NotNull(initialRecipe);

        await factory.SeedAsync(async db =>
        {
            var recipe = await db.Recipes.SingleAsync(item => item.Id == recipeId);
            recipe.Title = "Changed in another session";
            await db.SaveChangesAsync();
        });

        var staleUpdate = await client.PutAsJsonAsync(endpoint, CreateUpdatePayload(initialRecipe, "My edit"));
        Assert.Equal(HttpStatusCode.Conflict, staleUpdate.StatusCode);

        var reloadResponse = await client.GetAsync(endpoint);
        Assert.Equal(HttpStatusCode.OK, reloadResponse.StatusCode);
        var reloadedRecipe = await reloadResponse.Content.ReadFromJsonAsync<RecipeDetailDto>();
        Assert.NotNull(reloadedRecipe);
        Assert.Equal("Changed in another session", reloadedRecipe.Title);
        Assert.NotEqual(
            Convert.ToBase64String(initialRecipe.RowVersion),
            Convert.ToBase64String(reloadedRecipe.RowVersion));

        var retryResponse = await client.PutAsJsonAsync(
            endpoint,
            CreateUpdatePayload(reloadedRecipe, "My edit", updateIngredients, updateSteps));
        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);

        var finalResponse = await client.GetAsync(endpoint);
        Assert.Equal(HttpStatusCode.OK, finalResponse.StatusCode);
        var finalRecipe = await finalResponse.Content.ReadFromJsonAsync<RecipeDetailDto>();
        Assert.NotNull(finalRecipe);
        Assert.Equal("My edit", finalRecipe.Title);
    }

    [Fact]
    public async Task GetById_ReturnsFullRecipeToOwner_AndRejectsOtherUsers()
    {
        await using var factory = new CategoryWebApplicationFactory();
        var recipeId = Guid.NewGuid();

        await factory.SeedAsync(async db =>
        {
            var category = Category.Create("Test category");
            db.Categories.Add(category);
            db.Recipes.Add(new Recipe
            {
                Id = recipeId,
                Title = "Private recipe",
                Slug = "private-recipe",
                CategoryId = category.Id,
                AuthorId = "test-author",
                PrepTimeMinutes = 10,
                CookTimeMinutes = 20,
                Servings = 2,
                Difficulty = RecipeDifficulty.Easy,
                Ingredients = [new RecipeIngredient { Name = "Tomato", Quantity = 2, Unit = "pcs" }],
                Steps = [new RecipeStep { StepNumber = 1, Title = "Prepare", Description = "Wash the tomatoes." }]
            });
            await db.SaveChangesAsync();
        });

        var ownerResponse = await factory.CreateClientAs("Author")
            .GetAsync($"/api/v1/recipes/{recipeId}");

        Assert.Equal(HttpStatusCode.OK, ownerResponse.StatusCode);
        var recipe = await ownerResponse.Content.ReadFromJsonAsync<RecipeDetailDto>();
        Assert.NotNull(recipe);
        Assert.Equal(recipeId, recipe.Id);
        Assert.Equal("Private recipe", recipe.Title);
        Assert.Single(recipe.Ingredients);
        Assert.Single(recipe.Steps);

        var otherUserResponse = await factory.CreateClientAs("Reader")
            .GetAsync($"/api/v1/recipes/{recipeId}");
        Assert.Equal(HttpStatusCode.Forbidden, otherUserResponse.StatusCode);

        var anonymousResponse = await factory.CreateClient()
            .GetAsync($"/api/v1/recipes/{recipeId}");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
    }

    [Fact]
    public async Task AdminRecipeList_IncludesDraftsAndRequiresAdminRole()
    {
        await using var factory = new CategoryWebApplicationFactory();
        var recipeId = Guid.NewGuid();

        await factory.SeedAsync(async db =>
        {
            var category = Category.Create("Test category");
            db.Categories.Add(category);
            db.Recipes.Add(new Recipe
            {
                Id = recipeId,
                Title = "Another author's draft",
                Slug = "another-authors-draft",
                CategoryId = category.Id,
                AuthorId = "test-author",
                Status = RecipeStatus.Draft
            });
            await db.SaveChangesAsync();
        });

        var adminResponse = await factory.CreateClientAs("Admin")
            .GetAsync("/api/v1/admin/recipes");

        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
        var recipes = await adminResponse.Content
            .ReadFromJsonAsync<PaginatedResult<RecipeSummaryDto>>();
        Assert.NotNull(recipes);
        Assert.Contains(recipes.Items, recipe => recipe.Id == recipeId);

        var authorResponse = await factory.CreateClientAs("Author")
            .GetAsync("/api/v1/admin/recipes");
        Assert.Equal(HttpStatusCode.Forbidden, authorResponse.StatusCode);

        var anonymousResponse = await factory.CreateClient()
            .GetAsync("/api/v1/admin/recipes");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
    }

    [Fact]
    public async Task GetById_ReturnsStepNumberForRecipeImages()
    {
        await using var factory = new CategoryWebApplicationFactory();
        var recipeId = Guid.NewGuid();

        await factory.SeedAsync(async db =>
        {
            var category = Category.Create("Test category");
            db.Categories.Add(category);
            db.Recipes.Add(new Recipe
            {
                Id = recipeId,
                Title = "Recipe with step images",
                Slug = "recipe-with-step-images",
                CategoryId = category.Id,
                AuthorId = "test-author",
                Images =
                [
                    new RecipeImage
                    {
                        OriginalUrl = "https://media.example.test/a71f.webp",
                        OrderIndex = 1,
                        StepNumber = 1
                    },
                    new RecipeImage
                    {
                        OriginalUrl = "https://media.example.test/b82a.webp",
                        OrderIndex = 2,
                        StepNumber = 1
                    }
                ]
            });
            await db.SaveChangesAsync();
        });

        var response = await factory.CreateClientAs("Author")
            .GetAsync($"/api/v1/recipes/{recipeId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var recipe = await response.Content.ReadFromJsonAsync<RecipeDetailDto>();
        Assert.NotNull(recipe);
        Assert.Equal(new int?[] { 1, 1 }, recipe.Images.Select(image => image.StepNumber));
    }

    [Fact]
    public async Task UploadStepImage_StoresImageOnOwnedStepWithoutCreatingRecipeGalleryImage()
    {
        await using var factory = new CategoryWebApplicationFactory();
        var recipeId = Guid.NewGuid();
        var stepId = Guid.NewGuid();

        await factory.SeedAsync(async db =>
        {
            var category = Category.Create("Test category");
            db.Categories.Add(category);
            db.Recipes.Add(new Recipe
            {
                Id = recipeId,
                Title = "Private recipe",
                Slug = "private-recipe",
                CategoryId = category.Id,
                AuthorId = "test-author",
                Steps = [new RecipeStep
                {
                    Id = stepId,
                    StepNumber = 1,
                    Title = "Prepare",
                    Description = "Wash the tomatoes."
                }]
            });
            await db.SaveChangesAsync();
        });

        using var content = new MultipartFormDataContent();
        using var image = new ByteArrayContent([1, 2, 3]);
        image.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/webp");
        content.Add(image, "file", "step.webp");
        var response = await factory.CreateClientAs("Author").PostAsync(
            $"/api/v1/recipes/{recipeId}/steps/{stepId}/image", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            $"https://media.example.test/recipes/{recipeId:N}/step.webp",
            factory.Query(db => db.RecipeSteps.Single(step => step.Id == stepId).ImageUrl));
        Assert.Empty(factory.Query(db => db.RecipeImages.Where(recipeImage => recipeImage.RecipeId == recipeId).ToArray()));
    }

    private static object CreateUpdatePayload(
        RecipeDetailDto recipe,
        string title,
        bool includeIngredients = true,
        bool includeSteps = true,
        object? nutrition = null) => new
    {
        title,
        description = recipe.Description,
        categoryId = recipe.CategoryId,
        prepTimeMinutes = recipe.PrepTimeMinutes,
        cookTimeMinutes = recipe.CookTimeMinutes,
        servings = recipe.Servings,
        difficulty = recipe.Difficulty,
        rowVersion = recipe.RowVersion,
        ingredients = includeIngredients
            ? recipe.Ingredients.Select(item => new
            {
                item.Name,
                item.Quantity,
                item.Unit,
                item.Notes,
                item.OrderIndex
            }).ToArray()
            : null,
        steps = includeSteps
            ? recipe.Steps.Select(item => new
            {
                item.Title,
                item.Description,
                item.DurationMinutes,
                item.ImageUrl
            }).ToArray()
            : null,
        nutrition
    };
}
