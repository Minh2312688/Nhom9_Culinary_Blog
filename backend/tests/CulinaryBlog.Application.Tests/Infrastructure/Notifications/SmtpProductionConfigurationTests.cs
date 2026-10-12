using CulinaryBlog.Application.Contracts.Notifications;
using CulinaryBlog.Infrastructure.Notifications;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace CulinaryBlog.Application.Tests.Infrastructure.Notifications;

/// <summary>
/// FR-JOB-001 SMTP production configuration (SRS-AUDIT NFR-SEC-007):
/// production SMTP settings (host, port, SSL, username, password, from
/// address) must be supplied from environment/secrets through the standard
/// "Smtp" configuration section - never hard-coded - and the sender must
/// never write the password to logs. Deployment env vars / secrets surface
/// as Smtp__Host, Smtp__Port, ... keys, so the in-memory keys below mirror
/// that exact binding path used by DependencyInjection at startup.
///
/// These tests exercise only the configuration binding and the failure-log
/// path. They never send real email and never use real production
/// credentials (the live production-provider send is intentionally out of
/// scope for isolated verification).
/// </summary>
public sealed class SmtpProductionConfigurationTests
{
    // Production-shaped profile: authenticated + TLS + non-default port.
    // All values are placeholders - never committed real secrets.
    private const string ProdHost = "smtp.production.example";
    private const string ProdPassword = "placeholder-prod-password-never-committed";

    private static IConfiguration BuildProductionConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Mirrors Smtp__Host / Smtp__Port / ... from env vars or secrets.
                ["Smtp:Host"] = ProdHost,
                ["Smtp:Port"] = "587",
                ["Smtp:UseSsl"] = "true",
                ["Smtp:Username"] = "smtp-user",
                ["Smtp:Password"] = ProdPassword,
                ["Smtp:FromAddress"] = "no-reply@culinaryblog.example",
                ["Smtp:FromName"] = "Culinary Blog"
            })
            .Build();

    [Fact]
    public void ProductionProfile_BindsFromEnvironmentStyleConfiguration_AndValidates()
    {
        // Arrange: configuration keys shaped like deployment env vars/secrets.
        var configuration = BuildProductionConfiguration();

        // Act: same configuration-to-options binding the DI container relies on.
        var options = configuration
            .GetSection(SmtpOptions.SectionName)
            .Get<SmtpOptions>()!;

        // Assert: every production field, including credentials + TLS, binds.
        options.Host.Should().Be(ProdHost);
        options.Port.Should().Be(587);
        options.UseSsl.Should().BeTrue();
        options.Username.Should().Be("smtp-user");
        options.Password.Should().Be(ProdPassword);
        options.FromAddress.Should().Be("no-reply@culinaryblog.example");

        // A complete production profile passes validation.
        options.Invoking(o => o.Validate()).Should().NotThrow();
    }

    [Fact]
    public void MissingHost_InProductionConfiguration_ThrowsClearError_WithoutLeakingPassword()
    {
        // Arrange: production profile with an empty host (misconfiguration).
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Smtp:Host"] = "",
                ["Smtp:Port"] = "587",
                ["Smtp:UseSsl"] = "true",
                ["Smtp:Username"] = "smtp-user",
                ["Smtp:Password"] = ProdPassword,
                ["Smtp:FromAddress"] = "no-reply@culinaryblog.example"
            })
            .Build();

        var options = configuration
            .GetSection(SmtpOptions.SectionName)
            .Get<SmtpOptions>()!;

        // Act
        var act = () => options.Validate();

        // Assert: actionable error that names the missing key, never the secret.
        var ex = act.Should().Throw<InvalidOperationException>().Which;
        ex.Message.Should().Contain("Smtp:Host");
        ex.ToString().Should().NotContain(ProdPassword);
    }

    [Fact]
    public async Task SenderFailure_NeverLogsPassword_AndRethrows()
    {
        // Arrange: force a connection failure against an immediately-refused
        // local endpoint (127.0.0.1:1). This is isolated - it never touches a
        // shared service and never sends mail.
        var options = new SmtpOptions
        {
            Host = "127.0.0.1",
            Port = 1,
            UseSsl = false,
            Username = "smtp-user",
            Password = ProdPassword,
            FromAddress = "no-reply@culinaryblog.example"
        };
        var logger = new CapturingLogger();
        var sender = new SmtpEmailSender(Options.Create(options), logger);
        var message = new EmailMessage("user@example.com", "Subject", "<p>hi</p>");

        // Act: the failure must propagate so Hangfire can retry (no fake success).
        var act = () => sender.SendAsync(message);
        await act.Should().ThrowAsync<Exception>();

        // Assert: the failure log carries only host/port/recipient context,
        // never the password (NFR-SEC-007).
        logger.Entries.Should().ContainSingle();
        var entry = logger.Entries[0];
        entry.Level.Should().Be(LogLevel.Error);
        entry.Message.Should().Contain("127.0.0.1");
        entry.Message.Should().NotContain(ProdPassword);
    }

    private sealed class CapturingLogger : ILogger<SmtpEmailSender>
    {
        public List<CapturedLog> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new CapturedLog(logLevel, formatter(state, exception)));
        }
    }

    private sealed record CapturedLog(LogLevel Level, string Message);
}

