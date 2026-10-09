using Odisea.SharedKernel;

namespace Odisea.Modules.Documents.Domain;

/// A Bulgarian tax document (данъчен документ по чл.112 ЗДДС). All party and
/// amount fields are SNAPSHOTS taken at issue time — later edits to agencies,
/// bookings or the company profile never change an issued document.
public class FinancialDocument : Entity
{
    /// 10-digit, gapless, ascending (чл.114, ал.1, т.2 ЗДДС; чл.78 ППЗДДС).
    public required string Number { get; set; }

    public DocumentKind Kind { get; set; }
    public DocumentStatus Status { get; private set; } = DocumentStatus.Issued;

    public DateOnly IssueDate { get; set; }

    /// Дата на данъчното събитие (чл.114, ал.1, т.9). The invoice must be
    /// issued within 5 days of it (чл.113, ал.4) — late issue is logged, not blocked.
    public DateOnly TaxEventDate { get; set; }

    // Supplier snapshot (чл.114, ал.1, т.3–5)
    public required string SupplierName { get; set; }
    public required string SupplierEik { get; set; }
    public string? SupplierVatNumber { get; set; }
    public required string SupplierAddress { get; set; }
    public required string SupplierCity { get; set; }
    public required string SupplierMol { get; set; }
    public string? SupplierIban { get; set; }
    public string? SupplierBankName { get; set; }

    // Recipient snapshot (чл.114, ал.1, т.6–8)
    public required Guid AgencyId { get; set; }
    public required string RecipientName { get; set; }
    public required string RecipientEik { get; set; }
    public string? RecipientVatNumber { get; set; }
    public required string RecipientAddress { get; set; }
    public required string RecipientCity { get; set; }
    public string? RecipientMol { get; set; }

    // Business references
    public Guid? BookingId { get; set; }
    public string? BookingRef { get; set; }

    /// For credit/debit notes: the referenced invoice (чл.115, ал.4 ЗДДС).
    public Guid? RelatedDocumentId { get; set; }
    public string? RelatedDocumentNumber { get; set; }
    public DateOnly? RelatedDocumentDate { get; set; }

    // Amounts (чл.114, ал.1, т.11–15). Negative for credit notes.
    public VatTreatment VatTreatment { get; set; }
    public decimal VatRatePercent { get; set; }
    public decimal TaxBase { get; set; }
    public decimal VatAmount { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = "EUR";

    /// Описание на услугата (чл.114, ал.1, т.9).
    public required string Description { get; set; }

    /// Margin-scheme wording (чл.142, ал.1) or exemption grounds (чл.114, ал.1, т.12).
    public string? LegalNote { get; set; }

    public DateOnly? DueDate { get; set; }

    public string? AnnulmentReason { get; set; }

    /// чл.116 ЗДДС: wrong documents are annulled and KEPT, never deleted.
    public void Annul(string reason)
    {
        if (Status == DocumentStatus.Annulled)
            throw new DocumentValidationException("Документът вече е анулиран.");
        Status = DocumentStatus.Annulled;
        AnnulmentReason = reason;
    }
}

public enum DocumentKind
{
    Invoice,      // фактура
    CreditNote,   // кредитно известие
    DebitNote,    // дебитно известие
    Proforma,     // проформа (не е данъчен документ)
}

public enum DocumentStatus
{
    Issued,
    Annulled,
}

public enum VatTreatment
{
    /// чл.136–142 ЗДДС — обща туристическа услуга: данък върху маржа,
    /// НЕ се посочва отделно във фактурата.
    MarginScheme,

    /// Стандартна ставка 20% (чл.66 ЗДДС).
    Standard20,

    /// Настаняване в хотели — 9% (чл.66а ЗДДС).
    Accommodation9,

    /// Освободена/необлагаема доставка — изисква основание в документа.
    Exempt,
}

public sealed class DocumentValidationException(string message) : DomainException(message);
