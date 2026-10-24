using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Odisea.Modules.Notifications.Domain;

namespace Odisea.Modules.Notifications.Features.Outbox;

/// Hands one message to the outside world. Implementations throw on failure; the dispatcher decides about retries.
public interface IEmailTransport
{
    string Name { get; }
    Task SendAsync(EmailMessage message, CancellationToken ct);
}

/// MailKit over SMTP; one connection per message keeps the code simple at the outbox's volume.
public sealed class SmtpEmailTransport(IOptions<EmailOptions> options) : IEmailTransport
{
    public string Name => "smtp";

    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var settings = options.Value;
        // Both are validated at startup whenever this transport is registered.
        var host = settings.Smtp.Host ?? throw new InvalidOperationException("Email:Smtp:Host is not configured.");
        var from = settings.From ?? throw new InvalidOperationException("Email:From is not configured.");
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(settings.FromName, from));
        mime.To.Add(new MailboxAddress(message.ToName ?? string.Empty, message.To));
        if (message.ReplyTo is { } replyTo)
            mime.ReplyTo.Add(MailboxAddress.Parse(replyTo));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { TextBody = message.TextBody, HtmlBody = message.HtmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        var security = settings.Smtp.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.SslOnConnect;
        await client.ConnectAsync(host, settings.Smtp.Port, security, ct);
        if (!string.IsNullOrEmpty(settings.Smtp.Username))
            await client.AuthenticateAsync(settings.Smtp.Username, settings.Smtp.Password ?? string.Empty, ct);
        await client.SendAsync(mime, ct);
        await client.DisconnectAsync(quit: true, ct);
    }
}

/// Development and tests: nothing leaves the machine, the log shows only the recipient's domain.
public sealed class LoggingEmailTransport(ILogger<LoggingEmailTransport> logger) : IEmailTransport
{
    public string Name => "logging";

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var at = message.To.LastIndexOf('@');
        logger.LogInformation("E-mail {EmailId} ({Category}) to a {Domain} address: \"{Subject}\" — no SMTP configured, marked as sent",
            message.Id, message.Category, at >= 0 ? message.To[(at + 1)..] : "?", message.Subject);
        return Task.CompletedTask;
    }
}
