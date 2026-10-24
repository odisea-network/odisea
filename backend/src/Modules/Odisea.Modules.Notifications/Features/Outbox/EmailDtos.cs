using System.ComponentModel.DataAnnotations;
using Odisea.Modules.Notifications.Domain;

namespace Odisea.Modules.Notifications.Features.Outbox;

public sealed record EmailMessageDto(
    Guid Id,
    string To,
    string? ToName,
    string? ReplyTo,
    string Subject,
    string Category,
    Guid? AgencyId,
    string? CorrelationId,
    EmailStatus Status,
    int Attempts,
    DateTimeOffset NextAttemptAt,
    string? LastError,
    DateTimeOffset? SentAt,
    DateTimeOffset CreatedAt);

public sealed record EmailMessageDetailDto(
    EmailMessageDto Summary,
    string TextBody,
    string? HtmlBody);

public sealed record SendTestEmailRequest([Required, EmailAddress, MaxLength(320)] string To);

public static class EmailDtoMapping
{
    public static EmailMessageDto ToDto(this EmailMessage m) => new(
        m.Id, m.To, m.ToName, m.ReplyTo, m.Subject, m.Category, m.AgencyId, m.CorrelationId,
        m.Status, m.Attempts, m.NextAttemptAt, m.LastError, m.SentAt, m.CreatedAt);

    public static EmailMessageDetailDto ToDetailDto(this EmailMessage m) => new(m.ToDto(), m.TextBody, m.HtmlBody);
}
