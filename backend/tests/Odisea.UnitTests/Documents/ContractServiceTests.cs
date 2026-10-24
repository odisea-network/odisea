using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Booking.PublicApi;
using Odisea.Modules.Documents.Domain;
using Odisea.Modules.Documents.Features.Contracts;
using Odisea.Modules.Documents.Infrastructure;
using Odisea.SharedKernel;
using Xunit;

namespace Odisea.UnitTests.Documents;

public class ContractServiceTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeBookingLookup : IBookingLookup
    {
        public List<BookingContractInfo> Bookings { get; } = [];
        public Task<BookingFinancialInfo?> FindAsync(Guid bookingId, CancellationToken ct) =>
            Task.FromResult<BookingFinancialInfo?>(null);
        public Task<BookingContractInfo?> GetContractInfoAsync(Guid bookingId, CancellationToken ct) =>
            Task.FromResult(Bookings.FirstOrDefault(b => b.Id == bookingId));
    }

    private sealed class FakeAgencyLookup : IAgencyLookup
    {
        public List<AgencyContractInfo> Agencies { get; } = [];
        public Task<AgencySnapshot?> FindAsync(Guid agencyId, CancellationToken ct) =>
            Task.FromResult<AgencySnapshot?>(null);
        public Task<AgencyBillingInfo?> GetBillingInfoAsync(Guid agencyId, CancellationToken ct) =>
            Task.FromResult<AgencyBillingInfo?>(null);
        public Task<AgencyContractInfo?> GetContractInfoAsync(Guid agencyId, CancellationToken ct) =>
            Task.FromResult(Agencies.FirstOrDefault(a => a.Id == agencyId));
    }

    private readonly DocumentsDbContext _db;
    private readonly FakeBookingLookup _bookings = new();
    private readonly FakeAgencyLookup _agencies = new();
    private readonly ContractService _service;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _agencyId = Guid.NewGuid();
    private readonly Guid _otherAgencyId = Guid.NewGuid();
    private readonly Guid _bookingId = Guid.NewGuid();

    public ContractServiceTests()
    {
        _db = new DocumentsDbContext(new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var clock = new FakeClock();
        _service = new ContractService(
            _db, new DocxTemplateRenderer(), new ContractMergeDataBuilder(clock),
            _bookings, _agencies, clock, NullLogger<ContractService>.Instance);

        _db.CompanyProfiles.Add(new CompanyProfile
        {
            Name = "Одисея Травел ЕООД", Eik = "209876543", Address = "бул. Витоша 100", City = "София", Mol = "Директор",
        });
        _db.SaveChanges();

        _agencies.Agencies.Add(new AgencyContractInfo(
            _agencyId, "Blue Horizon Travel", "BG", "Active", "office@bluehorizon.example", 50_000m,
            "Блу Хоризон ЕООД", "201234567", "BG201234567", "ул. Тестова 1", "София", "Управител"));
        _agencies.Agencies.Add(new AgencyContractInfo(
            _otherAgencyId, "Other Agency", "BG", "Active", null, 0m, null, null, null, null, null, null));
        _bookings.Bookings.Add(new BookingContractInfo(
            _bookingId, "ODI-2027-000007", _agencyId, "AwaitingPayment", "Antalya Easter 2027", "Mock Palace Resort",
            new DateOnly(2027, 3, 30), new DateOnly(2027, 4, 6), 2, 0, 831.10m, 83.11m, "EUR", 0m, 831.10m,
            new DateTimeOffset(2026, 10, 27, 14, 32, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 24, 14, 0, 0, TimeSpan.Zero), null,
            [
                new BookingPassengerInfo(1, null, "Male", "Ivan", "Petrov", new DateOnly(1985, 5, 1), "Adult", "BG",
                    "Passport", "AB1234567", "BG", new DateOnly(2031, 1, 1), true, "ivan@example.com", "+359888123456"),
                new BookingPassengerInfo(2, null, "Female", "Maria", "Petrova", new DateOnly(1988, 7, 12), "Adult", "BG",
                    "IdCard", "6401234567", "BG", new DateOnly(2030, 6, 1), false, null, null),
            ]));
    }

    private static byte[] CustomerTemplate() => TestDocx.Build(body =>
    {
        body.Append(TestDocx.Paragraph("ДОГОВОР {{Booking.Ref}} между {{Company.Name}} и {{Agency.LegalName}}"));
        body.Append(TestDocx.Paragraph("Хотел {{Booking.HotelName}}, {{Booking.CheckIn}} – {{Booking.CheckOut}}, {{Booking.Nights}} нощувки"));
        body.Append(TestDocx.Paragraph("Цена: {{Booking.SellAmount}} {{Booking.Currency}}. Водач: {{Lead.FullName}}, {{Lead.Phone}}"));
        body.Append(new Table(TestDocx.Row("№", "Име", "Документ"),
            TestDocx.Row("{{Passengers.#}}", "{{Passengers.FullName}}", "{{Passengers.DocumentType}} {{Passengers.DocumentNumber}}")));
        body.Append(TestDocx.Paragraph("Специални условия: {{SpecialTerms}}"));
    });

    private Task<ContractTemplateDto> UploadAsync(byte[]? docx = null, bool agencyUsable = false) =>
        _service.UploadTemplateAsync("Customer contract", "CustomerContract", null, agencyUsable,
            docx ?? CustomerTemplate(), "contract.docx", _userId, CancellationToken.None);

    [Fact]
    public async Task Upload_discovers_placeholders_and_flags_the_custom_fields()
    {
        var dto = await UploadAsync();

        Assert.Equal(ContractKind.CustomerContract, dto.Kind);
        Assert.Equal(1, dto.Version);
        Assert.Contains("Booking.Ref", dto.Placeholders);
        Assert.Contains("Passengers.DocumentNumber", dto.Placeholders);
        Assert.Equal(["SpecialTerms"], dto.CustomFields);
        Assert.Equal("contract.docx", dto.FileName);
    }

    [Fact]
    public async Task Upload_rejects_files_that_are_not_docx_and_blank_names()
    {
        await Assert.ThrowsAsync<ContractValidationException>(() => UploadAsync("plain text"u8.ToArray()));
        await Assert.ThrowsAsync<ContractValidationException>(() => _service.UploadTemplateAsync(
            " ", null, null, false, CustomerTemplate(), null, _userId, CancellationToken.None));
        await Assert.ThrowsAsync<ContractValidationException>(() => _service.UploadTemplateAsync(
            "x", "Lease", null, false, CustomerTemplate(), null, _userId, CancellationToken.None));
        Assert.Empty(_db.ContractTemplates);
    }

    [Fact]
    public async Task Generate_fills_company_agency_booking_lead_and_one_row_per_passenger()
    {
        var template = await UploadAsync();

        var dto = await _service.GenerateAsync(new GenerateContractRequest(template.Id, _bookingId, null,
            new Dictionary<string, string> { ["SpecialTerms"] = "Без промени" }, "first draft"), _userId, CancellationToken.None);

        Assert.Equal("ODI-2027-000007", dto.BookingRef);
        Assert.Equal(_agencyId, dto.AgencyId);
        Assert.Equal(1, dto.TemplateVersion);
        Assert.False(dto.SharedWithAgency);
        Assert.False(dto.GeneratedByAgency);
        Assert.Equal("first draft", dto.Note);
        Assert.Equal("Customer-contract-ODI-2027-000007-20261025.docx", dto.FileName);

        var file = await _service.GetGeneratedFileAsync(dto.Id, CancellationToken.None);
        var text = TestDocx.BodyText(file.Content);
        Assert.Contains("ДОГОВОР ODI-2027-000007 между Одисея Травел ЕООД и Блу Хоризон ЕООД", text);
        Assert.Contains("30.03.2027 – 06.04.2027, 7 нощувки", text);
        Assert.Contains("831,10 EUR", text);
        Assert.Contains("Водач: Ivan Petrov, +359888123456", text);
        Assert.Contains("1Ivan PetrovPassport AB1234567", text);
        Assert.Contains("2Maria PetrovaIdCard 6401234567", text);
        Assert.Contains("Специални условия: Без промени", text);
        Assert.DoesNotContain("{{", text);
    }

    [Fact]
    public async Task Generate_requires_the_custom_fields_and_refuses_to_override_data_fields()
    {
        var template = await UploadAsync();

        var missing = await Assert.ThrowsAsync<ContractValidationException>(() => _service.GenerateAsync(
            new GenerateContractRequest(template.Id, _bookingId, null, null, null), _userId, CancellationToken.None));
        Assert.Contains("SpecialTerms", missing.Message);

        var overriding = await Assert.ThrowsAsync<ContractValidationException>(() => _service.GenerateAsync(
            new GenerateContractRequest(template.Id, _bookingId, null,
                new Dictionary<string, string> { ["SpecialTerms"] = "x", ["Booking.SellAmount"] = "1,00" }, null),
            _userId, CancellationToken.None));
        Assert.Contains("Booking.SellAmount", overriding.Message);
        Assert.Empty(_db.GeneratedContracts);
    }

    [Fact]
    public async Task Generate_rejects_agency_mismatch_unknown_subjects_and_inactive_templates()
    {
        var template = await UploadAsync();
        var fields = new Dictionary<string, string> { ["SpecialTerms"] = "x" };

        await Assert.ThrowsAsync<ContractValidationException>(() => _service.GenerateAsync(
            new GenerateContractRequest(template.Id, _bookingId, _otherAgencyId, fields, null), _userId, CancellationToken.None));
        await Assert.ThrowsAsync<ContractNotFoundException>(() => _service.GenerateAsync(
            new GenerateContractRequest(template.Id, Guid.NewGuid(), null, fields, null), _userId, CancellationToken.None));
        await Assert.ThrowsAsync<ContractNotFoundException>(() => _service.GenerateAsync(
            new GenerateContractRequest(Guid.NewGuid(), _bookingId, null, fields, null), _userId, CancellationToken.None));

        await _service.UpdateTemplateAsync(template.Id,
            new UpdateContractTemplateRequest(template.Name, template.Kind, null, IsActive: false, AgencyUsable: false), CancellationToken.None);
        await Assert.ThrowsAsync<ContractValidationException>(() => _service.GenerateAsync(
            new GenerateContractRequest(template.Id, _bookingId, null, fields, null), _userId, CancellationToken.None));
        Assert.Empty(await _service.ListTemplatesAsync(includeInactive: false, CancellationToken.None));
        Assert.Single(await _service.ListTemplatesAsync(includeInactive: true, CancellationToken.None));
    }

    [Fact]
    public async Task Agency_only_template_with_company_and_agency_data_needs_no_booking()
    {
        var template = await UploadAsync(TestDocx.Build(body =>
            body.Append(TestDocx.Paragraph("{{Company.Name}} ↔ {{Agency.Name}} ({{Agency.Eik}}), limit {{Agency.CreditLimit}} EUR"))));

        var dto = await _service.GenerateAsync(new GenerateContractRequest(template.Id, null, _agencyId, null, null),
            _userId, CancellationToken.None);

        Assert.Null(dto.BookingId);
        Assert.Equal("Customer-contract-Blue-Horizon-Travel-20261025.docx", dto.FileName);
        var text = TestDocx.BodyText((await _service.GetGeneratedFileAsync(dto.Id, CancellationToken.None)).Content);
        Assert.Equal("Одисея Травел ЕООД ↔ Blue Horizon Travel (201234567), limit 50 000,00 EUR", text);
    }

    [Fact]
    public async Task Sharing_controls_what_the_agency_sees_and_other_agencies_see_nothing()
    {
        var template = await UploadAsync();
        var dto = await _service.GenerateAsync(new GenerateContractRequest(template.Id, _bookingId, null,
            new Dictionary<string, string> { ["SpecialTerms"] = "x" }, null), _userId, CancellationToken.None);

        Assert.Empty(await _service.ListForAgencyAsync(_agencyId, CancellationToken.None));
        await Assert.ThrowsAsync<ContractNotFoundException>(() => _service.GetFileForAgencyAsync(dto.Id, _agencyId, CancellationToken.None));

        var shared = await _service.SetSharedAsync(dto.Id, shared: true, CancellationToken.None);
        Assert.True(shared.SharedWithAgency);
        Assert.NotNull(shared.SharedAt);
        Assert.Single(await _service.ListForAgencyAsync(_agencyId, CancellationToken.None));
        Assert.Empty(await _service.ListForAgencyAsync(_otherAgencyId, CancellationToken.None));
        await Assert.ThrowsAsync<ContractNotFoundException>(() => _service.GetForAgencyAsync(dto.Id, _otherAgencyId, CancellationToken.None));

        await _service.SetSharedAsync(dto.Id, shared: false, CancellationToken.None);
        Assert.Empty(await _service.ListForAgencyAsync(_agencyId, CancellationToken.None));
    }

    [Fact]
    public async Task Agencies_generate_only_from_agency_usable_templates_for_their_own_bookings()
    {
        var hidden = await UploadAsync(agencyUsable: false);
        var usable = await UploadAsync(agencyUsable: true);
        var fields = new Dictionary<string, string> { ["SpecialTerms"] = "x" };

        Assert.Equal([usable.Id], (await _service.ListTemplatesForAgencyAsync(CancellationToken.None)).Select(t => t.Id));
        await Assert.ThrowsAsync<ContractNotFoundException>(() => _service.GenerateForAgencyAsync(
            new AgencyGenerateContractRequest(hidden.Id, _bookingId, fields), _agencyId, _userId, CancellationToken.None));
        await Assert.ThrowsAsync<ContractNotFoundException>(() => _service.GenerateForAgencyAsync(
            new AgencyGenerateContractRequest(usable.Id, _bookingId, fields), _otherAgencyId, _userId, CancellationToken.None));

        var dto = await _service.GenerateForAgencyAsync(
            new AgencyGenerateContractRequest(usable.Id, _bookingId, fields), _agencyId, _userId, CancellationToken.None);

        Assert.True(dto.GeneratedByAgency);
        Assert.True(dto.SharedWithAgency);
        Assert.Equal(_agencyId, dto.AgencyId);
        Assert.Single(await _service.ListForAgencyAsync(_agencyId, CancellationToken.None));
        Assert.Single(await _service.ListGeneratedAsync(null, null, usable.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Agency_generation_is_capped_per_booking()
    {
        var usable = await UploadAsync(agencyUsable: true);
        var fields = new Dictionary<string, string> { ["SpecialTerms"] = "x" };
        for (var i = 0; i < ContractService.MaxAgencyContractsPerBooking; i++)
            await _service.GenerateForAgencyAsync(
                new AgencyGenerateContractRequest(usable.Id, _bookingId, fields), _agencyId, _userId, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<ContractValidationException>(() => _service.GenerateForAgencyAsync(
            new AgencyGenerateContractRequest(usable.Id, _bookingId, fields), _agencyId, _userId, CancellationToken.None));

        Assert.Contains("per booking", ex.Message);
    }

    [Fact]
    public async Task Used_templates_cannot_be_deleted_but_unused_ones_can()
    {
        var used = await UploadAsync();
        var unused = await UploadAsync();
        await _service.GenerateAsync(new GenerateContractRequest(used.Id, _bookingId, null,
            new Dictionary<string, string> { ["SpecialTerms"] = "x" }, null), _userId, CancellationToken.None);

        await Assert.ThrowsAsync<ContractTemplateInUseException>(() => _service.DeleteTemplateAsync(used.Id, CancellationToken.None));
        await _service.DeleteTemplateAsync(unused.Id, CancellationToken.None);

        Assert.Single(_db.ContractTemplates);
    }

    [Fact]
    public async Task Replacing_the_file_bumps_the_version_and_rediscovers_placeholders()
    {
        var template = await UploadAsync();

        var replaced = await _service.ReplaceTemplateFileAsync(template.Id,
            TestDocx.Build(body => body.Append(TestDocx.Paragraph("{{Booking.Ref}} only"))), "v2.docx", CancellationToken.None);

        Assert.Equal(2, replaced.Version);
        Assert.Equal(["Booking.Ref"], replaced.Placeholders);
        Assert.Empty(replaced.CustomFields);
        Assert.Equal("v2.docx", replaced.FileName);
        var generated = await _service.GenerateAsync(new GenerateContractRequest(template.Id, _bookingId, null, null, null),
            _userId, CancellationToken.None);
        Assert.Equal(2, generated.TemplateVersion);
    }
}
