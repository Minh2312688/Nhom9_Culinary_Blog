using Xunit;
using CulinaryBlog.Infrastructure.Persistence.Seed;

namespace CulinaryBlog.Application.Tests.Infrastructure.Persistence;

public sealed class SampleRecipeDataLoaderTests
{
    [Fact]
    public void Load_resolves_underscore_folder_names_to_hyphenated_directories()
    {
        var sampleDataPath = CreateSampleDataDirectory(includeImage: true);
        try
        {
            var recipes = SampleRecipeDataLoader.Load(sampleDataPath);

            var recipe = Assert.Single(recipes);
            Assert.Equal("Recipe Name", recipe.Title);
            Assert.Equal(Path.Combine(sampleDataPath, "images", "recipe-name"), recipe.ImageDirectory);
            Assert.True(File.Exists(Path.Combine(recipe.ImageDirectory, Assert.Single(recipe.Images).File)));
        }
        finally
        {
            Directory.Delete(sampleDataPath, recursive: true);
        }
    }

    [Fact]
    public void Load_reports_a_missing_referenced_image()
    {
        var sampleDataPath = CreateSampleDataDirectory(includeImage: false);
        try
        {
            Assert.Throws<FileNotFoundException>(() => SampleRecipeDataLoader.Load(sampleDataPath));
        }
        finally
        {
            Directory.Delete(sampleDataPath, recursive: true);
        }
    }

    [Fact]
    public void Load_deserializes_all_sample_nutrition_fields()
    {
        var sampleDataPath = CreateSampleDataDirectory(includeImage: true);
        try
        {
            var recipe = Assert.Single(SampleRecipeDataLoader.Load(sampleDataPath));

            Assert.Equal(310, recipe.Nutrition!.Calories);
            Assert.Equal(18.5m, recipe.Nutrition.Protein);
            Assert.Equal(36m, recipe.Nutrition.Carbohydrates);
            Assert.Equal(10.2m, recipe.Nutrition.Fat);
            Assert.Equal(4.1m, recipe.Nutrition.Fiber);
            Assert.Equal(680m, recipe.Nutrition.Sodium);
        }
        finally
        {
            Directory.Delete(sampleDataPath, recursive: true);
        }
    }

    [Fact]
    public void Load_rejects_sample_recipe_without_nutrition()
    {
        var sampleDataPath = CreateSampleDataDirectory(includeImage: true, includeNutrition: false);
        try
        {
            Assert.Throws<InvalidDataException>(() => SampleRecipeDataLoader.Load(sampleDataPath));
        }
        finally
        {
            Directory.Delete(sampleDataPath, recursive: true);
        }
    }

    [Fact]
    public void Load_rejects_negative_sample_nutrition()
    {
        var sampleDataPath = CreateSampleDataDirectory(includeImage: true, calories: -1);
        try
        {
            Assert.Throws<InvalidDataException>(() => SampleRecipeDataLoader.Load(sampleDataPath));
        }
        finally
        {
            Directory.Delete(sampleDataPath, recursive: true);
        }
    }

    private static string CreateSampleDataDirectory(
        bool includeImage,
        bool includeNutrition = true,
        int calories = 310)
    {
        var sampleDataPath = Path.Combine(Path.GetTempPath(), $"sample-recipes-{Guid.NewGuid():N}");
        var imageDirectory = Path.Combine(sampleDataPath, "images", "recipe-name");
        Directory.CreateDirectory(imageDirectory);
        if (includeImage)
        {
            File.WriteAllBytes(Path.Combine(imageDirectory, "main.webp"), new byte[] { 1 });
        }

        File.WriteAllText(
            Path.Combine(sampleDataPath, "20_cong_thuc_mon_an.json"),
            $$"""
            [
              {
                "title": "Recipe Name",
                "description": "A test recipe.",
                "categorySlug": "mon-test",
                "author": "Test Author",
                "prepTimeMinutes": 1,
                "cookTimeMinutes": 2,
                "servings": 1,
                "difficulty": "Easy",
                {{(includeNutrition ? $"\"nutrition\": {{ \"calories\": {calories}, \"protein\": 18.5, \"carbohydrates\": 36, \"fat\": 10.2, \"fiber\": 4.1, \"sodium\": 680 }}," : string.Empty)}}
                "ingredients": [{ "name": "Ingredient", "quantity": 1, "unit": "piece", "orderIndex": 0 }],
                "steps": [{ "stepNumber": 1, "description": "Cook it." }],
                "folder": "recipe_name",
                "images": [{ "file": "main.webp", "altText": "Finished recipe", "isPrimary": true, "orderIndex": 0 }]
              }
            ]
            """);

        return sampleDataPath;
    }
}
