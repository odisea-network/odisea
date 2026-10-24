using System.Net;
using System.Net.Http.Json;
using Odisea.Modules.Notifications.Domain;
using Odisea.Modules.Notifications.Features.Outbox;

namespace Odisea.IntegrationTests;

/// The factory runs with Email:Enabled=false and no SMTP host, so the logging transport sends
/// and nothing leaves the machine; dispatching is triggered through the admin endpoint.
[Collection("api")]
public class EmailOutboxTests(OdiseaApiFactory factory)
{
    [Fact]
    public async Task Outbox_is_operator_only_and_its_levers_are_admin_only()
    {
        var operatorUser = factory.ClientAs(TestIdentity.Operator);
        var agent = factory.ClientAs(TestIdentity.Agent);

        await (await agent.GetAsync("/api/v1/admin/emails")).ShouldBeAsync(HttpStatusCode.Forbidden);
        await (await operatorUser.GetAsync("/api/v1/admin/emails")).ShouldBeAsync(HttpStatusCode.OK);
        await (await operatorUser.PostAsJsonAsync("/api/v1/admin/emails/test", new SendTestEmailRequest("ops@example.com")))
            .ShouldBeAsync(HttpStatusCode.Forbidden);
        await (await operatorUser.PostAsync("/api/v1/admin/emails/dispatch", null)).ShouldBeAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Test_message_is_queued_dispatched_and_then_immutable()
    {
        var admin = factory.ClientAs(TestIdentity.Admin);

        var invalid = await admin.PostAsJsonAsync("/api/v1/admin/emails/test", new SendTestEmailRequest("nope"));
        await invalid.ShouldBeAsync(HttpStatusCode.BadRequest);

        var queued = await admin.PostAsJsonAsync("/api/v1/admin/emails/test", new SendTestEmailRequest("ops@example.com"));
        await queued.ShouldBeAsync(HttpStatusCode.Created);
        var message = await queued.Content.ReadFromJsonAsync<EmailMessageDto>(Json.Options);
        Assert.NotNull(message);
        Assert.Equal(EmailStatus.Pending, message.Status);
        Assert.Equal("test", message.Category);

        var dispatch = await admin.PostAsync("/api/v1/admin/emails/dispatch", null);
        await dispatch.ShouldBeAsync(HttpStatusCode.OK);
        var result = await dispatch.Content.ReadFromJsonAsync<DispatchResult>(Json.Options);
        Assert.NotNull(result);
        Assert.True(result.Sent >= 1);
        Assert.Equal(0, result.Failed);

        var detail = await admin.GetFromJsonAsync<EmailMessageDetailDto>($"/api/v1/admin/emails/{message.Id}", Json.Options);
        Assert.NotNull(detail);
        Assert.Equal(EmailStatus.Sent, detail.Summary.Status);
        Assert.Contains("logging", detail.TextBody);

        await (await admin.PostAsync($"/api/v1/admin/emails/{message.Id}/retry", null)).ShouldBeAsync(HttpStatusCode.Conflict);
        await (await admin.PostAsync($"/api/v1/admin/emails/{message.Id}/cancel", null)).ShouldBeAsync(HttpStatusCode.Conflict);

        var sentOnly = await admin.GetFromJsonAsync<List<EmailMessageDto>>("/api/v1/admin/emails?status=Sent&category=test", Json.Options);
        Assert.NotNull(sentOnly);
        Assert.Contains(sentOnly, m => m.Id == message.Id);
    }
}
