using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace CulinaryBlog.Infrastructure.Persistence;
// ApplicationDbContext implement IApplicationDbContext (interface từ Application Layer)
// → Application Layer không phụ thuộc EF Core, chỉ phụ thuộc interface
public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }
    
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Áp dụng tất cả Entity Configuration trong assembly này
        // Thay vì cấu hình từng entity ở đây, ta tách ra file riêng
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ApplicationDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(x => typeof(BaseEntity).IsAssignableFrom(x.ClrType)))
        {
            entityType.FindProperty(nameof(BaseEntity.RowVersion))
                ?.SetDefaultValueSql("decode('00', 'hex')");
        }
        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
