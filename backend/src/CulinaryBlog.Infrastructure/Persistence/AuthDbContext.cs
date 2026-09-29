using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Infrastructure.Identity;
using ApplicationUser = CulinaryBlog.Infrastructure.Identity.ApplicationUser;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Persistence;

/// <summary>
/// TEMPORARY LAB-02 PERSONAL DATABASE CONTEXT
/// Personal Database Context for TV1 Week 2 / Lab 02.
/// </summary>
public class AuthDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>, IApplicationDbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
    {
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<RecipeStep> RecipeSteps => Set<RecipeStep>();
    public DbSet<RecipeImage> RecipeImages => Set<RecipeImage>();
    public DbSet<RecipeNutrition> RecipeNutritions => Set<RecipeNutrition>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.Property(e => e.TokenHash).IsRequired().HasMaxLength(256);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.ExpiresAt).IsRequired();
        });

        builder.ApplyConfigurationsFromAssembly(typeof(AuthDbContext).Assembly);

        foreach (var entityType in builder.Model.GetEntityTypes()
                     .Where(x => typeof(BaseEntity).IsAssignableFrom(x.ClrType)))
        {
            entityType.FindProperty(nameof(BaseEntity.RowVersion))
                ?.SetDefaultValueSql("decode('00', 'hex')");
        }
    }

    public void SetOriginalRowVersion<T>(T entity, byte[] rowVersion) where T : class =>
        Entry(entity).Property(nameof(BaseEntity.RowVersion)).OriginalValue = rowVersion;

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
