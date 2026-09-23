using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Mapster;
using MapsterMapper;
using FluentValidation;
using CulinaryBlog.Application.Common.Behaviors;
namespace CulinaryBlog.Application;
public static class DependencyInjection
{
 public static IServiceCollection AddApplication(this IServiceCollection
services)
 {
 // Đăng ký MediatR — tự động tìm tất cả Handler, Validator
 // trong Assembly của Application Layer
 services.AddMediatR(cfg =>
 {
 cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
 cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
 });
 services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
 // Cấu hình Mapster — tự động đăng ký TypeAdapterConfig
 var config = TypeAdapterConfig.GlobalSettings;
 config.Scan(Assembly.GetExecutingAssembly());
 services.AddSingleton(config);
 services.AddScoped<IMapper, ServiceMapper>();
 return services;
 }
}
