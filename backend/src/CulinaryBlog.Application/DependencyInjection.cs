using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using CulinaryBlog.Application.Common.Behaviors;
using FluentValidation;
using Mapster;
using MapsterMapper;
namespace CulinaryBlog.Application;
public static class DependencyInjection
{
 public static IServiceCollection AddApplication(this IServiceCollection
services)
 {
 // Đăng ký MediatR — tự động tìm tất cả Handler trong Assembly của Application Layer
 // Pipeline behavior dạng open generic phải đăng ký tường minh, MediatR không tự tìm được
 services.AddMediatR(cfg =>
 {
 cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
 cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
 });
 // Đăng ký toàn bộ FluentValidation validator của Application Layer
 // để ValidationBehavior thực sự chạy validation trước khi handler được gọi
 services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
 // Cấu hình Mapster — tự động đăng ký TypeAdapterConfig
 var config = TypeAdapterConfig.GlobalSettings;
 config.Scan(Assembly.GetExecutingAssembly());
 services.AddSingleton(config);
 services.AddScoped<IMapper, ServiceMapper>();
 return services;
 }
}
