using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Odisea.Modules.Notifications.Features.Outbox;
using Odisea.Modules.Workspace.Domain;
using Odisea.Modules.Workspace.Features.ExternalReservations;
using Odisea.Modules.Workspace.Features.Inquiries;
using Odisea.Modules.Workspace.Features.Usage;

namespace Odisea.IntegrationTests;

[Collection("api")]
public class InquiryFormTests(OdiseaApiFactory factory)
{
    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var dto = await response.Content.ReadFromJsonAsync<T>(Json.Options);
        Assert.NotNull(dto);
        return dto;
    }

    [Fact]
    public async Task Website_form_posts_land_as_inquiries_and_trigger_outbox_mail()
    {
        var agent = factory.ClientAs(TestIdentity.Agent);
        var admin = factory.ClientAs(TestIdentity.Admin);
        var site = factory.CreateClient();
        site.DefaultRequestHeaders.Add("Origin", "https://www.bluehorizon.example");
        var stranger = factory.CreateClient();
        stranger.DefaultRequestHeaders.Add("Origin", "https://evil.example");

        var create = await agent.PostAsJsonAsync("/api/v1/agency/forms", new SaveInquiryFormRequest(
            "Website contact", ["https://www.bluehorizon.example"], ["office@bluehorizon.example"],
            true, "Thanks {{name}}", "Hello {{name}}, we received your inquiry about {{destination}}.", null, null, null), Json.Options);
        await create.ShouldBeAsync(HttpStatusCode.Created);
        var form = await ReadAsync<InquiryFormDto>(create);
        Assert.True(FormAccessKey.LooksValid(form.AccessKey));

        // JSON from fetch() on the agency's site.
        var json = await site.PostAsJsonAsync(form.SubmitPath, new
        {
            name = "Georgi Ivanov", email = "georgi@example.com", destination = "Rhodes", message = "Two adults, 7 nights", botcheck = "",
        });
        await json.ShouldBeAsync(HttpStatusCode.OK);
        Assert.Equal("*", json.Headers.GetValues("Access-Control-Allow-Origin").Single());
        var accepted = await ReadAsync<PublicSubmissionResponse>(json);
        Assert.True(accepted.Success);
        Assert.NotNull(accepted.SubmissionId);

        // Classic HTML form post with the key in the body and a redirect back to the site.
        var htmlForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["access_key"] = form.AccessKey, ["name"] = "Maria", ["email"] = "maria@example.com", ["redirect"] = "https://www.bluehorizon.example/thanks",
        });
        using var noRedirect = factory.CreateDefaultClient();
        noRedirect.DefaultRequestHeaders.Add("Origin", "https://www.bluehorizon.example");
        var posted = await noRedirect.PostAsync("/api/v1/public/forms", htmlForm);
        await posted.ShouldBeAsync(HttpStatusCode.Redirect);
        Assert.Equal("https://www.bluehorizon.example/thanks", posted.Headers.Location?.ToString());

        // Honeypot: accepted on the surface, stored nowhere.
        await (await site.PostAsJsonAsync(form.SubmitPath, new { name = "Bot", email = "bot@example.com", botcheck = "http://spam" })).ShouldBeAsync(HttpStatusCode.OK);
        // Wrong origin, unknown key, bad body.
        await (await stranger.PostAsJsonAsync(form.SubmitPath, new { name = "x" })).ShouldBeAsync(HttpStatusCode.Forbidden);
        await (await site.PostAsJsonAsync($"/api/v1/public/forms/{FormAccessKey.New()}", new { name = "x" })).ShouldBeAsync(HttpStatusCode.NotFound);
        await (await site.PostAsync(form.SubmitPath, new StringContent("[1,2]", Encoding.UTF8, "application/json"))).ShouldBeAsync(HttpStatusCode.UnprocessableEntity);

        // The agency sees two inquiries (not the bot), reads one.
        var list = await ReadAsync<PagedResult<InquirySummaryDto>>(await agent.GetAsync($"/api/v1/agency/inquiries?formId={form.Id}"));
        Assert.Equal(2, list.TotalCount);
        var detail = await ReadAsync<InquiryDto>(await agent.GetAsync($"/api/v1/agency/inquiries/{accepted.SubmissionId}"));
        Assert.Equal("Rhodes", detail.Fields.GetProperty("destination").GetString());
        Assert.Equal("georgi@example.com", detail.CustomerEmail);
        var read = await ReadAsync<InquiryDto>(await agent.PostAsJsonAsync($"/api/v1/agency/inquiries/{accepted.SubmissionId}/status",
            new SetInquiryStatusRequest(InquiryStatus.Read, "called"), Json.Options));
        Assert.Equal(InquiryStatus.Read, read.Status);
        Assert.NotNull(read.HandledAt);

        var updatedForm = await ReadAsync<InquiryFormDto>(await agent.GetAsync($"/api/v1/agency/forms/{form.Id}"));
        Assert.Equal((2, 1), (updatedForm.SubmissionCount, updatedForm.SpamCount));

        // Outbox: notification + auto-reply per real submission, dispatched through the logging transport.
        await (await admin.PostAsync("/api/v1/admin/emails/dispatch", null)).ShouldBeAsync(HttpStatusCode.OK);
        var mails = await ReadAsync<List<EmailMessageDto>>(await admin.GetAsync("/api/v1/admin/emails?category=inquiry-notification"));
        Assert.Contains(mails, m => m.CorrelationId == accepted.SubmissionId.ToString() && m.To == "office@bluehorizon.example");
        var replies = await ReadAsync<List<EmailMessageDto>>(await admin.GetAsync("/api/v1/admin/emails?category=inquiry-auto-reply"));
        Assert.Contains(replies, m => m.CorrelationId == accepted.SubmissionId.ToString() && m.To == "georgi@example.com" && m.Subject == "Thanks Georgi Ivanov");

        // Usage counts inquiries; operators cannot touch agency forms.
        var usage = await ReadAsync<List<WorkspaceUsageDto>>(await admin.GetAsync("/api/v1/admin/workspace/usage"));
        Assert.Contains(usage, u => u.InquirySubmissions >= 2);
        await (await admin.GetAsync("/api/v1/agency/forms")).ShouldBeAsync(HttpStatusCode.Forbidden);
        await (await agent.DeleteAsync($"/api/v1/agency/forms/{form.Id}")).ShouldBeAsync(HttpStatusCode.Conflict);
    }

}
