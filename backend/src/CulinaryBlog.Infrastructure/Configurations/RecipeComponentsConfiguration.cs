using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CulinaryBlog.Infrastructure.Configurations;

public class RecipeStepConfiguration : IEntityTypeConfiguration<RecipeStep>
{
    public void Configure(EntityTypeBuilder<RecipeStep> builder)
    {
        builder.ToTable("RecipeSteps");
        builder.HasKey(x => x.Id);
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.Property(x => x.Title).HasMaxLength(150);
        builder.Property(x => x.Description).IsRequired();
        builder.Property(x => x.ImageUrl).HasMaxLength(500);
    }
}

public class RecipeIngredientConfiguration : IEntityTypeConfiguration<RecipeIngredient>
{
    public void Configure(EntityTypeBuilder<RecipeIngredient> builder)
    {
        builder.ToTable("RecipeIngredients");
        builder.HasKey(x => x.Id);
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Unit).HasMaxLength(50);
        builder.Property(x => x.Notes).HasMaxLength(200);
        builder.Property(x => x.Quantity).HasColumnType("decimal(18,2)");
    }
}

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
