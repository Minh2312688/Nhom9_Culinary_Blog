using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Tests.Features.Categories;

/// <summary>
/// DbContext test double (EF Core InMemory) chỉ phục vụ unit test Category.
/// Dùng để test handler mà không phụ thuộc ApplicationDbContext thật thuộc Infrastructure (TV2).
/// Đây là unit test, không phải integration test.
/// Recipe và các entity liên quan được bỏ qua vì test Category chỉ cần bảng Categories.
/// </summary>
public sealed class CategoryTestDbContext : DbContext, IApplicationDbContext
{
    public CategoryTestDbContext(DbContextOptions<CategoryTestDbContext> options)
        : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Recipe> Recipes => Set<Recipe>();

    public DbSet<RecipeStep> RecipeSteps => Set<RecipeStep>();

    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();

    public DbSet<RecipeImage> RecipeImages => Set<RecipeImage>();

    public DbSet<RecipeNutrition> RecipeNutritions => Set<RecipeNutrition>();

    public void SetOriginalRowVersion<T>(T entity, byte[] rowVersion) where T : class
    {
        Entry(entity).Property(nameof(BaseEntity.RowVersion)).OriginalValue = rowVersion;
    }

    public static CategoryTestDbContext CreateInMemory()
    {
        var options = new DbContextOptionsBuilder<CategoryTestDbContext>()
            .UseInMemoryDatabase($"Categories_{Guid.NewGuid():N}")
            .Options;

        return new CategoryTestDbContext(options);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(category => category.Id);
            entity.Property(category => category.Name)
                .IsRequired()
                .HasMaxLength(Category.MaxNameLength);
            entity.Property(category => category.Slug)
                .IsRequired()
                .HasMaxLength(Category.MaxNameLength);
            entity.Property(category => category.Description)
                .HasMaxLength(Category.MaxDescriptionLength);
            entity.Property(category => category.ImageUrl)
                .HasMaxLength(Category.MaxImageUrlLength);
            entity.HasIndex(category => category.Slug).IsUnique();
            // Mirror CategoryConfiguration của Infrastructure: query thông thường không trả
            // category đã soft delete.
            entity.HasQueryFilter(category => !category.IsDeleted);
        });

        // Recipe được map tối thiểu để test delete guard (Category còn Recipe) chạy được.
        // TV2 sở hữu Recipe; ở đây chỉ cần quan hệ CategoryId + global query filter.
        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.HasKey(recipe => recipe.Id);
            entity.Property(recipe => recipe.Title).IsRequired().HasMaxLength(200);
            entity.Property(recipe => recipe.Slug).IsRequired().HasMaxLength(220);
            entity.Property(recipe => recipe.AuthorId).IsRequired().HasMaxLength(450);
            entity.HasOne(recipe => recipe.Category)
                .WithMany(category => category.Recipes)
                .HasForeignKey(recipe => recipe.CategoryId);
            entity.HasQueryFilter(recipe => !recipe.IsDeleted);
            entity.Ignore(recipe => recipe.Ingredients);
            entity.Ignore(recipe => recipe.Steps);
            entity.Ignore(recipe => recipe.Images);
            entity.Ignore(recipe => recipe.Nutrition);
        });

        base.OnModelCreating(modelBuilder);
    }
}
