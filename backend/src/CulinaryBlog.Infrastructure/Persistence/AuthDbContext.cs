using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Identity;
using ApplicationUser = CulinaryBlog.Infrastructure.Identity.ApplicationUser;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Persistence;

/// <summary>
/// Minimal Auth DbContext for TV1 Week 2 authentication operations.
/// TV2 is the lead for the consolidated DbContext and migrations.
/// </summary>
public class AuthDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
    {
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

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
    }
}
