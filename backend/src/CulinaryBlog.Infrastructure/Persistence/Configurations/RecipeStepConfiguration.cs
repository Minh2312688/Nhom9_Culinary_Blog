using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CulinaryBlog.Infrastructure.Persistence.Configurations;

public class RecipeStepConfiguration : IEntityTypeConfiguration<RecipeStep>
{
    public void Configure(EntityTypeBuilder<RecipeStep> builder)
    {
        builder.ToTable("RecipeSteps");

        builder.HasKey(rs => rs.Id);

        builder.Property(rs => rs.StepNumber)
            .IsRequired();

        builder.Property(rs => rs.Title)
            .HasMaxLength(200);

        builder.Property(rs => rs.Description)
            .IsRequired();

        builder.Property(rs => rs.ImageUrl)
            .HasMaxLength(500);

        builder.HasIndex(rs => new { rs.RecipeId, rs.StepNumber })
            .IsUnique();

        builder.Property(rs => rs.RowVersion)
            .IsRowVersion();

        builder.HasQueryFilter(rs => !rs.IsDeleted);
    }
}
