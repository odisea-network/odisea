using Microsoft.EntityFrameworkCore;
using Odisea.Application.Webhooks;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;
using Odisea.Infrastructure.Data;

namespace Odisea.UnitTests.Webhooks;

public class WebhookDispatcherTests
{
    private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    // Captures what the dispatcher tried to send.
    private sealed class CapturingSender : IWebhookSender
    {
        public List<(string Url, string Body, string Signature)> Sent { get; } = [];
        public bool Result { get; set; } = true;

        public Task<bool> SendAsync(string url, string body, string signature, CancellationToken ct)
        {
            Sent.Add((url, body, signature));
            return Task.FromResult(Result);
        }
    }

    private sealed class ThrowingSender : IWebhookSender
    {
        public Task<bool> SendAsync(string url, string body, string signature, CancellationToken ct) =>
            throw new HttpRequestException("down");
    }

    [Fact]
    public async Task Dispatch_SendsToActiveSubscribersWantingTheEvent()
    {
        await using var db = NewDb();
        var agency = Guid.NewGuid();
        db.WebhookSubscriptions.AddRange(
            new WebhookSubscription { AgencyId = agency, Url = "https://crm.example/hook", Secret = "s1", EventTypes = "lead.created", Status = WebhookStatus.Active },
            new WebhookSubscription { AgencyId = agency, Url = "https://other.example/hook", Secret = "s2", EventTypes = "offer.published", Status = WebhookStatus.Active },   // wrong event
            new WebhookSubscription { AgencyId = agency, Url = "https://disabled.example/hook", Secret = "s3", EventTypes = "lead.created", Status = WebhookStatus.Disabled }, // disabled
            new WebhookSubscription { AgencyId = Guid.NewGuid(), Url = "https://elsewhere.example/hook", Secret = "s4", EventTypes = "lead.created", Status = WebhookStatus.Active }); // other agency
        await db.SaveChangesAsync();

        var sender = new CapturingSender();
        await new WebhookDispatcher(db, sender).DispatchAsync(agency, "lead.created", new { id = 1 }, default);

        Assert.Single(sender.Sent);
        Assert.Equal("https://crm.example/hook", sender.Sent[0].Url);
    }

    [Fact]
    public async Task Dispatch_SignsBodyWithSubscriptionSecret()
    {
        await using var db = NewDb();
        var agency = Guid.NewGuid();
        db.WebhookSubscriptions.Add(new WebhookSubscription
        {
            AgencyId = agency, Url = "https://crm.example/hook", Secret = "topsecret",
            EventTypes = "lead.created", Status = WebhookStatus.Active,
        });
        await db.SaveChangesAsync();

        var sender = new CapturingSender();
        await new WebhookDispatcher(db, sender).DispatchAsync(agency, "lead.created", new { id = 1 }, default);

        var (_, body, signature) = sender.Sent.Single();
        Assert.Equal(WebhookDispatcher.Sign(body, "topsecret"), signature);
    }

    [Fact]
    public async Task Dispatch_NoMatchingSubscriptions_SendsNothing()
    {
        await using var db = NewDb();
        var sender = new CapturingSender();

        await new WebhookDispatcher(db, sender).DispatchAsync(Guid.NewGuid(), "lead.created", new { }, default);

        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task Dispatch_FailingSender_DoesNotThrow()
    {
        await using var db = NewDb();
        var agency = Guid.NewGuid();
        db.WebhookSubscriptions.Add(new WebhookSubscription
        {
            AgencyId = agency, Url = "https://crm.example/hook", Secret = "s",
            EventTypes = "lead.created", Status = WebhookStatus.Active,
        });
        await db.SaveChangesAsync();

        // A throwing sender must be swallowed — a dead receiver can't break the
        // request that triggered the event.
        var ex = await Record.ExceptionAsync(() =>
            new WebhookDispatcher(db, new ThrowingSender()).DispatchAsync(agency, "lead.created", new { }, default));

        Assert.Null(ex);
    }
}
