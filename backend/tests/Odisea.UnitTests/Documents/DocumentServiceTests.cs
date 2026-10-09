using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Booking.PublicApi;
using Odisea.Modules.Documents.Domain;
using Odisea.Modules.Documents.Features.Issuing;
using Odisea.Modules.Documents.Features.Numbering;
using Odisea.Modules.Documents.Features.Pdf;
using Odisea.Modules.Documents.Infrastructure;
using Odisea.SharedKernel;
using Xunit;

namespace Odisea.UnitTests.Documents;

public class DocumentServiceTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeBookingLookup : IBookingLookup
    {
        public BookingFinancialInfo? Info { get; set; }
        public Task<BookingFinancialInfo?> FindAsync(Guid bookingId, CancellationToken ct) =>
            Task.FromResult(Info?.Id == bookingId ? Info : null);
    }

    private sealed class FakeAgencyLookup : IAgencyLookup
    {
        public AgencyBillingInfo? Billing { get; set; }
        public Task<AgencySnapshot?> FindAsync(Guid agencyId, CancellationToken ct) =>
            Task.FromResult<AgencySnapshot?>(null);
        public Task<AgencyBillingInfo?> GetBillingInfoAsync(Guid agencyId, CancellationToken ct) =>
            Task.FromResult(Billing);
    }

    private readonly DocumentsDbContext _db;
    private readonly FakeClock _clock = new();
    private readonly FakeBookingLookup _bookings = new();
    private readonly FakeAgencyLookup _agencies = new();
    private readonly DocumentService _service;
    private readonly Guid _agencyId = Guid.NewGuid();
    private readonly Guid _bookingId = Guid.NewGuid();

    public DocumentServiceTests()
    {
        _db = new DocumentsDbContext(new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _service = new DocumentService(
            _db, new NumberSeries(_db), _bookings, _agencies, _clock,
            NullLogger<DocumentService>.Instance);

        _db.CompanyProfiles.Add(new CompanyProfile
        {
            Name = "Одисея Травел ЕООД",
            Eik = "209876543",
            VatNumber = "BG209876543",
            Address = "бул. Витоша 100",
            City = "София",
            Mol = "Директор",
            Iban = "BG80BNBG96611020345678",
        });
        _db.SaveChanges();

        _agencies.Billing = new AgencyBillingInfo(
            _agencyId, "Блу Хоризон ЕООД", "201234567", "BG201234567",
            "ул. Тестова 1", "София", "Управител");
        _bookings.Info = ConfirmedBooking();
    }

    private BookingFinancialInfo ConfirmedBooking(string status = "Confirmed") => new(
        _bookingId, "ODI-2027-000001", _agencyId, status,
        831.10m, "EUR", "Antalya Easter 2027", "Mock Palace Resort",
        new DateOnly(2027, 3, 30), new DateOnly(2027, 4, 6),
        _clock.UtcNow.AddDays(-1), status == "Cancelled" ? _clock.UtcNow : null);

    private Task<FinancialDocumentDto> IssueAsync() =>
        _service.IssueInvoiceAsync(new IssueInvoiceRequest(_bookingId), CancellationToken.None);

    [Fact]
    public async Task Invoice_carries_all_mandatory_requisites_with_margin_scheme_default()
    {
        var invoice = await IssueAsync();

        Assert.Equal("0000000001", invoice.Number); // 10-digit, starts at 1
        Assert.Equal("Invoice", invoice.Kind);
        Assert.Equal("Одисея Травел ЕООД", invoice.Supplier.Name);
        Assert.Equal("209876543", invoice.Supplier.Eik);
        Assert.Equal("Блу Хоризон ЕООД", invoice.Recipient.Name);
        Assert.Equal("201234567", invoice.Recipient.Eik);
        Assert.Equal(831.10m, invoice.Total);
        Assert.Equal(831.10m, invoice.TaxBase);
        Assert.Equal(0m, invoice.VatAmount); // margin scheme: no separate VAT
        Assert.Contains("маржа", invoice.LegalNote);
        Assert.Equal("ODI-2027-000001", invoice.BookingRef);
        Assert.Equal(new DateOnly(2026, 10, 8), invoice.TaxEventDate);
        Assert.Equal(new DateOnly(2026, 10, 9), invoice.IssueDate);
        Assert.Equal(new DateOnly(2026, 10, 23), invoice.DueDate);
    }

    [Fact]
    public async Task Numbering_is_sequential_and_gapless()
    {
        var first = await IssueAsync();
        await _service.AnnulAsync(first.Id, "грешка в данните", CancellationToken.None);
        var second = await IssueAsync();

        Assert.Equal("0000000001", first.Number);
        Assert.Equal("0000000002", second.Number);
    }

    [Fact]
    public async Task Second_invoice_for_the_same_booking_is_rejected_until_annulment()
    {
        await IssueAsync();
        var ex = await Assert.ThrowsAsync<DocumentValidationException>(IssueAsync);
        Assert.Contains("чл. 116", ex.Message);
    }

    [Fact]
    public async Task Unconfirmed_booking_cannot_be_invoiced()
    {
        _bookings.Info = ConfirmedBooking(status: "Draft");
        await Assert.ThrowsAsync<DocumentValidationException>(IssueAsync);
    }

    [Fact]
    public async Task Missing_recipient_requisites_block_issuing()
    {
        _agencies.Billing = new AgencyBillingInfo(_agencyId, null, null, null, null, null, null);
        var ex = await Assert.ThrowsAsync<DocumentValidationException>(IssueAsync);
        Assert.Contains("чл. 114", ex.Message);
    }

    [Fact]
    public async Task Missing_company_profile_blocks_issuing()
    {
        _db.CompanyProfiles.RemoveRange(_db.CompanyProfiles);
        _db.SaveChanges();
        await Assert.ThrowsAsync<DocumentValidationException>(IssueAsync);
    }

    [Fact]
    public async Task Credit_note_negates_the_invoice_and_references_it()
    {
        var invoice = await IssueAsync();
        _bookings.Info = ConfirmedBooking(status: "Cancelled");

        var note = await _service.IssueCreditNoteAsync(
            new IssueCreditNoteRequest(invoice.Id, "отказана резервация"), CancellationToken.None);

        Assert.Equal("CreditNote", note.Kind);
        Assert.Equal("0000000002", note.Number); // same gapless series
        Assert.Equal(-831.10m, note.Total);
        Assert.Equal(invoice.Number, note.RelatedDocumentNumber); // чл.115, ал.4
        Assert.Equal(invoice.IssueDate, note.RelatedDocumentDate);
        Assert.Contains("0000000001", note.Description);
    }

    [Fact]
    public async Task Credit_note_requires_a_cancelled_booking()
    {
        var invoice = await IssueAsync();
        var ex = await Assert.ThrowsAsync<DocumentValidationException>(
            () => _service.IssueCreditNoteAsync(
                new IssueCreditNoteRequest(invoice.Id, "x"), CancellationToken.None));
        Assert.Contains("чл. 115", ex.Message);
    }

    [Fact]
    public async Task Annulled_documents_are_kept_not_deleted()
    {
        var invoice = await IssueAsync();
        var annulled = await _service.AnnulAsync(invoice.Id, "грешна цена", CancellationToken.None);

        Assert.Equal("Annulled", annulled.Status);
        Assert.Equal(1, await _db.FinancialDocuments.CountAsync()); // still there
        await Assert.ThrowsAsync<DocumentValidationException>(
            () => _service.AnnulAsync(invoice.Id, "пак", CancellationToken.None));
    }

    [Fact]
    public async Task Standard_vat_invoice_computes_base_and_vat()
    {
        var invoice = await _service.IssueInvoiceAsync(
            new IssueInvoiceRequest(_bookingId, VatTreatment.Standard20), CancellationToken.None);

        Assert.Equal(692.58m, invoice.TaxBase);  // 831.10 / 1.20
        Assert.Equal(138.52m, invoice.VatAmount);
        Assert.Equal(831.10m, invoice.TaxBase + invoice.VatAmount);
        Assert.Null(invoice.LegalNote);
    }

    [Fact]
    public async Task Pdf_renders_a_valid_document_with_cyrillic_content()
    {
        var invoice = await IssueAsync();
        var entity = await _db.FinancialDocuments.SingleAsync(d => d.Id == invoice.Id);

        var bytes = new DocumentPdfRenderer().Render(entity);

        Assert.True(bytes.Length > 10_000, "PDF suspiciously small");
        Assert.Equal("%PDF"u8.ToArray(), bytes[..4]);
    }
}
