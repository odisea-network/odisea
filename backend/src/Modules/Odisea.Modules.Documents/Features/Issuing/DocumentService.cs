using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Booking.PublicApi;
using Odisea.Modules.Documents.Domain;
using Odisea.Modules.Documents.Features.Numbering;
using Odisea.Modules.Documents.Infrastructure;
using Odisea.SharedKernel;

namespace Odisea.Modules.Documents.Features.Issuing;

public class DocumentService(
    DocumentsDbContext db,
    NumberSeries numberSeries,
    IBookingLookup bookingLookup,
    IAgencyLookup agencyLookup,
    IClock clock,
    ILogger<DocumentService> logger)
{
    public async Task<FinancialDocumentDto> IssueInvoiceAsync(IssueInvoiceRequest request, CancellationToken ct)
    {
        var profile = await RequireCompanyProfileAsync(ct);

        var booking = await bookingLookup.FindAsync(request.BookingId, ct)
            ?? throw new DocumentValidationException("Резервацията не е намерена.");
        if (booking.Status != "Confirmed")
            throw new DocumentValidationException(
                $"Фактура се издава само за потвърдена резервация (текущ статус: {booking.Status}).");
        if (booking.SellAmount is not { } gross)
            throw new DocumentValidationException("Резервацията няма потвърдена цена.");

        var alreadyInvoiced = await db.FinancialDocuments.AnyAsync(d =>
            d.BookingId == booking.Id && d.Kind == DocumentKind.Invoice && d.Status == DocumentStatus.Issued, ct);
        if (alreadyInvoiced)
            throw new DocumentValidationException(
                $"За резервация {booking.Ref} вече е издадена фактура. Анулирайте я първо (чл. 116 ЗДДС).");

        var recipient = await RequireBillingAsync(booking.AgencyId, ct);

        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var taxEvent = DateOnly.FromDateTime((booking.ConfirmedAt ?? clock.UtcNow).UtcDateTime);
        if (today.DayNumber - taxEvent.DayNumber > 5)
            logger.LogWarning(
                "Invoice for booking {Ref} issued {Days} days after the tax event — чл.113, ал.4 ЗДДС allows 5",
                booking.Ref, today.DayNumber - taxEvent.DayNumber);

        var (taxBase, vat, rate, legalNote) =
            VatCalculator.FromGross(gross, request.VatTreatment, request.ExemptionGrounds);

        return await PersistNumberedAsync(number => new FinancialDocument
        {
            Number = number,
            Kind = DocumentKind.Invoice,
            IssueDate = today,
            TaxEventDate = taxEvent,
            SupplierName = profile.Name,
            SupplierEik = profile.Eik,
            SupplierVatNumber = profile.VatNumber,
            SupplierAddress = profile.Address,
            SupplierCity = profile.City,
            SupplierMol = profile.Mol,
            SupplierIban = profile.Iban,
            SupplierBankName = profile.BankName,
            AgencyId = booking.AgencyId,
            RecipientName = recipient.LegalName!,
            RecipientEik = recipient.Eik!,
            RecipientVatNumber = recipient.VatNumber,
            RecipientAddress = recipient.Address!,
            RecipientCity = recipient.City!,
            RecipientMol = recipient.Mol,
            BookingId = booking.Id,
            BookingRef = booking.Ref,
            VatTreatment = request.VatTreatment,
            VatRatePercent = rate,
            TaxBase = taxBase,
            VatAmount = vat,
            Total = gross,
            Currency = booking.Currency,
            Description = Describe(booking),
            LegalNote = legalNote,
            DueDate = today.AddDays(request.DueInDays),
        }, ct);
    }

    public async Task<FinancialDocumentDto> IssueCreditNoteAsync(IssueCreditNoteRequest request, CancellationToken ct)
    {
        var invoice = await db.FinancialDocuments.FirstOrDefaultAsync(
                d => d.Id == request.InvoiceId && d.Kind == DocumentKind.Invoice, ct)
            ?? throw new DocumentValidationException("Фактурата не е намерена.");
        if (invoice.Status == DocumentStatus.Annulled)
            throw new DocumentValidationException(
                "Към анулирана фактура не се издава известие (чл. 116 ЗДДС).");

        var existingNote = await db.FinancialDocuments.AnyAsync(d =>
            d.RelatedDocumentId == invoice.Id && d.Kind == DocumentKind.CreditNote
            && d.Status == DocumentStatus.Issued, ct);
        if (existingNote)
            throw new DocumentValidationException("Към тази фактура вече е издадено кредитно известие.");

        if (invoice.BookingId is { } bookingId)
        {
            var booking = await bookingLookup.FindAsync(bookingId, ct);
            if (booking?.Status != "Cancelled")
                throw new DocumentValidationException(
                    "Кредитно известие се издава при разваляне на доставката — резервацията не е отказана (чл. 115, ал. 1 ЗДДС).");
        }

        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        return await PersistNumberedAsync(number => new FinancialDocument
        {
            Number = number,
            Kind = DocumentKind.CreditNote,
            IssueDate = today,
            TaxEventDate = today,
            SupplierName = invoice.SupplierName,
            SupplierEik = invoice.SupplierEik,
            SupplierVatNumber = invoice.SupplierVatNumber,
            SupplierAddress = invoice.SupplierAddress,
            SupplierCity = invoice.SupplierCity,
            SupplierMol = invoice.SupplierMol,
            SupplierIban = invoice.SupplierIban,
            SupplierBankName = invoice.SupplierBankName,
            AgencyId = invoice.AgencyId,
            RecipientName = invoice.RecipientName,
            RecipientEik = invoice.RecipientEik,
            RecipientVatNumber = invoice.RecipientVatNumber,
            RecipientAddress = invoice.RecipientAddress,
            RecipientCity = invoice.RecipientCity,
            RecipientMol = invoice.RecipientMol,
            BookingId = invoice.BookingId,
            BookingRef = invoice.BookingRef,
            RelatedDocumentId = invoice.Id,
            RelatedDocumentNumber = invoice.Number,
            RelatedDocumentDate = invoice.IssueDate,
            VatTreatment = invoice.VatTreatment,
            VatRatePercent = invoice.VatRatePercent,
            TaxBase = -invoice.TaxBase,
            VatAmount = -invoice.VatAmount,
            Total = -invoice.Total,
            Currency = invoice.Currency,
            Description = $"Кредитно известие към фактура № {invoice.Number} / {invoice.IssueDate:dd.MM.yyyy}. Основание: {request.Reason}",
            LegalNote = invoice.LegalNote,
        }, ct);
    }

    public async Task<FinancialDocumentDto> AnnulAsync(Guid documentId, string reason, CancellationToken ct)
    {
        var document = await db.FinancialDocuments.FirstOrDefaultAsync(d => d.Id == documentId, ct)
            ?? throw new DocumentValidationException("Документът не е намерен.");
        document.Annul(reason);
        await db.SaveChangesAsync(ct);
        return document.ToDto();
    }

    private async Task<FinancialDocumentDto> PersistNumberedAsync(
        Func<string, FinancialDocument> create, CancellationToken ct)
    {
        // The counter increment and the insert share this transaction — on any
        // failure both roll back and the numbering stays gapless.
        if (db.Database.IsRelational())
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var document = create(await numberSeries.NextAsync(NumberSeries.TaxDocuments, ct));
            db.FinancialDocuments.Add(document);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return document.ToDto();
        }
        else
        {
            var document = create(await numberSeries.NextAsync(NumberSeries.TaxDocuments, ct));
            db.FinancialDocuments.Add(document);
            await db.SaveChangesAsync(ct);
            return document.ToDto();
        }
    }

    private async Task<CompanyProfile> RequireCompanyProfileAsync(CancellationToken ct) =>
        await db.CompanyProfiles.AsNoTracking().FirstOrDefaultAsync(ct)
        ?? throw new DocumentValidationException(
            "Фирменият профил на издателя не е попълнен (наименование, ЕИК, адрес, МОЛ).");

    private async Task<AgencyBillingInfo> RequireBillingAsync(Guid agencyId, CancellationToken ct)
    {
        var billing = await agencyLookup.GetBillingInfoAsync(agencyId, ct);
        if (billing is null
            || string.IsNullOrWhiteSpace(billing.LegalName)
            || string.IsNullOrWhiteSpace(billing.Eik)
            || string.IsNullOrWhiteSpace(billing.Address)
            || string.IsNullOrWhiteSpace(billing.City))
            throw new DocumentValidationException(
                "Агенцията няма попълнени данни за фактуриране (наименование, ЕИК, адрес) — чл. 114, ал. 1 ЗДДС.");
        return billing;
    }

    private static string Describe(BookingFinancialInfo booking) =>
        $"Туристическа услуга по резервация {booking.Ref}: {booking.ProgramName}, " +
        $"хотел {booking.HotelName}, {booking.CheckIn:dd.MM.yyyy} – {booking.CheckOut:dd.MM.yyyy}";
}
