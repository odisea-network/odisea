using System.ComponentModel.DataAnnotations;

namespace Odisea.Modules.Notifications.Features.Outbox;

/// Section "Email". SMTP credentials come from user-secrets or the environment, never from
/// committed settings. Without an SMTP host the outbox still fills, and a logging transport
/// marks messages as sent (development and tests).
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// Turns the background dispatcher on or off; the outbox itself always accepts messages.
    public bool Enabled { get; set; } = true;

    /// Validated in the module (a plain mailbox address) so an empty placeholder in appsettings is fine.
    public string? From { get; set; }

    [MaxLength(200)]
    public string FromName { get; set; } = "Odisea";

    public SmtpOptions Smtp { get; set; } = new();

    [Range(1, 300)]
    public int PollSeconds { get; set; } = 5;

    [Range(1, 500)]
    public int BatchSize { get; set; } = 20;

    [Range(1, 20)]
    public int MaxAttempts { get; set; } = 5;

    public bool SmtpConfigured => !string.IsNullOrWhiteSpace(Smtp.Host);

    public sealed class SmtpOptions
    {
        public string? Host { get; set; }

        [Range(1, 65535)]
        public int Port { get; set; } = 587;

        public string? Username { get; set; }
        public string? Password { get; set; }

        /// STARTTLS on the submission port (587); false means implicit TLS (465).
        public bool UseStartTls { get; set; } = true;
    }
}
