using Microsoft.Extensions.Options;

namespace CulinaryBlog.Infrastructure.Notifications;

/// <summary>
/// Validates <see cref="SmtpOptions"/> at host startup ONLY when the
/// environment is Production. Development/Testing/e2e skip this validator
/// so MailHog (no credentials, no TLS) keeps working.
/// The error message never contains secrets (username/password).
/// </summary>
public sealed class SmtpProductionOptionsValidator : IValidateOptions<SmtpOptions>
{
    public ValidateOptionsResult Validate(string? name, SmtpOptions options)
    {
        var failures = new List<string>();

        // Reuse the existing base checks (Host required, Port 1..65535,
        // FromAddress required). Their messages name the missing key only.
        try
        {
            options.Validate();
        }
        catch (InvalidOperationException ex)
        {
            failures.Add(ex.Message);
        }

        if (string.Equals(options.Host, "mailhog", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add(
                "Smtp:Host must not be 'mailhog' in Production (MailHog is for development/demo only).");
        }

        if (string.IsNullOrWhiteSpace(options.Username))
        {
            failures.Add("Smtp:Username is required in Production.");
        }

        if (string.IsNullOrWhiteSpace(options.Password))
        {
            failures.Add("Smtp:Password is required in Production.");
        }

        if (!string.IsNullOrEmpty(options.FromAddress)
            && options.FromAddress.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add(
                "Smtp:FromAddress must be a real sender address in Production (placeholder '.local' domain).");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}