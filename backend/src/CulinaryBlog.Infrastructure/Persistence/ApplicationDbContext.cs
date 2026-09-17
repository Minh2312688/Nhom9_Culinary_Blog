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
 protected override void OnModelCreating(ModelBuilder modelBuilder)
 {
 // Áp dụng tất cả Entity Configuration trong assembly này
 // Thay vì cấu hình từng entity ở đây, ta tách ra file riêng
 modelBuilder.ApplyConfigurationsFromAssembly(
 typeof(ApplicationDbContext).Assembly);
 base.OnModelCreating(modelBuilder);
 }
}
