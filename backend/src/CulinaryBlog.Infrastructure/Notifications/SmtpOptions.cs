namespace CulinaryBlog.Infrastructure.Notifications;

/// <summary>
/// SMTP configuration bound from the "Smtp" section.
/// All values come from configuration/environment; never hard-code credentials.
/// </summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 25;
    public bool UseSsl { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "Culinary Blog";

    /// <summary>
    /// Validates configuration. Throws <see cref="InvalidOperationException"/>
    /// with a message that never contains secrets.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            throw new InvalidOperationException(
                "SMTP configuration is incomplete: 'Smtp:Host' is required.");
        }

        if (Port is < 1 or > 65535)
        {
            throw new InvalidOperationException(
                "SMTP configuration is invalid: 'Smtp:Port' must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(FromAddress))
        {
            throw new InvalidOperationException(
                "SMTP configuration is incomplete: 'Smtp:FromAddress' is required.");
        }
    }
}
