using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CulinaryBlog.Infrastructure.Configurations;

public class RecipeImageConfiguration : IEntityTypeConfiguration<RecipeImage>
{
    public void Configure(EntityTypeBuilder<RecipeImage> builder)
    {
        builder.ToTable("RecipeImages");
        builder.HasKey(x => x.Id);
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.Property(x => x.OriginalUrl).IsRequired().HasMaxLength(500);
        builder.Property(x => x.MediumUrl).HasMaxLength(500);
        builder.Property(x => x.ThumbnailUrl).HasMaxLength(500);
        builder.Property(x => x.AltText).HasMaxLength(200);
    }
}

public class RecipeNutritionConfiguration : IEntityTypeConfiguration<RecipeNutrition>
{
    public void Configure(EntityTypeBuilder<RecipeNutrition> builder)
    {
        builder.ToTable("RecipeNutritions");
        builder.HasKey(x => x.Id);
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.Property(x => x.Protein).HasColumnType("decimal(8,2)");
        builder.Property(x => x.Carbohydrates).HasColumnType("decimal(8,2)");
        builder.Property(x => x.Fat).HasColumnType("decimal(8,2)");
        builder.Property(x => x.Fiber).HasColumnType("decimal(8,2)");
        builder.Property(x => x.Sodium).HasColumnType("decimal(8,2)");
    }
}
