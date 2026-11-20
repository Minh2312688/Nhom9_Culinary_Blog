using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CulinaryBlog.Domain.Constants;
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
            ["mon-an-vat"] = "Món ăn vặt",
            ["thuc-uong"] = "Thức uống"
        };

    public static async Task SeedAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IFileStorageService storage,
        string sampleDataDirectory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var recipes = SampleRecipeDataLoader.Load(sampleDataDirectory);
        var categories = await EnsureCategoriesAsync(context, recipes, cancellationToken);
        var categoryImages = SampleCategoryImageLoader.Load(
            Path.Combine(sampleDataDirectory, "images", "danhmuc"));
        await EnsureCategoryImagesAsync(
            context,
            categories.Values,
            categoryImages,
            storage,
            logger,
            cancellationToken);

        foreach (var sample in recipes)
        {
            var author = await EnsureAuthorAsync(userManager, roleManager, sample.Author);
            var slug = Slugify(sample.Title);
            var recipe = await context.Recipes
                .IgnoreQueryFilters()
                .Include(item => item.Images)
                .Include(item => item.Steps.Where(step => !step.IsDeleted))
                .Include(item => item.Nutrition)
                .SingleOrDefaultAsync(item => item.Slug == slug, cancellationToken);

            if (recipe is null)
            {
                recipe = CreateRecipe(sample, slug, categories[sample.CategorySlug], author.Id);
                context.Recipes.Add(recipe);
                await context.SaveChangesAsync(cancellationToken);
            }
            else if (recipe.Nutrition is null && sample.Nutrition is not null)
            {
                recipe.Nutrition = CreateNutrition(sample.Nutrition);
                context.RecipeNutritions.Add(recipe.Nutrition);
                await context.SaveChangesAsync(cancellationToken);
            }
            else if (recipe.Nutrition is { IsDeleted: true } nutrition && sample.Nutrition is not null)
            {
                RestoreNutrition(nutrition, sample.Nutrition);
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

    private static async Task EnsureCategoryImagesAsync(
        ApplicationDbContext context,
        IEnumerable<Category> categories,
        IReadOnlyDictionary<string, string> categoryImages,
        IFileStorageService storage,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        foreach (var category in categories)
        {
            if (!string.IsNullOrWhiteSpace(category.ImageUrl) ||
                !categoryImages.TryGetValue(category.Slug, out var imagePath))
            {
                continue;
            }

            var fileName = Path.GetFileName(imagePath);
            await using var stream = File.OpenRead(imagePath);
            var uploaded = await storage.UploadAsync(
                new FileUploadRequest(
                    stream,
                    fileName,
                    GetContentType(fileName),
                    stream.Length,
                    StorageFolders.Categories),
                cancellationToken);

            category.ImageUrl = uploaded.Url;
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                try
                {
                    await storage.DeleteAsync(uploaded.Url, CancellationToken.None);
                }
                catch (Exception cleanupException)
                {
                    logger.LogError(cleanupException,
                        "Failed to remove category image {ImageUrl} after database save failed.",
                        uploaded.Url);
                }

                throw;
            }
        }
    }

    private static async Task<ApplicationUser> EnsureAuthorAsync(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        string displayName)
    {
        if (!await roleManager.RoleExistsAsync(AppRoles.Author))
        {
            var roleCreateResult = await roleManager.CreateAsync(new IdentityRole(AppRoles.Author));
            if (!roleCreateResult.Succeeded)
            {
                var errors = string.Join(", ", roleCreateResult.Errors.Select(error => error.Description));
                throw new InvalidOperationException($"Failed to create Author role: {errors}");
            }
        }

        var email = $"sample.{Slugify(displayName).Replace('-', '.')}@culinaryblog.local";
        var author = await userManager.FindByEmailAsync(email);
        if (author is null)
        {
            author = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = displayName.Trim(),
                CreatedAt = DateTimeOffset.UtcNow
            };

            var createResult = await userManager.CreateAsync(author, "Admin123!");
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(error => error.Description));
                throw new InvalidOperationException(
                    $"Failed to create sample author '{displayName}': {errors}");
            }
        }

        if (!await userManager.IsInRoleAsync(author, AppRoles.Author))
        {
            var roleResult = await userManager.AddToRoleAsync(author, AppRoles.Author);
            if (!roleResult.Succeeded)
            {
                var errors = string.Join(", ", roleResult.Errors.Select(error => error.Description));
                throw new InvalidOperationException(
                    $"Failed to assign Author role to '{displayName}': {errors}");
            }
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
            Nutrition = sample.Nutrition is null ? null : CreateNutrition(sample.Nutrition),
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

    private static RecipeNutrition CreateNutrition(SampleRecipeNutrition sample)
        => new()
        {
            Calories = sample.Calories!.Value,
            Protein = sample.Protein!.Value,
            Carbohydrates = sample.Carbohydrates!.Value,
            Fat = sample.Fat!.Value,
            Fiber = sample.Fiber!.Value,
            Sodium = sample.Sodium!.Value
        };

    private static void RestoreNutrition(RecipeNutrition nutrition, SampleRecipeNutrition sample)
    {
        nutrition.Calories = sample.Calories!.Value;
        nutrition.Protein = sample.Protein!.Value;
        nutrition.Carbohydrates = sample.Carbohydrates!.Value;
        nutrition.Fat = sample.Fat!.Value;
        nutrition.Fiber = sample.Fiber!.Value;
        nutrition.Sodium = sample.Sodium!.Value;
        nutrition.IsDeleted = false;
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
            var stepNumber = FindStepNumber(image.File, sample.Steps);
            var existingImage = recipe.Images.SingleOrDefault(
                item => item.OrderIndex == image.OrderIndex);
            if (stepNumber.HasValue)
            {
                var step = recipe.Steps.SingleOrDefault(item => item.StepNumber == stepNumber.Value);
                var hasChanges = false;
                if (existingImage is null)
                {
                    var stepImagePath = Path.Combine(sample.ImageDirectory, image.File);
                    await using var stepStream = File.OpenRead(stepImagePath);
                    var uploadedStepImage = await storage.UploadAsync(
                        new FileUploadRequest(
                            stepStream,
                            image.File,
                            GetContentType(image.File),
                            stepStream.Length,
                            StorageFolders.ForRecipe(recipe.Id)),
                        cancellationToken);

                    existingImage = new RecipeImage
                    {
                        RecipeId = recipe.Id,
                        OriginalUrl = uploadedStepImage.Url,
                        AltText = image.AltText,
                        IsPrimary = false,
                        OrderIndex = image.OrderIndex,
                        StepNumber = stepNumber.Value,
                        VariantStatus = RecipeImageVariantStatus.Pending
                    };
                    context.RecipeImages.Add(existingImage);
                    hasChanges = true;
                }
                else if (existingImage.StepNumber != stepNumber.Value)
                {
                    existingImage.StepNumber = stepNumber.Value;
                    hasChanges = true;
                }

                if (step is not null && string.IsNullOrWhiteSpace(step.ImageUrl))
                {
                    step.ImageUrl = existingImage.OriginalUrl;
                    hasChanges = true;
                }

                if (hasChanges)
                {
                    await context.SaveChangesAsync(cancellationToken);
                }

                continue;
            }

            if (existingImage is not null)
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

    private static int? FindStepNumber(string fileName, IReadOnlyList<SampleRecipeStep> steps)
    {
        var exactMatch = steps.FirstOrDefault(step =>
            !string.IsNullOrWhiteSpace(step.ImageAsset) &&
            string.Equals(step.ImageAsset, fileName, StringComparison.OrdinalIgnoreCase));
        if (exactMatch is not null)
        {
            return exactMatch.StepNumber;
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var match = Regex.Match(
            stem,
            @"^step(?<stepNumber>[1-9][0-9]*)(?:-[1-9][0-9]*)?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success ||
            !int.TryParse(
                match.Groups["stepNumber"].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var stepNumber) ||
            !steps.Any(step => step.StepNumber == stepNumber))
        {
            return null;
        }

        return stepNumber;
    }

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
