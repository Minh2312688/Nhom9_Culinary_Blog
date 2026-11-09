using System.Globalization;
using System.Text;
using CulinaryBlog.Application.Common.Files;
using CulinaryBlog.Application.Contracts.Storage;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ApplicationUser = CulinaryBlog.Infrastructure.Identity.ApplicationUser;

namespace CulinaryBlog.Infrastructure.Persistence.Seed;

public static class Lab02DataSeeder
{
    private static readonly (string Name, string Slug, string Description)[] CategoryDefinitions =
    [
        ("Món Việt", "mon-viet", "Các món ăn truyền thống và đặc sản ba miền Việt Nam."),
        ("Món Á", "mon-a", "Khám phá tinh hoa ẩm thực các nước châu Á phong phú."),
        ("Món Âu", "mon-au", "Các món ăn tinh tế mang phong cách ẩm thực phương Tây."),
        ("Món chay", "mon-chay", "Món ăn thanh đạm, giàu dinh dưỡng từ rau củ quả tự nhiên."),
        ("Món ăn sáng", "mon-an-sang", "Các món điểm tâm khởi đầu ngày mới tràn đầy năng lượng."),
        ("Món chính", "mon-chinh", "Món ăn chính đậm đà cho bữa cơm gia đình đầm ấm."),
        ("Món tráng miệng", "mon-trang-mieng", "Món ngọt thanh mát, chè, kem và trái cây tươi."),
        ("Đồ uống", "do-uong", "Các loại nước giải khát, sinh tố, trà và cà phê thơm ngon."),
        ("Món canh", "mon-canh", "Canh thanh nhiệt, canh hầm bổ dưỡng cho mọi bữa ăn."),
        ("Món xào", "mon-xao", "Món xào nhanh, giữ trọn độ giòn ngọt của nguyên liệu."),
        ("Món chiên", "mon-chien", "Món chiên vàng giòn rụm, thơm ngon hấp dẫn."),
        ("Món nướng", "mon-nuong", "Món nướng thơm lừng với nước sốt ướp đậm đà."),
        ("Món hấp", "mon-hap", "Món hấp thanh vị, giữ trọn vẹn dưỡng chất nguyên bản."),
        ("Món kho", "mon-kho", "Món kho đậm đà, màu sắc cánh gián bắt mắt hao cơm."),
        ("Món lẩu", "mon-lau", "Nồi lẩu nóng hổi sum vầy cùng gia đình và bạn bè."),
        ("Salad", "salad", "Các món gỏi, nộm, salad tươi giòn giàu vitamin."),
        ("Bánh", "banh", "Các loại bánh truyền thống và bánh ngọt hiện đại thơm bơ."),
        ("Hải sản", "hai-san", "Hải sản tươi sống chế biến đa dạng hương vị biển cả."),
        ("Món gà", "mon-ga", "Các món ngon hấp dẫn từ thịt gà đồi, gà thả vườn."),
        ("Món bò", "mon-bo", "Thịt bò mềm mọng nước chế biến theo nhiều phong cách.")
    ];

    private static readonly IReadOnlyDictionary<string, string> SampleCategoryNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["mon-chao"] = "Món cháo",
            ["mon-khia"] = "Món khía",
            ["mon-quay"] = "Món quay",
            ["mon-sup"] = "Món súp",
            ["thuc-uong"] = "Thức uống"
        };

    public static async Task SeedAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IFileStorageService storage,
        string sampleDataDirectory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var recipes = SampleRecipeDataLoader.Load(sampleDataDirectory);
        var categories = await EnsureCategoriesAsync(context, recipes, cancellationToken);

        foreach (var sample in recipes)
        {
            var author = await EnsureAuthorAsync(userManager, sample.Author);
            var slug = Slugify(sample.Title);
            var recipe = await context.Recipes
                .IgnoreQueryFilters()
                .Include(item => item.Images)
                .SingleOrDefaultAsync(item => item.Slug == slug, cancellationToken);

            if (recipe is null)
            {
                recipe = CreateRecipe(sample, slug, categories[sample.CategorySlug], author.Id);
                context.Recipes.Add(recipe);
                await context.SaveChangesAsync(cancellationToken);
            }

            await EnsureImagesAsync(context, recipe, sample, storage, cancellationToken);
        }

        logger.LogInformation("Sample recipe seed ready: {RecipeCount} recipes.", recipes.Count);
    }

    private static async Task<Dictionary<string, Category>> EnsureCategoriesAsync(
        ApplicationDbContext context,
        IReadOnlyList<SampleRecipeData> recipes,
        CancellationToken cancellationToken)
    {
        var categories = new Dictionary<string, Category>(StringComparer.Ordinal);
        var orderIndex = 0;

        foreach (var definition in CategoryDefinitions)
        {
            var category = await EnsureCategoryAsync(
                context,
                definition.Name,
                definition.Slug,
                definition.Description,
                ++orderIndex,
                cancellationToken);
            categories[category.Slug] = category;
        }

        foreach (var slug in recipes.Select(recipe => recipe.CategorySlug).Distinct(StringComparer.Ordinal))
        {
            if (categories.ContainsKey(slug))
            {
                continue;
            }

            var name = SampleCategoryNames.TryGetValue(slug, out var sampleName)
                ? sampleName
                : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(slug.Replace('-', ' '));
            var category = await EnsureCategoryAsync(context, name, slug, null, ++orderIndex, cancellationToken);
            categories[category.Slug] = category;
        }

        return categories;
    }

    private static async Task<Category> EnsureCategoryAsync(
        ApplicationDbContext context,
        string name,
        string slug,
        string? description,
        int orderIndex,
        CancellationToken cancellationToken)
    {
        var category = await context.Categories
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Slug == slug, cancellationToken);
        if (category is not null)
        {
            return category;
        }

        category = new Category
        {
            Name = name,
            Slug = slug,
            Description = description,
            OrderIndex = orderIndex,
            CreatedAt = DateTime.UtcNow
        };
        context.Categories.Add(category);
        await context.SaveChangesAsync(cancellationToken);
        return category;
    }

    private static async Task<ApplicationUser> EnsureAuthorAsync(
        UserManager<ApplicationUser> userManager,
        string displayName)
    {
        var email = $"sample.{Slugify(displayName).Replace('-', '.')}@culinaryblog.local";
        var author = await userManager.FindByEmailAsync(email);
        if (author is not null)
        {
            return author;
        }

        author = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        var result = await userManager.CreateAsync(author);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"Failed to create sample author '{displayName}': {errors}");
        }

        return author;
    }

    private static Recipe CreateRecipe(
        SampleRecipeData sample,
        string slug,
        Category category,
        string authorId)
    {
        var recipe = new Recipe
        {
            Title = sample.Title.Trim(),
            Slug = slug,
            Description = sample.Description,
            CategoryId = category.Id,
            AuthorId = authorId,
            PrepTimeMinutes = sample.PrepTimeMinutes,
            CookTimeMinutes = sample.CookTimeMinutes,
            Servings = sample.Servings,
            Difficulty = Enum.Parse<RecipeDifficulty>(sample.Difficulty, true),
            CreatedAt = DateTime.UtcNow
        };

        foreach (var ingredient in sample.Ingredients.OrderBy(item => item.OrderIndex))
        {
            recipe.Ingredients.Add(new RecipeIngredient
            {
                RecipeId = recipe.Id,
                Name = ingredient.Name,
                Quantity = ingredient.Quantity,
                Unit = ingredient.Unit,
                Notes = ingredient.Notes,
                OrderIndex = ingredient.OrderIndex
            });
        }

        foreach (var step in sample.Steps.OrderBy(item => item.StepNumber))
        {
            recipe.Steps.Add(new RecipeStep
            {
                RecipeId = recipe.Id,
                StepNumber = step.StepNumber,
                Title = step.Title,
                Description = step.Description,
                DurationMinutes = step.DurationMinutes
            });
        }

        recipe.Publish(DateTimeOffset.UtcNow);
        return recipe;
    }

    private static async Task EnsureImagesAsync(
        ApplicationDbContext context,
        Recipe recipe,
        SampleRecipeData sample,
        IFileStorageService storage,
        CancellationToken cancellationToken)
    {
        foreach (var image in sample.Images.OrderBy(item => item.OrderIndex))
        {
            if (recipe.Images.Any(existing => existing.OrderIndex == image.OrderIndex))
            {
                continue;
            }

            var imagePath = Path.Combine(sample.ImageDirectory, image.File);
            await using var stream = File.OpenRead(imagePath);
            var uploaded = await storage.UploadAsync(
                new FileUploadRequest(
                    stream,
                    image.File,
                    GetContentType(image.File),
                    stream.Length,
                    StorageFolders.ForRecipe(recipe.Id)),
                cancellationToken);

            var isPrimary = !recipe.Images.Any(existing => existing.IsPrimary) &&
                (image.IsPrimary || image.OrderIndex == 0);
            context.RecipeImages.Add(new RecipeImage
            {
                RecipeId = recipe.Id,
                OriginalUrl = uploaded.Url,
                AltText = image.AltText,
                IsPrimary = isPrimary,
                OrderIndex = image.OrderIndex,
                VariantStatus = RecipeImageVariantStatus.Pending
            });
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private static string GetContentType(string fileName)
        => Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".webp" => "image/webp",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };

    private static string Slugify(string value)
    {
        var normalized = value.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var slug = new StringBuilder(normalized.Length);
        var previousWasSeparator = false;

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                slug.Append(char.ToLowerInvariant(character));
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator && slug.Length > 0)
            {
                slug.Append('-');
                previousWasSeparator = true;
            }
        }

        return slug.ToString().Trim('-');
    }
}
