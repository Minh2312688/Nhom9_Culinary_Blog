using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.Contracts;
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
	configuration.GetConnectionString("Postgres") ?? configuration.GetConnectionString("DefaultConnection"),
 npgsql => npgsql.MigrationsAssembly(
 typeof(ApplicationDbContext).Assembly.FullName));
 });
 var redisConnection = configuration["Redis:ConnectionString"];
 if (!string.IsNullOrWhiteSpace(redisConnection))
 {
  services.AddStackExchangeRedisCache(options => options.Configuration = redisConnection);
 }
 else
 {
  services.AddDistributedMemoryCache();
 }
 services.AddScoped<IRecipeCache, Persistence.DistributedRecipeCache>();
 // Đăng ký IApplicationDbContext → ApplicationDbContext
 // Scoped: mỗi HTTP request có một DbContext instance riêng
 services.AddScoped<IApplicationDbContext, ApplicationDbContext>();
 return services;
 }
}