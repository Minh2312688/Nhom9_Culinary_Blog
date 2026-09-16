using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CulinaryBlog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Phase 1 intentionally keeps infrastructure registrations minimal.
        // EF Core/Identity/Auth/Redis/MinIO implementations are added by the
        // assigned modules in later phases without changing dependency direction.
        return services;
    }
}
