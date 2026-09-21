using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Persistence;

public static class RandomDataSeeder
{
    private const string ReportAuthorId = "report-author-00000000000000000000000000000001";
    private const int CategoryTarget = 20;
    private const int RecipeTarget = 100;
    private const int IngredientsPerRecipe = 10;
    private const int StepsPerRecipe = 5;

    public static async Task SeedAsync(
        ApplicationDbContext context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var author = await EnsureAuthorAsync(context, cancellationToken);
        var categories = await EnsureCategoriesAsync(context, cancellationToken);
        await EnsureRecipesAsync(context, author.Id, categories, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var categoryCount = await context.Categories.CountAsync(cancellationToken);
        var recipeCount = await context.Recipes.CountAsync(cancellationToken);
        logger.LogInformation(
            "Report random data ready: {CategoryCount} categories and {RecipeCount} recipes.",
            categoryCount,
            recipeCount);
    }

    private static async Task<ApplicationUser> EnsureAuthorAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var author = await context.Set<ApplicationUser>()
            .SingleOrDefaultAsync(x => x.Id == ReportAuthorId, cancellationToken);
        if (author is not null)
        {
            return author;
        }

        author = new ApplicationUser
        {
            Id = ReportAuthorId,
            UserName = "report.author@culinary.local",
            NormalizedUserName = "REPORT.AUTHOR@CULINARY.LOCAL",
            Email = "report.author@culinary.local",
            NormalizedEmail = "REPORT.AUTHOR@CULINARY.LOCAL",
            EmailConfirmed = true,
            DisplayName = "Report Random Author",
            Role = nameof(UserRole.Author)
        };
        context.Set<ApplicationUser>().Add(author);
        await context.SaveChangesAsync(cancellationToken);
        return author;
    }

    private static async Task<List<Category>> EnsureCategoriesAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var categories = await context.Categories
            .Where(x => x.Slug.StartsWith("report-category-"))
            .OrderBy(x => x.Slug)
            .ToListAsync(cancellationToken);

        for (var index = categories.Count + 1; categories.Count < CategoryTarget; index++)
        {
            var category = Category.Create(
                $"Report Category {index:00}",
                $"Category generated for the report dataset {index:00}.");
            context.Categories.Add(category);
            categories.Add(category);
        }

        await context.SaveChangesAsync(cancellationToken);
        return categories;
    }

    private static async Task EnsureRecipesAsync(
        ApplicationDbContext context,
        string authorId,
        IReadOnlyList<Category> categories,
        CancellationToken cancellationToken)
    {
        var randomRecipes = await context.Recipes
            .IgnoreQueryFilters()
            .Where(x => x.Slug.StartsWith("report-recipe-"))
            .Include(x => x.Ingredients)
            .Include(x => x.Steps)
            .ToListAsync(cancellationToken);

        var random = new Random(20260921);
        for (var index = randomRecipes.Count + 1; randomRecipes.Count < RecipeTarget; index++)
        {
            var category = categories[(index - 1) % categories.Count];
            var recipe = CreateRecipe(index, category.Id, authorId, random);
            context.Recipes.Add(recipe);
            randomRecipes.Add(recipe);
        }

        foreach (var recipe in randomRecipes)
        {
            EnsureMinimumChildren(recipe, random);
        }
    }

    private static Recipe CreateRecipe(int index, Guid categoryId, string authorId, Random random)
    {
        var recipe = new Recipe
        {
            Title = $"Report Recipe {index:000}",
            Slug = $"report-recipe-{index:000}",
            Description = $"Randomly generated report recipe number {index:000}.",
            PrepTimeMinutes = random.Next(5, 31),
            CookTimeMinutes = random.Next(10, 91),
            Servings = random.Next(2, 9),
            Difficulty = new[] { "Easy", "Medium", "Hard" }[random.Next(3)],
            Status = RecipeStatus.Published,
            CategoryId = categoryId,
            AuthorId = authorId
        };

        AddIngredients(recipe, random);
        AddSteps(recipe, random);
        recipe.Nutrition = CreateNutrition(random);
        return recipe;
    }

    private static void EnsureMinimumChildren(Recipe recipe, Random random)
    {
        for (var index = recipe.Ingredients.Count + 1; recipe.Ingredients.Count < IngredientsPerRecipe; index++)
        {
            recipe.Ingredients.Add(new RecipeIngredient
            {
                Name = $"Ingredient {index:00}",
                Quantity = random.Next(1, 6),
                Unit = "portion",
                Notes = "Generated report ingredient",
                OrderIndex = index
            });
        }

        for (var index = recipe.Steps.Count + 1; recipe.Steps.Count < StepsPerRecipe; index++)
        {
            recipe.Steps.Add(new RecipeStep
            {
                StepNumber = index,
                Title = $"Preparation step {index}",
                Description = $"Complete preparation step {index} for the report recipe.",
                DurationMinutes = random.Next(2, 16)
            });
        }
    }

    private static void AddIngredients(Recipe recipe, Random random)
    {
        for (var index = 1; index <= IngredientsPerRecipe; index++)
        {
            recipe.Ingredients.Add(new RecipeIngredient
            {
                Name = $"Ingredient {index:00}",
                Quantity = random.Next(1, 6),
                Unit = "portion",
                Notes = "Generated report ingredient",
                OrderIndex = index
            });
        }
    }

    private static void AddSteps(Recipe recipe, Random random)
    {
        for (var index = 1; index <= StepsPerRecipe; index++)
        {
            recipe.Steps.Add(new RecipeStep
            {
                StepNumber = index,
                Title = $"Preparation step {index}",
                Description = $"Complete preparation step {index} for the report recipe.",
                DurationMinutes = random.Next(2, 16)
            });
        }
    }

    private static RecipeNutrition CreateNutrition(Random random) => new()
    {
        Calories = random.Next(200, 801),
        Protein = random.Next(10, 41),
        Carbohydrates = random.Next(20, 91),
        Fat = random.Next(5, 31),
        Fiber = random.Next(2, 16),
        Sodium = random.Next(100, 901)
    };
}
