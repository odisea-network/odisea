using Odisea.SharedKernel;

namespace Odisea.Modules.Notifications.Domain;

/// One outbound e-mail in the outbox. Bodies are kept so a failed send can be retried and an
/// operator can see exactly what went out; they are never written to logs.
public class EmailMessage : Entity
{
    public required string To { get; set; }
    public string? ToName { get; set; }
    public string? ReplyTo { get; set; }
    public required string Subject { get; set; }
    public required string TextBody { get; set; }
    public string? HtmlBody { get; set; }

    public required string Category { get; set; }
    public Guid? AgencyId { get; set; }
    public string? CorrelationId { get; set; }

    public EmailStatus Status { get; private set; } = EmailStatus.Pending;
    public int Attempts { get; private set; }
    public required DateTimeOffset NextAttemptAt { get; set; }
    public string? LastError { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }

    public void MarkSent(DateTimeOffset now)
    {
        Attempts++;
        Status = EmailStatus.Sent;
        SentAt = now;
        LastError = null;
    }

    /// Schedules the next try with growing delays; after maxAttempts the message is parked as Failed
    /// and only an explicit retry reopens it.
    public void MarkFailed(string error, DateTimeOffset now, int maxAttempts)
    {
        Attempts++;
        LastError = error.Length > 1000 ? error[..1000] : error;
        if (Attempts >= maxAttempts)
        {
            Status = EmailStatus.Failed;
            return;
        }
        Status = EmailStatus.Pending;
        NextAttemptAt = now + Backoff(Attempts);
    }

    public void Retry(DateTimeOffset now)
    {
        if (Status == EmailStatus.Sent)
            throw new EmailStateException("The e-mail was already sent.");
        Status = EmailStatus.Pending;
        NextAttemptAt = now;
    }

    public void Cancel()
    {
        if (Status == EmailStatus.Sent)
            throw new EmailStateException("The e-mail was already sent.");
        Status = EmailStatus.Cancelled;
    }

    public static TimeSpan Backoff(int attempts) => attempts switch
    {
        <= 1 => TimeSpan.FromMinutes(1),
        2 => TimeSpan.FromMinutes(5),
        3 => TimeSpan.FromMinutes(30),
        _ => TimeSpan.FromHours(2),
    };
}

public enum EmailStatus
{
    Pending,
    Sent,
    Failed,
    Cancelled,
}

public sealed class EmailValidationException(string message) : DomainException(message);

public sealed class EmailStateException(string message) : DomainException(message);

public sealed class EmailNotFoundException() : DomainException("The e-mail was not found.");
