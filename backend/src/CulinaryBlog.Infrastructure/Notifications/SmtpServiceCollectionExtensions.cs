using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CulinaryBlog.Infrastructure.Notifications;

/// <summary>
/// Extension methods for SMTP options binding. Production additionally
/// registers <see cref="SmtpProductionOptionsValidator"/> and enables
/// <c>ValidateOnStart</c> so the host fails fast before any email is sent.
/// Non-production keeps the old behavior: bind only, validate at send time.
/// </summary>
public static class SmtpServiceCollectionExtensions
{
    public static IServiceCollection AddSmtpConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isProduction)
    {
        var builder = services
            .AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection(SmtpOptions.SectionName));

        if (isProduction)
        {
            // Register only once; keeps the validator idempotent across repeated
            // calls (e.g. when multiple modules wire SMTP options).
            if (!services.Any(descriptor =>
                    descriptor.ServiceType == typeof(IValidateOptions<SmtpOptions>)
                    && descriptor.ImplementationType == typeof(SmtpProductionOptionsValidator)))
            {
                services.Add(
                    ServiceDescriptor.Singleton<IValidateOptions<SmtpOptions>, SmtpProductionOptionsValidator>());
            }

            builder.ValidateOnStart();
        }

        return services;
    }
}