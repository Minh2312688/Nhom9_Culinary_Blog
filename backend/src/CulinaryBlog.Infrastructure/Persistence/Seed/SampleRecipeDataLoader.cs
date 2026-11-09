using System.Text.Json;

namespace CulinaryBlog.Infrastructure.Persistence.Seed;

public static class SampleRecipeDataLoader
{
    private const string DataFileName = "20_cong_thuc_mon_an.json";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<SampleRecipeData> Load(string sampleDataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sampleDataDirectory);

        var dataPath = Path.GetFullPath(sampleDataDirectory);
        var jsonPath = Path.Combine(dataPath, DataFileName);
        if (!File.Exists(jsonPath))
        {
            throw new FileNotFoundException($"Sample recipe data file was not found: {jsonPath}", jsonPath);
        }

        var recipes = JsonSerializer.Deserialize<List<SampleRecipeData>>(
            File.ReadAllText(jsonPath),
            SerializerOptions);

        if (recipes is null || recipes.Count == 0)
        {
            throw new InvalidDataException($"Sample recipe data file '{jsonPath}' contains no recipes.");
        }

        var imagesRoot = Path.GetFullPath(Path.Combine(dataPath, "images"));
        foreach (var recipe in recipes)
        {
            ValidateRecipe(recipe, imagesRoot);
        }

        return recipes;
    }

    private static void ValidateRecipe(SampleRecipeData recipe, string imagesRoot)
    {
        if (string.IsNullOrWhiteSpace(recipe.Title) ||
            string.IsNullOrWhiteSpace(recipe.CategorySlug) ||
            string.IsNullOrWhiteSpace(recipe.Author) ||
            !Enum.TryParse<CulinaryBlog.Domain.Entities.RecipeDifficulty>(recipe.Difficulty, true, out _))
        {
            throw new InvalidDataException("A sample recipe has a missing title, category, author, or valid difficulty.");
        }

        if (recipe.Nutrition is null ||
            recipe.Nutrition.Calories is null or < 0 ||
            recipe.Nutrition.Protein is null or < 0 ||
            recipe.Nutrition.Carbohydrates is null or < 0 ||
            recipe.Nutrition.Fat is null or < 0 ||
            recipe.Nutrition.Fiber is null or < 0 ||
            recipe.Nutrition.Sodium is null or < 0)
        {
            throw new InvalidDataException(
                $"Sample recipe '{recipe.Title}' must include non-negative values for all nutrition fields.");
        }

        if (recipe.Ingredients is null ||
            recipe.Steps is null ||
            recipe.Images is null ||
            recipe.Ingredients.Count == 0 ||
            recipe.Steps.Count == 0 ||
            recipe.Images.Count == 0)
        {
            throw new InvalidDataException($"Sample recipe '{recipe.Title}' must include ingredients, steps, and images.");
        }

        if (string.IsNullOrWhiteSpace(recipe.Folder) ||
            recipe.Folder.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('_' or '-')))
        {
            throw new InvalidDataException($"Sample recipe '{recipe.Title}' has an invalid image folder.");
        }

        var physicalFolder = recipe.Folder.Replace('_', '-');
        var imageDirectory = Path.GetFullPath(Path.Combine(imagesRoot, physicalFolder));
        if (!IsChildPath(imagesRoot, imageDirectory))
        {
            throw new InvalidDataException($"Sample recipe '{recipe.Title}' image folder escapes the images directory.");
        }

        if (!Directory.Exists(imageDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Image directory '{imageDirectory}' for recipe '{recipe.Title}' does not exist.");
        }

        foreach (var image in recipe.Images)
        {
            if (string.IsNullOrWhiteSpace(image.File) ||
                !string.Equals(Path.GetFileName(image.File), image.File, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Sample recipe '{recipe.Title}' has an invalid image file name.");
            }

            var imagePath = Path.GetFullPath(Path.Combine(imageDirectory, image.File));
            if (!IsChildPath(imageDirectory, imagePath) || !File.Exists(imagePath))
            {
                throw new FileNotFoundException(
                    $"Image '{image.File}' for recipe '{recipe.Title}' was not found in '{imageDirectory}'.",
                    imagePath);
            }
        }

        recipe.ImageDirectory = imageDirectory;
    }

    private static bool IsChildPath(string parentPath, string candidatePath)
    {
        var relativePath = Path.GetRelativePath(parentPath, candidatePath);
        return !Path.IsPathRooted(relativePath) &&
            relativePath != "." &&
            relativePath != ".." &&
            !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
    }
}

public sealed class SampleRecipeData
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string CategorySlug { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public SampleRecipeNutrition? Nutrition { get; init; }
    public int PrepTimeMinutes { get; init; }
    public int CookTimeMinutes { get; init; }
    public int Servings { get; init; }
    public string Difficulty { get; init; } = string.Empty;
    public List<SampleRecipeIngredient> Ingredients { get; init; } = [];
    public List<SampleRecipeStep> Steps { get; init; } = [];
    public string Folder { get; init; } = string.Empty;
    public List<SampleRecipeImage> Images { get; init; } = [];
    public string ImageDirectory { get; set; } = string.Empty;
}

public sealed class SampleRecipeNutrition
{
    public int? Calories { get; init; }
    public decimal? Protein { get; init; }
    public decimal? Carbohydrates { get; init; }
    public decimal? Fat { get; init; }
    public decimal? Fiber { get; init; }
    public decimal? Sodium { get; init; }
}

public sealed class SampleRecipeIngredient
{
    public string Name { get; init; } = string.Empty;
    public decimal? Quantity { get; init; }
    public string? Unit { get; init; }
    public string? Notes { get; init; }
    public int OrderIndex { get; init; }
}

public sealed class SampleRecipeStep
{
    public int StepNumber { get; init; }
    public string? Title { get; init; }
    public string Description { get; init; } = string.Empty;
    public int? DurationMinutes { get; init; }
}

public sealed class SampleRecipeImage
{
    public string File { get; init; } = string.Empty;
    public string? AltText { get; init; }
    public bool IsPrimary { get; init; }
    public int OrderIndex { get; init; }
}
