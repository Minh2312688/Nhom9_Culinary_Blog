using CulinaryBlog.Infrastructure.Notifications;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CulinaryBlog.Application.Tests.Infrastructure.Notifications;

/// <summary>
/// Startup-time validation of AddSmtpConfiguration (Production registers
/// SmtpProductionOptionsValidator + ValidateOnStart; non-production does not).
/// Resolving IOptions&lt;SmtpOptions&gt; triggers validation. The error
/// messages never contain secrets (username/password).
/// </summary>
public sealed class SmtpProductionStartupValidationTests
{
    private static IConfiguration BuildConfiguration(IDictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

    private static SmtpOptions ResolveOptions(
        IConfiguration configuration,
        bool isProduction)
    {
        var services = new ServiceCollection();
        services.AddSmtpConfiguration(configuration, isProduction);
        return services.BuildServiceProvider()
            .GetRequiredService<IOptions<SmtpOptions>>().Value;
    }

    [Fact]
    public void Production_MissingHost_Throws()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "",
            ["Smtp:Port"] = "587",
            ["Smtp:Username"] = "user",
            ["Smtp:Password"] = "pass",
            ["Smtp:FromAddress"] = "no-reply@culinaryblog.example"
        });

        var act = () => ResolveOptions(configuration, isProduction: true);

        var ex = act.Should().Throw<OptionsValidationException>().Which;
        ex.Failures.Should().Contain(f => f.Contains("Smtp:Host"));
    }

    [Fact]
    public void Production_MissingFromAddress_Throws()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "smtp.example.com",
            ["Smtp:Port"] = "587",
            ["Smtp:Username"] = "user",
            ["Smtp:Password"] = "pass",
            ["Smtp:FromAddress"] = ""
        });

        var act = () => ResolveOptions(configuration, isProduction: true);

        var ex = act.Should().Throw<OptionsValidationException>().Which;
        ex.Failures.Should().Contain(f => f.Contains("Smtp:FromAddress"));
    }

    [Fact]
    public void Production_MissingUsername_Throws()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "smtp.example.com",
            ["Smtp:Port"] = "587",
            ["Smtp:Username"] = "",
            ["Smtp:Password"] = "pass",
            ["Smtp:FromAddress"] = "no-reply@culinaryblog.example"
        });

        var act = () => ResolveOptions(configuration, isProduction: true);

        var ex = act.Should().Throw<OptionsValidationException>().Which;
        ex.Failures.Should().Contain(f => f.Contains("Smtp:Username"));
    }

    [Fact]
    public void Production_MissingPassword_Throws()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "smtp.example.com",
            ["Smtp:Port"] = "587",
            ["Smtp:Username"] = "user",
            ["Smtp:Password"] = "",
            ["Smtp:FromAddress"] = "no-reply@culinaryblog.example"
        });

        var act = () => ResolveOptions(configuration, isProduction: true);

        var ex = act.Should().Throw<OptionsValidationException>().Which;
        ex.Failures.Should().Contain(f => f.Contains("Smtp:Password"));
    }

    [Fact]
    public void Production_HostIsMailHog_Throws()
    {
        foreach (var host in new[] { "mailhog", "MailHog", "MAILHOG" })
        {
            var configuration = BuildConfiguration(new Dictionary<string, string?>
            {
                ["Smtp:Host"] = host,
                ["Smtp:Port"] = "1025",
                ["Smtp:Username"] = "user",
                ["Smtp:Password"] = "pass",
                ["Smtp:FromAddress"] = "no-reply@culinaryblog.example"
            });

            var act = () => ResolveOptions(configuration, isProduction: true);

            var ex = act.Should().Throw<OptionsValidationException>().Which;
            ex.Failures.Should().Contain(f => f.Contains("mailhog", StringComparison.OrdinalIgnoreCase),
                $"host '{host}' must be rejected in Production");
        }
    }

    [Fact]
    public void Production_FromAddressWithLocalDomain_Throws()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "smtp.example.com",
            ["Smtp:Port"] = "587",
            ["Smtp:Username"] = "user",
            ["Smtp:Password"] = "pass",
            ["Smtp:FromAddress"] = "no-reply@culinaryblog.local"
        });

        var act = () => ResolveOptions(configuration, isProduction: true);

        var ex = act.Should().Throw<OptionsValidationException>().Which;
        ex.Failures.Should().Contain(f => f.Contains("Smtp:FromAddress"));
    }

    [Fact]
    public void Production_CompleteConfiguration_Resolves()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "smtp.example.com",
            ["Smtp:Port"] = "587",
            ["Smtp:Username"] = "user",
            ["Smtp:Password"] = "pass",
            ["Smtp:FromAddress"] = "no-reply@culinaryblog.example"
        });

        var act = () => ResolveOptions(configuration, isProduction: true);

        act.Should().NotThrow();
        var options = ResolveOptions(configuration, isProduction: true);
        options.Host.Should().Be("smtp.example.com");
    }

    [Fact]
    public void NonProduction_WithMailHogAndNoCredentials_Resolves()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "mailhog",
            ["Smtp:Port"] = "1025",
            ["Smtp:Username"] = "",
            ["Smtp:Password"] = "",
            ["Smtp:FromAddress"] = "no-reply@culinaryblog.local"
        });

        var act = () => ResolveOptions(configuration, isProduction: false);

        act.Should().NotThrow();
    }

    [Fact]
    public void Production_ErrorMessage_NeverContainsSecrets()
    {
        const string secretPassword = "S3cr3t-P@ss-9";
        const string secretUsername = "smtp-user-xyz";

        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "mailhog",
            ["Smtp:Port"] = "1025",
            ["Smtp:Username"] = secretUsername,
            ["Smtp:Password"] = secretPassword,
            ["Smtp:FromAddress"] = "no-reply@culinaryblog.local"
        });

        var act = () => ResolveOptions(configuration, isProduction: true);

        var ex = act.Should().Throw<OptionsValidationException>().Which;
        ex.Message.Should().NotContain(secretPassword);
        ex.Message.Should().NotContain(secretUsername);
        foreach (var failure in ex.Failures)
        {
            failure.Should().NotContain(secretPassword);
            failure.Should().NotContain(secretUsername);
        }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("70000")]
    public void Production_InvalidPort_Throws(string port)
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "smtp.example.com",
            ["Smtp:Port"] = port,
            ["Smtp:Username"] = "user",
            ["Smtp:Password"] = "pass",
            ["Smtp:FromAddress"] = "no-reply@culinaryblog.example"
        });

        var act = () => ResolveOptions(configuration, isProduction: true);

        var ex = act.Should().Throw<OptionsValidationException>().Which;
        ex.Failures.Should().Contain(f => f.Contains("Port"));
    }
}