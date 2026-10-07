using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CulinaryBlog.Infrastructure.Persistence.Configurations;

public sealed class RecipeImageConfiguration : IEntityTypeConfiguration<RecipeImage>
{
    public void Configure(EntityTypeBuilder<RecipeImage> builder)
    {
        builder.ToTable("RecipeImages");
        builder.HasKey(image => image.Id);
        builder.Property(image => image.OriginalUrl).IsRequired().HasMaxLength(500);
        builder.Property(image => image.MediumUrl).HasMaxLength(500);
        builder.Property(image => image.ThumbnailUrl).HasMaxLength(500);
        builder.Property(image => image.AltText).HasMaxLength(200);
        builder.HasQueryFilter(image => !image.IsDeleted);
    }
}
