using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Odisea.Modules.Documents.Features.Contracts;

namespace Odisea.IntegrationTests;

[Collection("api")]
public class ContractTests(OdiseaApiFactory factory)
{
    private static byte[] Template()
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new Document(new Body(
                Paragraph("Travel contract {{Booking.Ref}} for {{Agency.Name}}"),
                new Table(
                    new TableRow(Cell("#"), Cell("Traveller")),
                    new TableRow(Cell("{{Passengers.#}}"), Cell("{{Passengers.FullName}}"))),
                Paragraph("Special terms: {{SpecialTerms}}")));
        }
        return stream.ToArray();
    }

    private static Paragraph Paragraph(string text) =>
        new(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));

    private static TableCell Cell(string text) => new(Paragraph(text));

    private static string BodyText(byte[] docx)
    {
        using var stream = new MemoryStream(docx, writable: false);
        using var document = WordprocessingDocument.Open(stream, false);
        return document.MainDocumentPart!.Document!.Body!.InnerText;
    }

    private static MultipartFormDataContent TemplateForm(string name, bool agencyUsable)
    {
        var file = new ByteArrayContent(Template());
        file.Headers.ContentType = new MediaTypeHeaderValue(ContractDtoMapping.DocxContentType);
        return new MultipartFormDataContent
        {
            { file, "file", "contract.docx" },
            { new StringContent(name), "name" },
            { new StringContent("CustomerContract"), "kind" },
            { new StringContent(agencyUsable ? "true" : "false"), "agencyUsable" },
        };
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var dto = await response.Content.ReadFromJsonAsync<T>(Json.Options);
        Assert.NotNull(dto);
        return dto;
    }

    [Fact]
    public async Task Templates_are_changed_by_operator_admins_only_and_hidden_from_agencies()
    {
        var operatorUser = factory.ClientAs(TestIdentity.Operator);
        var agent = factory.ClientAs(TestIdentity.Agent);

        await (await operatorUser.PostAsync("/api/v1/contract-templates", TemplateForm("Forbidden", false)))
            .ShouldBeAsync(HttpStatusCode.Forbidden);
        await (await operatorUser.GetAsync("/api/v1/contract-templates/placeholders")).ShouldBeAsync(HttpStatusCode.OK);
        await (await agent.GetAsync("/api/v1/contract-templates")).ShouldBeAsync(HttpStatusCode.Forbidden);
        await (await agent.GetAsync("/api/v1/contracts")).ShouldBeAsync(HttpStatusCode.Forbidden);
        await (await agent.GetAsync("/api/v1/agency/contract-templates/placeholders")).ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Operator_generates_a_contract_for_a_booking_and_shares_it_with_the_agency()
    {
        var admin = factory.ClientAs(TestIdentity.Admin);
        var operatorUser = factory.ClientAs(TestIdentity.Operator);
        var agent = factory.ClientAs(TestIdentity.Agent);

        var upload = await admin.PostAsync("/api/v1/contract-templates", TemplateForm("Operator contract", agencyUsable: false));
        await upload.ShouldBeAsync(HttpStatusCode.Created);
        var template = await ReadAsync<ContractTemplateDto>(upload);
        Assert.Equal(["SpecialTerms"], template.CustomFields);
        Assert.Contains("Passengers.FullName", template.Placeholders);

        var booking = await AgencyFlow.SubmittedBookingAsync(agent);

        var missing = await operatorUser.PostAsJsonAsync("/api/v1/contracts",
            new GenerateContractRequest(template.Id, booking.Id, null, null, null), Json.Options);
        await missing.ShouldBeAsync(HttpStatusCode.UnprocessableEntity);
        Assert.Contains("SpecialTerms", await missing.Content.ReadAsStringAsync());

        var generate = await operatorUser.PostAsJsonAsync("/api/v1/contracts",
            new GenerateContractRequest(template.Id, booking.Id, null,
                new Dictionary<string, string> { ["SpecialTerms"] = "None" }, "sent by e-mail"), Json.Options);
        await generate.ShouldBeAsync(HttpStatusCode.Created);
        var contract = await ReadAsync<GeneratedContractDto>(generate);
        Assert.Equal(booking.Ref, contract.BookingRef);
        Assert.False(contract.SharedWithAgency);
        Assert.False(contract.GeneratedByAgency);

        var download = await operatorUser.GetAsync($"/api/v1/contracts/{contract.Id}/file");
        await download.ShouldBeAsync(HttpStatusCode.OK);
        Assert.Equal(ContractDtoMapping.DocxContentType, download.Content.Headers.ContentType?.MediaType);
        var text = BodyText(await download.Content.ReadAsByteArrayAsync());
        Assert.Contains($"Travel contract {booking.Ref} for", text);
        Assert.Contains("1Ivan Petrov", text);
        Assert.Contains("2Maria Petrova", text);
        Assert.Contains("Special terms: None", text);

        // Not shared yet: the agency sees nothing.
        var before = await ReadAsync<List<GeneratedContractDto>>(await agent.GetAsync("/api/v1/agency/contracts"));
        Assert.DoesNotContain(before, c => c.Id == contract.Id);
        await (await agent.GetAsync($"/api/v1/agency/contracts/{contract.Id}/file")).ShouldBeAsync(HttpStatusCode.NotFound);

        var share = await operatorUser.PostAsync($"/api/v1/contracts/{contract.Id}/share", null);
        await share.ShouldBeAsync(HttpStatusCode.OK);
        Assert.True((await ReadAsync<GeneratedContractDto>(share)).SharedWithAgency);

        var after = await ReadAsync<List<GeneratedContractDto>>(await agent.GetAsync("/api/v1/agency/contracts"));
        Assert.Contains(after, c => c.Id == contract.Id);
        await (await agent.GetAsync($"/api/v1/agency/contracts/{contract.Id}/file")).ShouldBeAsync(HttpStatusCode.OK);

        // The template is now part of a legal record and cannot be deleted.
        await (await admin.DeleteAsync($"/api/v1/contract-templates/{template.Id}")).ShouldBeAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Agency_generates_its_own_contract_from_an_agency_usable_template()
    {
        var admin = factory.ClientAs(TestIdentity.Admin);
        var agent = factory.ClientAs(TestIdentity.Agent);

        var hidden = await ReadAsync<ContractTemplateDto>(
            await admin.PostAsync("/api/v1/contract-templates", TemplateForm("Internal", agencyUsable: false)));
        var usable = await ReadAsync<ContractTemplateDto>(
            await admin.PostAsync("/api/v1/contract-templates", TemplateForm("Customer contract", agencyUsable: true)));

        var visible = await ReadAsync<List<ContractTemplateDto>>(await agent.GetAsync("/api/v1/agency/contract-templates"));
        Assert.Contains(visible, t => t.Id == usable.Id);
        Assert.DoesNotContain(visible, t => t.Id == hidden.Id);

        var booking = await AgencyFlow.SubmittedBookingAsync(agent);
        var fields = new Dictionary<string, string> { ["SpecialTerms"] = "Agency copy" };

        await (await agent.PostAsJsonAsync("/api/v1/agency/contracts",
            new AgencyGenerateContractRequest(hidden.Id, booking.Id, fields), Json.Options)).ShouldBeAsync(HttpStatusCode.NotFound);
        await (await agent.PostAsJsonAsync("/api/v1/agency/contracts",
            new AgencyGenerateContractRequest(usable.Id, Guid.NewGuid(), fields), Json.Options)).ShouldBeAsync(HttpStatusCode.NotFound);

        var generate = await agent.PostAsJsonAsync("/api/v1/agency/contracts",
            new AgencyGenerateContractRequest(usable.Id, booking.Id, fields), Json.Options);
        await generate.ShouldBeAsync(HttpStatusCode.Created);
        var contract = await ReadAsync<GeneratedContractDto>(generate);
        Assert.True(contract.GeneratedByAgency);
        Assert.True(contract.SharedWithAgency);
        Assert.Equal(booking.Ref, contract.BookingRef);

        var download = await agent.GetAsync($"/api/v1/agency/contracts/{contract.Id}/file");
        await download.ShouldBeAsync(HttpStatusCode.OK);
        Assert.Contains("Special terms: Agency copy", BodyText(await download.Content.ReadAsByteArrayAsync()));

        // The operator sees agency-generated contracts too.
        var all = await ReadAsync<List<GeneratedContractDto>>(
            await admin.GetAsync($"/api/v1/contracts?bookingId={booking.Id}"));
        Assert.Contains(all, c => c.Id == contract.Id && c.GeneratedByAgency);
    }
}
