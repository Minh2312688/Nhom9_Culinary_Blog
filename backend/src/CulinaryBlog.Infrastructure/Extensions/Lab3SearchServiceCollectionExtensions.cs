using CulinaryBlog.Application.Abstractions.Search;
using CulinaryBlog.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace CulinaryBlog.Infrastructure.Extensions;

/// <summary>
/// Lab 03 Service Collection Extensions for Recipe Full-Text Search.
/// Merge-safe extension file to avoid conflicting modifications in shared DependencyInjection.cs.
/// Personal scope: Nguyen Pham Phu Nam (MSSV: 2312695).
/// </summary>
public static class Lab3SearchServiceCollectionExtensions
{
    public static IServiceCollection AddRecipeSearch(this IServiceCollection services)
    {
        services.AddScoped<IRecipeSearchRepository, PostgresRecipeSearchRepository>();
        return services;
    }
}
