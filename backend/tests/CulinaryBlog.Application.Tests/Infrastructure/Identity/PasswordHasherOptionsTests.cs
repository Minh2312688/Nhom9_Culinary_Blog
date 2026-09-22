using CulinaryBlog.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CulinaryBlog.Application.Tests.Infrastructure.Identity;

public class PasswordHasherOptionsTests
{
    [Fact]
    public void AddInfrastructure_ShouldConfigurePasswordHasherOptions_WithIterationCountAtLeast100000()
    {
        // Arrange (NFR-SEC-001 / Safety Patch 2)
        var services = new ServiceCollection();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:Postgres", "Host=localhost;Database=test;Username=test;Password=test" },
                { "Jwt:Key", "TestSecretKeyForTestingOnly_Min256Bits!" }
            })
            .Build();

        // Act
        services.AddInfrastructure(configuration);
        var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<PasswordHasherOptions>>().Value;

        // Assert
        options.IterationCount.Should().BeGreaterThanOrEqualTo(100000,
            "NFR-SEC-001 requires PBKDF2 iteration count >= 100,000");
    }
}
