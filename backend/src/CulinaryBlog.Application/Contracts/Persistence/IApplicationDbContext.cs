using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace CulinaryBlog.Application.Contracts.Persistence;
public interface IApplicationDbContext
{
    DbSet<Category> Categories { get; }
    DbSet<Recipe> Recipes { get; }
    DbSet<RecipeStep> RecipeSteps { get; }
    DbSet<RecipeIngredient> RecipeIngredients { get; }
    DbSet<RecipeImage> RecipeImages { get; }
    DbSet<RecipeNutrition> RecipeNutritions { get; }
    void SetOriginalRowVersion<T>(T entity, byte[] rowVersion) where T : class;
    // Lưu thay đổi — trả về số bản ghi bị ảnh hưởng
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
