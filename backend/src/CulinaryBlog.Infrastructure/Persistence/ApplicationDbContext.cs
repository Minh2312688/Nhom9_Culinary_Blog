using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using IdentityApplicationUser = CulinaryBlog.Infrastructure.Identity.ApplicationUser;
namespace CulinaryBlog.Infrastructure.Persistence;
// ApplicationDbContext implement IApplicationDbContext (interface từ Application Layer)
// → Application Layer không phụ thuộc EF Core, chỉ phụ thuộc interface
public class ApplicationDbContext : IdentityDbContext<IdentityApplicationUser, IdentityRole, string>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }
    
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
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
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ApplicationDbContext).Assembly,
            type => type.Namespace ==
                typeof(CulinaryBlog.Infrastructure.Persistence.Configurations.CategoryConfiguration).Namespace);

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(token => token.Id);
            entity.HasIndex(token => token.TokenHash).IsUnique();
            entity.Property(token => token.TokenHash).IsRequired().HasMaxLength(256);
            entity.Property(token => token.UserId).IsRequired();
            entity.Property(token => token.CreatedAt).IsRequired();
            entity.Property(token => token.ExpiresAt).IsRequired();
        });

        // PostgreSQL-specific search storage must not be added to test contexts
        // using EF InMemory or other providers.
        if (Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            var recipe = modelBuilder.Entity<Recipe>();
            recipe.Property<NpgsqlTsVector>("SearchVector")
                .HasColumnType("tsvector")
                .IsRequired()
                .ValueGeneratedOnAddOrUpdate();
            recipe.HasIndex("SearchVector")
                .HasDatabaseName("IDX_Recipe_Search")
                .HasMethod("GIN");
        }

        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(x => typeof(BaseEntity).IsAssignableFrom(x.ClrType)))
        {
            entityType.FindProperty(nameof(BaseEntity.RowVersion))
                ?.SetDefaultValueSql("decode('00', 'hex')");
        }
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
