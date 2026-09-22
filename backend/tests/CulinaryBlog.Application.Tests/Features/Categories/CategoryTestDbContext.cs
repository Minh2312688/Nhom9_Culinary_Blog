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
            entity.HasIndex(category => category.Slug).IsUnique();
        });

        modelBuilder.Ignore<Recipe>();
        modelBuilder.Ignore<RecipeIngredient>();
        modelBuilder.Ignore<RecipeStep>();

        base.OnModelCreating(modelBuilder);
    }
}
