using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace CulinaryBlog.Infrastructure;
public static class DependencyInjection
{
 public static IServiceCollection AddInfrastructure(
 this IServiceCollection services,
 IConfiguration configuration)
 {
 // Đăng ký PostgreSQL với EF Core
 services.AddDbContext<ApplicationDbContext>(options =>
 {
 options.UseNpgsql(
 configuration.GetConnectionString("DefaultConnection"),
 npgsql => npgsql.MigrationsAssembly(
 typeof(ApplicationDbContext).Assembly.FullName));
 });
 // Đăng ký IApplicationDbContext → ApplicationDbContext
 // Scoped: mỗi HTTP request có một DbContext instance riêng
 services.AddScoped<IApplicationDbContext, ApplicationDbContext>();
 return services;
 }
}