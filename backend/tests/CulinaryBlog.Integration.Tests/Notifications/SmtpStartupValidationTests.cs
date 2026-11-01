using CulinaryBlog.Infrastructure.Notifications;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Notifications;

/// <summary>
/// Host-startup validation of AddSmtpConfiguration via Host.CreateDefaultBuilder.
/// Production registers SmtpProductionOptionsValidator + ValidateOnStart, so a
/// bad configuration must abort host.StartAsync() with OptionsValidationException
/// (possibly wrapped in AggregateException). Non-production skips the validator,
/// so MailHog with no credentials still boots.
/// No Docker, no database, no real SMTP server is touched.
/// </summary>
public sealed class SmtpStartupValidationTests
{
    private static IHost BuildHost(IDictionary<string, string?> values, bool isProduction)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((ctx, config) =>
            {
                config.AddInMemoryCollection(values);
            })
            .ConfigureServices((ctx, services) =>
            {
                services.AddSmtpConfiguration(ctx.Configuration, isProduction);
            })
            .Build();
    }

    private static IEnumerable<Exception> Flatten(Exception exception)
    {
        var pending = new Stack<Exception>();
        pending.Push(exception);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            yield return current;

            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.Flatten().InnerExceptions)
                {
                    pending.Push(inner);
                }
            }
            else if (current.InnerException is not null)
            {
                pending.Push(current.InnerException);
            }
        }
    }

    [Fact]
    public async Task Production_InvalidSmtp_HostStartAsync_ThrowsOptionsValidationException()
    {
        var host = BuildHost(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "mailhog",
            ["Smtp:Port"] = "1025",
            ["Smtp:Username"] = "",
            ["Smtp:Password"] = "",
            ["Smtp:FromAddress"] = "no-reply@culinaryblog.local"
        }, isProduction: true);

        using (host)
        {
            Exception? thrown = null;
            try
            {
                await host.StartAsync();
            }
            catch (Exception ex)
            {
                thrown = ex;
            }

            thrown.Should().NotBeNull("Production must reject invalid SMTP at startup");

            var chain = Flatten(thrown!).ToList();
            var validation = chain.OfType<OptionsValidationException>().FirstOrDefault();
            validation.Should().NotBeNull("the failure must surface as OptionsValidationException");

            var failures = validation!.Failures;
            failures.Should().Contain(f => f.Contains("mailhog", StringComparison.OrdinalIgnoreCase));
            failures.Should().Contain(f => f.Contains("Smtp:Username"));
            failures.Should().Contain(f => f.Contains("Smtp:Password"));
            failures.Should().Contain(f => f.Contains(".local", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task Production_CompleteSmtp_HostStartsAndStops()
    {
        var host = BuildHost(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "smtp.example.com",
            ["Smtp:Port"] = "587",
            ["Smtp:Username"] = "user",
            ["Smtp:Password"] = "pass",
            ["Smtp:FromAddress"] = "no-reply@culinaryblog.example"
        }, isProduction: true);

        using (host)
        {
            var act = async () => await host.StartAsync();
            await act.Should().NotThrowAsync("a complete Production SMTP profile must boot");

            await host.StopAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task Development_MailHogWithoutCredentials_HostStarts()
    {
        var host = BuildHost(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "mailhog",
            ["Smtp:Port"] = "1025",
            ["Smtp:Username"] = "",
            ["Smtp:Password"] = "",
            ["Smtp:FromAddress"] = "no-reply@culinaryblog.local"
        }, isProduction: false);

        using (host)
        {
            var act = async () => await host.StartAsync();
            await act.Should().NotThrowAsync("non-production must skip the validator and boot with MailHog");

            await host.StopAsync(TimeSpan.FromSeconds(5));
        }
    }
}