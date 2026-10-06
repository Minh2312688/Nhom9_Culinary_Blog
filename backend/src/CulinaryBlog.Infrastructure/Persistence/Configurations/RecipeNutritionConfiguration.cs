using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CulinaryBlog.Infrastructure.Persistence.Configurations;

public sealed class RecipeNutritionConfiguration : IEntityTypeConfiguration<RecipeNutrition>
{
    public void Configure(EntityTypeBuilder<RecipeNutrition> builder)
    {
        builder.ToTable("RecipeNutritions");
        builder.HasKey(nutrition => nutrition.Id);
        builder.Property(nutrition => nutrition.Protein).HasPrecision(8, 2);
        builder.Property(nutrition => nutrition.Carbohydrates).HasPrecision(8, 2);
        builder.Property(nutrition => nutrition.Fat).HasPrecision(8, 2);
        builder.Property(nutrition => nutrition.Fiber).HasPrecision(8, 2);
        builder.Property(nutrition => nutrition.Sodium).HasPrecision(8, 2);
        builder.HasQueryFilter(nutrition => !nutrition.IsDeleted);
    }
}
