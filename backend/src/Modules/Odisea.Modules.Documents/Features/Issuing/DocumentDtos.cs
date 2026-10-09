using System.ComponentModel.DataAnnotations;
using Odisea.Modules.Documents.Domain;

namespace Odisea.Modules.Documents.Features.Issuing;

public record IssueInvoiceRequest(
    [Required] Guid BookingId,
    VatTreatment VatTreatment = VatTreatment.MarginScheme,
    [MaxLength(300)] string? ExemptionGrounds = null,
    [Range(0, 60)] int DueInDays = 14);

public record IssueCreditNoteRequest(
    [Required] Guid InvoiceId,
    [Required, MaxLength(300)] string Reason);

public record AnnulRequest([Required, MaxLength(300)] string Reason);

public record PartyDto(string Name, string Eik, string? VatNumber, string Address, string City, string? Mol);

public record FinancialDocumentDto(
    Guid Id,
    string Number,
    string Kind,
    string Status,
    DateOnly IssueDate,
    DateOnly TaxEventDate,
    PartyDto Supplier,
    PartyDto Recipient,
    string? BookingRef,
    string? RelatedDocumentNumber,
    DateOnly? RelatedDocumentDate,
    string VatTreatment,
    decimal VatRatePercent,
    decimal TaxBase,
    decimal VatAmount,
    decimal Total,
    string Currency,
    string Description,
    string? LegalNote,
    DateOnly? DueDate,
    string? AnnulmentReason);

public record SaveCompanyProfileRequest(
    [Required, MaxLength(200)] string Name,
    [Required, RegularExpression(@"^\d{9}(\d{4})?$")] string Eik,
    [RegularExpression(@"^BG\d{9,10}$")] string? VatNumber,
    [Required, MaxLength(300)] string Address,
    [Required, MaxLength(100)] string City,
    [Required, MaxLength(200)] string Mol,
    [RegularExpression(@"^[A-Z]{2}\d{2}[A-Z0-9]{10,30}$")] string? Iban,
    [MaxLength(100)] string? BankName);

public record CompanyProfileDto(
    string Name, string Eik, string? VatNumber, string Address, string City,
    string Mol, string? Iban, string? BankName);

public static class DocumentDtoMapping
{
    public static FinancialDocumentDto ToDto(this FinancialDocument d) => new(
        d.Id, d.Number, d.Kind.ToString(), d.Status.ToString(),
        d.IssueDate, d.TaxEventDate,
        new PartyDto(d.SupplierName, d.SupplierEik, d.SupplierVatNumber, d.SupplierAddress, d.SupplierCity, d.SupplierMol),
        new PartyDto(d.RecipientName, d.RecipientEik, d.RecipientVatNumber, d.RecipientAddress, d.RecipientCity, d.RecipientMol),
        d.BookingRef, d.RelatedDocumentNumber, d.RelatedDocumentDate,
        d.VatTreatment.ToString(), d.VatRatePercent, d.TaxBase, d.VatAmount, d.Total, d.Currency,
        d.Description, d.LegalNote, d.DueDate, d.AnnulmentReason);

    public static CompanyProfileDto ToDto(this CompanyProfile p) =>
        new(p.Name, p.Eik, p.VatNumber, p.Address, p.City, p.Mol, p.Iban, p.BankName);
}
