using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CulinaryBlog.Infrastructure.Persistence.Configurations;

public class RecipeIngredientConfiguration : IEntityTypeConfiguration<RecipeIngredient>
{
    public void Configure(EntityTypeBuilder<RecipeIngredient> builder)
    {
        builder.ToTable("RecipeIngredients");

        builder.HasKey(ri => ri.Id);

        builder.Property(ri => ri.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(ri => ri.Quantity)
            .HasPrecision(10, 3);

        builder.Property(ri => ri.Unit)
            .HasMaxLength(50);

        builder.Property(ri => ri.Notes)
            .HasMaxLength(500);

        builder.Property(ri => ri.RowVersion)
            .IsRowVersion();

        builder.HasQueryFilter(ri => !ri.IsDeleted);
    }
}
