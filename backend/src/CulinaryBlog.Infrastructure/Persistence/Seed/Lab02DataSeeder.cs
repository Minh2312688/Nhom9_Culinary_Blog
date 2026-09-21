using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Persistence.Seed;

public static class Lab02DataSeeder
{
    public static async Task SeedAsync(
        AuthDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger logger)
    {
        // 1. Check idempotency
        if (await context.Categories.AnyAsync() && await context.Recipes.CountAsync() >= 100)
        {
            logger.LogInformation("Lab02DataSeeder: Database already contains categories and at least 100 recipes. Skipping seeding.");
            return;
        }

        logger.LogInformation("Lab02DataSeeder: Starting database seeding for Lab 02...");

        // Fixed seed random generator for deterministic data
        var rng = new Random(20260922);

        // 2. Ensure Development Seed Author exists
        var devAuthorEmail = "dev.author@culinaryblog.local";
        var author = await userManager.FindByEmailAsync(devAuthorEmail);
        if (author == null)
        {
            author = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = devAuthorEmail,
                Email = devAuthorEmail,
                DisplayName = "Chef Nguyễn Phạm Phú Nam",
                EmailConfirmed = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            var createResult = await userManager.CreateAsync(author);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create development seed user: {errors}");
            }
            logger.LogInformation("Lab02DataSeeder: Created development seed user {Email}", devAuthorEmail);
        }

        // 3. Seed Categories (at least 20)
        var categoryDefs = new (string Name, string Slug, string Description)[]
        {
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
        };

        var categories = new List<Category>();
        int catOrder = 1;
        foreach (var def in categoryDefs)
        {
            var existingCat = await context.Categories.FirstOrDefaultAsync(c => c.Slug == def.Slug);
            if (existingCat == null)
            {
                var cat = new Category
                {
                    Name = def.Name,
                    Slug = def.Slug,
                    Description = def.Description,
                    OrderIndex = catOrder++,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                context.Categories.Add(cat);
                categories.Add(cat);
            }
            else
            {
                categories.Add(existingCat);
            }
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Lab02DataSeeder: Verified/seeded {Count} categories.", categories.Count);

        // 4. Seed Recipes (at least 100 recipes)
        var currentRecipeCount = await context.Recipes.CountAsync();
        if (currentRecipeCount >= 100)
        {
            logger.LogInformation("Lab02DataSeeder: Already have {Count} recipes. Skipping recipe generation.", currentRecipeCount);
            return;
        }

        var recipeTemplates = new (string TitlePrefix, string DescPrefix)[]
        {
            ("Phở bò gia truyền", "Hương vị nước dùng phở bò ninh xương thơm lừng quế hồi."),
            ("Bún chả Hà Nội", "Thịt nướng than hoa thơm nức mũi ăn kèm bún và nước mắm chua ngọt."),
            ("Cơm tấm sườn bì chả", "Cơm tấm dẻo thơm kết hợp sườn nướng mật ong vàng óng."),
            ("Gỏi cuốn tôm thịt", "Món cuốn tươi mát chấm cùng tương đậu phộng béo ngậy."),
            ("Bánh mì chảo đặc biệt", "Bánh mì giòn rụm với pate gan, trứng ốp la và xúc xích."),
            ("Bò kho nước dừa", "Thịt bò mềm thấm vị nước dừa tươi béo ngọt tự nhiên."),
            ("Canh chua cá lóc", "Canh chua thanh tao đặc trưng Nam Bộ với dứa và đậu bắp."),
            ("Thịt kho tàu nước dừa", "Món ăn truyền thống ngày Tết với thịt ba chỉ mềm tan."),
            ("Gà nướng muối ớt", "Gà ta thịt chắc nướng than hoa thơm lừng vị cay nồng."),
            ("Lẩu thái hải sản cay nồng", "Nước lẩu chua cay đậm đà ngập tràn tôm mực tươi rói."),
            ("Salad ức gà sốt mè rang", "Bữa ăn lành mạnh chuẩn eat-clean cho người ăn kiêng."),
            ("Bánh xèo miền Tây giòn rụm", "Vỏ bánh giòn tan bọc nhân tôm thịt giá đỗ thơm phức."),
            ("Mực xào cần tỏi", "Mực tươi giòn sần sật xào cùng cần tây và tỏi phi thơm."),
            ("Sườn xào chua ngọt", "Sườn non chiên giòn sốt cà chua dứa chua ngọt đậm đà."),
            ("Chả giò tôm thịt chiên giòn", "Món khai vị giòn tan không thể thiếu trong các bữa tiệc."),
            ("Cá hồi áp chảo sốt bơ chanh", "Cá hồi tươi áp chảo béo ngậy quyện cùng sốt chanh tươi."),
            ("Spaghetti bò bằm sốt Bolognese", "Mì Ý sợi dai mềm với sốt thịt bò cà chua thơm ngậy."),
            ("Bò bít tết sốt tiêu đen", "Thịt bò thăn mềm mọng kết hợp sốt tiêu đen cay nhẹ nồng ấm."),
            ("Canh rong biển thịt bò", "Món canh thanh mát chuẩn vị Hàn Quốc bổ dưỡng."),
            ("Bánh flan caramen mềm mịn", "Tráng miệng ngọt ngào mát lạnh với lớp caramen đắng nhẹ.")
        };

        var ingredientPool = new (string Name, string Unit, decimal BaseQty)[]
        {
            ("Thịt ba chỉ", "gam", 300m),
            ("Thịt bò thăn", "gam", 250m),
            ("Thịt gà ta", "gam", 500m),
            ("Tôm sú tươi", "gam", 200m),
            ("Mực nang", "gam", 200m),
            ("Hành tím băm", "củ", 3m),
            ("Tỏi khô băm", "tép", 5m),
            ("Gừng tươi thái sợi", "nhánh", 1m),
            ("Hành lá cắt khúc", "nhánh", 4m),
            ("Nước mắm truyền thống", "thìa canh", 2m),
            ("Muối tinh", "thìa cà phê", 1.5m),
            ("Đường phèn", "thìa canh", 1m),
            ("Hạt tiêu đen xay", "thìa cà phê", 0.5m),
            ("Dầu ăn thực vật", "thìa canh", 2m),
            ("Ớt sừng tươi", "trái", 2m),
            ("Sả cây đập dập", "cây", 3m),
            ("Cà chua chín", "quả", 2m),
            ("Rau xà lách", "gam", 150m),
            ("Nước cốt dừa", "ml", 150m),
            ("Hạt nêm xương hầm", "thìa cà phê", 2m),
            ("Dầu hào", "thìa canh", 1m),
            ("Nước tương", "thìa canh", 1.5m),
            ("Rau ngò rí", "nhánh", 3m)
        };

        var stepTemplates = new (string Title, string Description)[]
        {
            ("Sơ chế nguyên liệu", "Rửa sạch toàn bộ thịt, cá và rau củ với nước muối loãng, sau đó để ráo nước và cắt miếng vừa ăn."),
            ("Ướp gia vị", "Cho nguyên liệu chính vào âu, thêm gia vị theo định lượng đã chuẩn bị, trộn đều và để thấm trong 20 phút."),
            ("Phi thơm hành tỏi", "Bắc chảo lên bếp, cho dầu ăn vào đun nóng rồi cho hành tỏi băm vào phi đến khi vàng giòn và dậy mùi thơm."),
            ("Chế biến món ăn", "Cho các nguyên liệu đã ướp vào chảo đảo đều tay ở lửa vừa cho đến khi chín đều và ngấm đều gia vị."),
            ("Nêm nếm và hoàn thiện", "Kiểm tra lại độ đậm đà của món ăn, nêm nếm lại cho vừa khẩu vị gia đình rồi tắt bếp."),
            ("Trình bày và thưởng thức", "Múc món ăn ra đĩa, trang trí thêm hành hoa, ngò rí và ớt tỉa hoa rồi thưởng thức nóng cùng cơm trắng.")
        };

        int targetRecipes = 100;
        int recipesToCreate = targetRecipes - currentRecipeCount;

        logger.LogInformation("Lab02DataSeeder: Generating {Count} unique recipes...", recipesToCreate);

        for (int i = 1; i <= recipesToCreate; i++)
        {
            int recipeIndex = currentRecipeCount + i;
            var template = recipeTemplates[(recipeIndex - 1) % recipeTemplates.Length];
            var category = categories[(recipeIndex - 1) % categories.Count];

            string title = $"{template.TitlePrefix} - Phong cách #{recipeIndex}";
            string slug = $"cong-thuc-{category.Slug}-{recipeIndex}";

            var recipe = new Recipe
            {
                Title = title,
                Slug = slug,
                Description = $"{template.DescPrefix} Hướng dẫn chi tiết công thức chuẩn vị số #{recipeIndex}.",
                CategoryId = category.Id,
                AuthorId = author.Id,
                PrepTimeMinutes = rng.Next(10, 45),
                CookTimeMinutes = rng.Next(15, 90),
                Servings = rng.Next(2, 8),
                Difficulty = rng.Next(1, 4), // 1=Easy, 2=Medium, 3=Hard
                Status = 1, // Published
                CreatedAt = DateTimeOffset.UtcNow
            };

            // Section 14: EACH recipe must have at least 10 RecipeIngredients
            int ingredientCount = rng.Next(10, 13); // 10 to 12 ingredients
            var selectedIngredients = ingredientPool
                .OrderBy(_ => rng.Next())
                .Take(ingredientCount)
                .ToList();

            for (int order = 0; order < selectedIngredients.Count; order++)
            {
                var ingDef = selectedIngredients[order];
                recipe.Ingredients.Add(new RecipeIngredient
                {
                    RecipeId = recipe.Id,
                    Name = ingDef.Name,
                    Quantity = ingDef.BaseQty * (decimal)(0.8 + rng.NextDouble() * 0.6),
                    Unit = ingDef.Unit,
                    Notes = order == 0 ? "Nguyên liệu chính" : "Gia vị nêm nếm vừa ăn",
                    OrderIndex = order,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }

            // Section 15: EACH recipe must have at least 5 RecipeSteps with unique StepNumber
            int stepCount = rng.Next(5, 7); // 5 to 6 steps
            for (int s = 1; s <= stepCount; s++)
            {
                var stepTmpl = stepTemplates[s - 1];
                recipe.Steps.Add(new RecipeStep
                {
                    RecipeId = recipe.Id,
                    StepNumber = s, // 1, 2, 3, 4, 5...
                    Title = $"Bước {s}: {stepTmpl.Title}",
                    Description = stepTmpl.Description,
                    TimerMinutes = s == 2 ? 20 : (s == 4 ? 15 : null),
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }

            context.Recipes.Add(recipe);
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Lab02DataSeeder: Successfully seeded {Count} recipes with ingredients and steps.", recipesToCreate);
    }
}
