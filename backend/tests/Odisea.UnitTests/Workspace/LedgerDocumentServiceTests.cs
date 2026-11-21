using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Workspace.Domain;
using Odisea.Modules.Workspace.Domain.Ledger;
using Odisea.Modules.Workspace.Features.Ledger;
using Odisea.Modules.Workspace.Infrastructure;
using Odisea.SharedKernel;
using Xunit;

namespace Odisea.UnitTests.Workspace;

public class LedgerDocumentServiceTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 12, 10, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeEntitlements : IAgencyEntitlements
    {
        public HashSet<Guid> WithLedger { get; } = [];

        public Task<AgencyEntitlements?> GetAsync(Guid agencyId, CancellationToken ct) =>
            Task.FromResult<AgencyEntitlements?>(null);

        public Task RequireAsync(Guid agencyId, AgencyFeature feature, CancellationToken ct) =>
            WithLedger.Contains(agencyId) && feature == AgencyFeature.ReservationLedger
                ? Task.CompletedTask
                : throw new FeatureNotInPlanException(feature, "Core");
    }

    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly byte[] PdfBytes = "%PDF-1.7\n1 0 obj\n<<>>\nendobj\n%%EOF"u8.ToArray();
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13];

    private readonly WorkspaceDbContext _db;
    private readonly FakeClock _clock = new();
    private readonly FakeEntitlements _entitlements = new();
    private readonly LedgerDocumentService _documents;
    private readonly Guid _agencyId = Guid.NewGuid();
    private readonly Guid _otherAgencyId = Guid.NewGuid();
    private readonly LedgerActor _agent = new(Guid.NewGuid(), WorkspaceActor.Agency, IsAdmin: false);
    private readonly LedgerActor _admin = new(Guid.NewGuid(), WorkspaceActor.Agency, IsAdmin: true);

    public LedgerDocumentServiceTests()
    {
        _db = new WorkspaceDbContext(new DbContextOptionsBuilder<WorkspaceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _documents = new LedgerDocumentService(_db, _entitlements, _clock);
        _entitlements.WithLedger.Add(_agencyId);
        _entitlements.WithLedger.Add(_otherAgencyId);
    }

    private async Task<LedgerReservation> ReservationAsync(Guid? agencyId = null)
    {
        var reservation = new LedgerReservation
        {
            AgencyId = agencyId ?? _agencyId,
            TourOperatorId = Guid.NewGuid(),
            Reference = $"TO-{Guid.NewGuid():N}"[..12],
            HolderName = "Georgi Ivanov",
            Currency = "EUR",
            OperatorTotal = 2000m,
            BookedOn = new DateOnly(2026, 11, 20),
            TravelStart = new DateOnly(2027, 2, 1),
            CreatedByUserId = _agent.Id,
        };
        _db.LedgerReservations.Add(reservation);
        await _db.SaveChangesAsync(Ct);
        return reservation;
    }

    private async Task<LedgerPayment> PaymentAsync(LedgerReservation reservation)
    {
        var payment = new LedgerPayment
        {
            ReservationId = reservation.Id,
            AgencyId = reservation.AgencyId,
            Amount = 400m,
            Currency = "EUR",
            PaidOn = new DateOnly(2026, 12, 5),
            RecordedByUserId = _agent.Id,
        };
        _db.LedgerPayments.Add(payment);
        await _db.SaveChangesAsync(Ct);
        return payment;
    }

    private Task<LedgerDocumentDto> UploadAsync(
        LedgerReservation reservation, byte[]? bytes = null, string? name = "contract.pdf", Guid? paymentId = null,
        Guid? agencyId = null) =>
        _documents.UploadAsync(reservation.Id, agencyId ?? _agencyId, _agent, bytes ?? PdfBytes, name,
            LedgerDocumentType.OperatorContract, paymentId, Ct);

    [Fact]
    public async Task An_uploaded_file_is_listed_downloaded_and_audited()
    {
        var reservation = await ReservationAsync();

        var uploaded = await UploadAsync(reservation);

        Assert.Equal("contract.pdf", uploaded.FileName);
        Assert.Equal(LedgerFile.Pdf, uploaded.ContentType);
        Assert.Equal(PdfBytes.Length, uploaded.SizeBytes);
        Assert.Equal(_agent.Id, uploaded.UploadedByUserId);
        Assert.Equal(uploaded, Assert.Single(await _documents.ListAsync(reservation.Id, _agencyId, Ct)));

        var file = await _documents.GetFileAsync(reservation.Id, uploaded.Id, _agencyId, Ct);
        Assert.Equal(PdfBytes, file.Content);
        Assert.Equal(LedgerFile.Pdf, file.ContentType);

        var audit = Assert.Single(_db.LedgerAuditEvents);
        Assert.Equal("DocumentUploaded", audit.Action);
        Assert.Equal(reservation.Id, audit.ReservationId);
        Assert.Contains("contract.pdf", audit.Changes);
    }

    [Fact]
    public async Task The_type_comes_from_the_bytes_not_from_the_name()
    {
        var reservation = await ReservationAsync();

        var png = await UploadAsync(reservation, PngBytes, "scan.pdf");
        Assert.Equal(LedgerFile.Png, png.ContentType);
        Assert.Equal("scan.png", png.FileName);

        var html = "<html><script>alert(1)</script></html>"u8.ToArray();
        var refused = await Assert.ThrowsAsync<LedgerValidationException>(() => UploadAsync(reservation, html, "invoice.pdf"));
        Assert.Equal("file_type_unsupported", refused.Code);
    }

    [Fact]
    public async Task Empty_and_oversized_files_are_refused()
    {
        var reservation = await ReservationAsync();

        Assert.Equal("file_required",
            (await Assert.ThrowsAsync<LedgerValidationException>(() => UploadAsync(reservation, []))).Code);

        var tooBig = new byte[LedgerDocument.MaxBytes + 1];
        PdfBytes.CopyTo(tooBig, 0);
        Assert.Equal("file_too_large",
            (await Assert.ThrowsAsync<LedgerValidationException>(() => UploadAsync(reservation, tooBig))).Code);
    }

    [Theory]
    [InlineData(@"C:\Users\me\Desktop\договор.pdf", "договор.pdf")]
    [InlineData("../../etc/passwd", "passwd.pdf")]
    [InlineData("  ", "document.pdf")]
    [InlineData(null, "document.pdf")]
    [InlineData(".htaccess", "htaccess.pdf")]
    [InlineData("a\r\nb\".PDF", "ab.pdf")]
    public void File_names_are_reduced_to_a_bare_name_with_the_detected_extension(string? original, string expected) =>
        Assert.Equal(expected, LedgerFile.SafeName(original, LedgerFile.Pdf));

    [Fact]
    public void A_very_long_name_is_cut_to_fit_the_column() =>
        Assert.Equal(200, LedgerFile.SafeName(new string('x', 500) + ".pdf", LedgerFile.Pdf).Length);

    [Fact]
    public async Task The_same_file_twice_on_one_reservation_is_a_conflict()
    {
        var reservation = await ReservationAsync();
        await UploadAsync(reservation);

        var duplicate = await Assert.ThrowsAsync<LedgerConflictException>(() => UploadAsync(reservation, name: "again.pdf"));
        Assert.Equal("document_duplicate", duplicate.Code);

        // The same bytes on another reservation are a different attachment.
        await UploadAsync(await ReservationAsync());
    }

    [Fact]
    public async Task A_payment_proof_must_point_at_a_payment_of_the_same_reservation()
    {
        var reservation = await ReservationAsync();
        var payment = await PaymentAsync(reservation);
        var elsewhere = await PaymentAsync(await ReservationAsync());

        Assert.Equal(payment.Id, (await UploadAsync(reservation, paymentId: payment.Id)).PaymentId);
        var refused = await Assert.ThrowsAsync<LedgerValidationException>(
            () => UploadAsync(reservation, PngBytes, paymentId: elsewhere.Id));
        Assert.Equal("payment_unknown", refused.Code);
    }

    [Fact]
    public async Task Another_agency_cannot_list_read_add_or_delete()
    {
        var reservation = await ReservationAsync();
        var document = await UploadAsync(reservation);
        var stranger = new LedgerActor(Guid.NewGuid(), WorkspaceActor.Agency, IsAdmin: true);

        await Assert.ThrowsAsync<LedgerNotFoundException>(() => _documents.ListAsync(reservation.Id, _otherAgencyId, Ct));
        await Assert.ThrowsAsync<LedgerNotFoundException>(
            () => _documents.GetFileAsync(reservation.Id, document.Id, _otherAgencyId, Ct));
        await Assert.ThrowsAsync<LedgerNotFoundException>(() => UploadAsync(reservation, PngBytes, agencyId: _otherAgencyId));
        await Assert.ThrowsAsync<LedgerNotFoundException>(
            () => _documents.DeleteAsync(reservation.Id, document.Id, _otherAgencyId, stranger, Ct));

        // Its own reservation id does not open someone else's document either.
        var own = await ReservationAsync(_otherAgencyId);
        await Assert.ThrowsAsync<LedgerNotFoundException>(
            () => _documents.GetFileAsync(own.Id, document.Id, _otherAgencyId, Ct));
        Assert.Single(_db.LedgerDocuments);
    }

    [Fact]
    public async Task Only_an_agency_admin_deletes_and_the_removal_stays_in_the_history()
    {
        var reservation = await ReservationAsync();
        var document = await UploadAsync(reservation);

        await Assert.ThrowsAsync<LedgerAdminRequiredException>(
            () => _documents.DeleteAsync(reservation.Id, document.Id, _agencyId, _agent, Ct));
        Assert.Single(_db.LedgerDocuments);

        _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
        await _documents.DeleteAsync(reservation.Id, document.Id, _agencyId, _admin, Ct);

        Assert.Empty(_db.LedgerDocuments);
        var deleted = _db.LedgerAuditEvents.OrderBy(e => e.CreatedAt).Last();
        Assert.Equal("DocumentDeleted", deleted.Action);
        Assert.Equal(_admin.Id, deleted.ActorId);
        Assert.Contains("contract.pdf", deleted.Changes);
    }

    [Fact]
    public async Task Without_the_ledger_plan_files_stay_readable_but_nothing_can_be_added_or_removed()
    {
        var reservation = await ReservationAsync();
        var document = await UploadAsync(reservation);
        _entitlements.WithLedger.Remove(_agencyId);

        Assert.Single(await _documents.ListAsync(reservation.Id, _agencyId, Ct));
        await _documents.GetFileAsync(reservation.Id, document.Id, _agencyId, Ct);
        await Assert.ThrowsAsync<FeatureNotInPlanException>(() => UploadAsync(reservation, PngBytes));
        await Assert.ThrowsAsync<FeatureNotInPlanException>(
            () => _documents.DeleteAsync(reservation.Id, document.Id, _agencyId, _admin, Ct));
    }

    [Fact]
    public async Task A_reservation_holds_a_limited_number_of_documents()
    {
        var reservation = await ReservationAsync();
        for (var i = 0; i < LedgerDocument.MaxPerReservation; i++)
            await UploadAsync(reservation, [.. PdfBytes, (byte)i]);

        var refused = await Assert.ThrowsAsync<LedgerValidationException>(() => UploadAsync(reservation, PngBytes));
        Assert.Equal("too_many_documents", refused.Code);
    }
}
