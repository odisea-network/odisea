namespace Odisea.Modules.Notifications.PublicApi;

/// Queues transactional e-mail. The call only writes a row; a background dispatcher sends it,
/// so no API request ever waits on an SMTP server.
public interface IEmailOutbox
{
    Task<Guid> EnqueueAsync(EmailRequest request, CancellationToken ct);
}

/// Category is a short machine tag ("inquiry-notification") for filtering the outbox;
/// CorrelationId links the mail to the thing that caused it (a submission id, a booking ref).
public sealed record EmailRequest(
    string To,
    string Subject,
    string TextBody,
    string? HtmlBody = null,
    string? ReplyTo = null,
    string Category = "general",
    Guid? AgencyId = null,
    string? CorrelationId = null,
    string? ToName = null);
