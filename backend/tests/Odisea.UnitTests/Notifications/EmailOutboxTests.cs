using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Odisea.Modules.Notifications.Domain;
using Odisea.Modules.Notifications.Features.Outbox;
using Odisea.Modules.Notifications.Infrastructure;
using Odisea.Modules.Notifications.PublicApi;
using Odisea.SharedKernel;
using Xunit;

namespace Odisea.UnitTests.Notifications;

public class EmailOutboxTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 25, 10, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeTransport : IEmailTransport
    {
        public string Name => "fake";
        public List<Guid> Sent { get; } = [];
        public Func<EmailMessage, Exception?> FailWith { get; set; } = _ => null;

        public Task SendAsync(EmailMessage message, CancellationToken ct)
        {
            if (FailWith(message) is { } ex)
                throw ex;
            Sent.Add(message.Id);
            return Task.CompletedTask;
        }
    }

    private readonly NotificationsDbContext _db;
    private readonly FakeClock _clock = new();
    private readonly FakeTransport _transport = new();
    private readonly EmailOptions _options = new() { BatchSize = 10, MaxAttempts = 3 };
    private readonly EmailOutbox _outbox;
    private readonly EmailDispatchRun _run;

    public EmailOutboxTests()
    {
        _db = new NotificationsDbContext(new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _outbox = new EmailOutbox(_db, _clock, NullLogger<EmailOutbox>.Instance);
        _run = new EmailDispatchRun(_db, _transport, Options.Create(_options), _clock, NullLogger<EmailDispatchRun>.Instance);
    }

    private Task<Guid> EnqueueAsync(string to = "customer@example.com", string subject = "Hello") =>
        _outbox.EnqueueAsync(new EmailRequest(to, subject, "Body", Category: "test", CorrelationId: "corr-1"), CancellationToken.None);

    [Fact]
    public async Task Enqueue_stores_a_pending_message_due_now()
    {
        var id = await EnqueueAsync();

        var message = await _db.EmailMessages.SingleAsync(m => m.Id == id);
        Assert.Equal(EmailStatus.Pending, message.Status);
        Assert.Equal(_clock.UtcNow, message.NextAttemptAt);
        Assert.Equal(0, message.Attempts);
        Assert.Equal("corr-1", message.CorrelationId);
    }

    [Theory]
    [InlineData("not-an-address")]
    [InlineData("Name <someone@example.com>")]
    [InlineData("")]
    public async Task Enqueue_rejects_invalid_recipients(string to)
    {
        await Assert.ThrowsAsync<EmailValidationException>(() => EnqueueAsync(to));
        Assert.Empty(_db.EmailMessages);
    }

    [Fact]
    public async Task Enqueue_rejects_blank_subject_and_body()
    {
        await Assert.ThrowsAsync<EmailValidationException>(() => EnqueueAsync(subject: " "));
        await Assert.ThrowsAsync<EmailValidationException>(() => _outbox.EnqueueAsync(
            new EmailRequest("a@example.com", "Subject", ""), CancellationToken.None));
    }

    [Fact]
    public async Task Dispatch_sends_due_messages_and_leaves_future_ones()
    {
        var due = await EnqueueAsync();
        _clock.UtcNow = _clock.UtcNow.AddMinutes(10);
        var later = await EnqueueAsync("later@example.com");
        _clock.UtcNow = _clock.UtcNow.AddMinutes(-5);

        var result = await _run.RunAsync(CancellationToken.None);

        Assert.Equal(new DispatchResult(1, 1, 0), result);
        Assert.Equal([due], _transport.Sent);
        Assert.Equal(EmailStatus.Sent, (await _db.EmailMessages.FindAsync(due))!.Status);
        Assert.Equal(EmailStatus.Pending, (await _db.EmailMessages.FindAsync(later))!.Status);
    }

    [Fact]
    public async Task Failures_back_off_and_park_as_failed_after_the_maximum_attempts()
    {
        var id = await EnqueueAsync();
        _transport.FailWith = _ => new InvalidOperationException("SMTP down");

        var first = await _run.RunAsync(CancellationToken.None);
        var message = (await _db.EmailMessages.FindAsync(id))!;
        Assert.Equal(new DispatchResult(1, 0, 1), first);
        Assert.Equal(EmailStatus.Pending, message.Status);
        Assert.Equal(1, message.Attempts);
        Assert.Equal(_clock.UtcNow.AddMinutes(1), message.NextAttemptAt);
        Assert.Equal("SMTP down", message.LastError);

        // Not due yet: nothing happens.
        Assert.Equal(new DispatchResult(0, 0, 0), await _run.RunAsync(CancellationToken.None));

        _clock.UtcNow = message.NextAttemptAt;
        await _run.RunAsync(CancellationToken.None);
        Assert.Equal(_clock.UtcNow.AddMinutes(5), message.NextAttemptAt);

        _clock.UtcNow = message.NextAttemptAt;
        await _run.RunAsync(CancellationToken.None);
        Assert.Equal(EmailStatus.Failed, message.Status);
        Assert.Equal(3, message.Attempts);

        // Operator retry reopens it; a working transport then sends it.
        message.Retry(_clock.UtcNow);
        await _db.SaveChangesAsync();
        _transport.FailWith = _ => null;
        Assert.Equal(new DispatchResult(1, 1, 0), await _run.RunAsync(CancellationToken.None));
        Assert.Equal(EmailStatus.Sent, message.Status);
        Assert.Null(message.LastError);
    }

    [Fact]
    public async Task Sent_messages_cannot_be_retried_or_cancelled_and_cancelled_ones_are_skipped()
    {
        var id = await EnqueueAsync();
        var other = await EnqueueAsync("other@example.com");
        var cancelled = (await _db.EmailMessages.FindAsync(other))!;
        cancelled.Cancel();
        await _db.SaveChangesAsync();

        await _run.RunAsync(CancellationToken.None);

        var sent = (await _db.EmailMessages.FindAsync(id))!;
        Assert.Equal(EmailStatus.Sent, sent.Status);
        Assert.Equal(EmailStatus.Cancelled, cancelled.Status);
        Assert.Throws<EmailStateException>(() => sent.Retry(_clock.UtcNow));
        Assert.Throws<EmailStateException>(() => sent.Cancel());
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 30)]
    [InlineData(4, 120)]
    [InlineData(9, 120)]
    public void Backoff_grows_and_caps_at_two_hours(int attempts, int minutes) =>
        Assert.Equal(TimeSpan.FromMinutes(minutes), EmailMessage.Backoff(attempts));
}
