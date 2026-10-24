using Microsoft.Extensions.Logging;
using MimeKit;
using Odisea.Modules.Notifications.Domain;
using Odisea.Modules.Notifications.Infrastructure;
using Odisea.Modules.Notifications.PublicApi;
using Odisea.SharedKernel;

namespace Odisea.Modules.Notifications.Features.Outbox;

public sealed class EmailOutbox(NotificationsDbContext db, IClock clock, ILogger<EmailOutbox> logger) : IEmailOutbox
{
    private const int MaxTextBody = 100_000;
    private const int MaxHtmlBody = 500_000;

    public async Task<Guid> EnqueueAsync(EmailRequest request, CancellationToken ct)
    {
        var message = new EmailMessage
        {
            To = RequireAddress(request.To, "recipient"),
            ToName = Trim(request.ToName, 200, "recipient name"),
            ReplyTo = string.IsNullOrWhiteSpace(request.ReplyTo) ? null : RequireAddress(request.ReplyTo, "reply-to"),
            Subject = Require(request.Subject, 200, "subject"),
            TextBody = Require(request.TextBody, MaxTextBody, "text body"),
            HtmlBody = Trim(request.HtmlBody, MaxHtmlBody, "HTML body"),
            Category = Require(request.Category, 50, "category"),
            AgencyId = request.AgencyId,
            CorrelationId = Trim(request.CorrelationId, 100, "correlation id"),
            NextAttemptAt = clock.UtcNow,
        };
        db.EmailMessages.Add(message);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("E-mail {EmailId} queued ({Category}) for agency {AgencyId}", message.Id, message.Category, message.AgencyId);
        return message.Id;
    }

    /// Accepts "name@host" only (no display names), so a stored address is always a plain mailbox.
    public static string RequireAddress(string? value, string what)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > 320
            || !MailboxAddress.TryParse(trimmed, out var parsed) || parsed.Address != trimmed || !trimmed.Contains('@'))
            throw new EmailValidationException($"The {what} address is not valid.");
        return trimmed;
    }

    private static string Require(string? value, int max, string what)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new EmailValidationException($"The {what} is required.");
        if (trimmed.Length > max)
            throw new EmailValidationException($"The {what} exceeds {max} characters.");
        return trimmed;
    }

    private static string? Trim(string? value, int max, string what)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;
        if (trimmed.Length > max)
            throw new EmailValidationException($"The {what} exceeds {max} characters.");
        return trimmed;
    }
}
