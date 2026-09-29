using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Application.Tests.Features.Categories;

/// <summary>
/// Dữ liệu mẫu dùng chung cho unit test Category (EF Core InMemory, không phải integration test).
/// </summary>
internal static class CategoriesTestData
{
    public const string CakeName = "Bánh Ngọt";
    public const string CoffeeName = "Cà Phê Sữa Đá";
    public const string VeganName = "Món Chay";

    public static async Task<CategoryTestDbContext> SeedDefaultAsync()
    {
        var context = CategoryTestDbContext.CreateInMemory();

        context.Categories.AddRange(
            Category.Create(CakeName, "Bánh ngọt cho bữa sáng"),
            Category.Create(CoffeeName, "Đồ uống buổi sáng"),
            Category.Create(VeganName, "Món chay thanh đạm"));

        await context.SaveChangesAsync();
        return context;
    }
}
