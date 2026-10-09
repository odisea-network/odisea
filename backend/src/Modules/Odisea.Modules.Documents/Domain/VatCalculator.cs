namespace Odisea.Modules.Documents.Domain;

public static class VatCalculator
{
    /// чл.142, ал.1 ЗДДС — mandatory wording on margin-scheme invoices.
    public const string MarginSchemeNote =
        "Режим на облагане на маржа — туристически услуги (чл. 142, ал. 1 ЗДДС). " +
        "Данъкът не се посочва отделно във фактурата (чл. 86, ал. 1 ППЗДДС).";

    public static (decimal TaxBase, decimal Vat, decimal RatePercent, string? LegalNote) FromGross(
        decimal gross, VatTreatment treatment, string? exemptionGrounds = null)
    {
        switch (treatment)
        {
            case VatTreatment.MarginScheme:
                // The margin tax is settled via протокол, not on the invoice.
                return (gross, 0m, 0m, MarginSchemeNote);

            case VatTreatment.Standard20:
                var base20 = Math.Round(gross / 1.20m, 2, MidpointRounding.AwayFromZero);
                return (base20, gross - base20, 20m, null);

            case VatTreatment.Accommodation9:
                var base9 = Math.Round(gross / 1.09m, 2, MidpointRounding.AwayFromZero);
                return (base9, gross - base9, 9m, null);

            case VatTreatment.Exempt:
                if (string.IsNullOrWhiteSpace(exemptionGrounds))
                    throw new DocumentValidationException(
                        "Освободена доставка изисква основание за неначисляване на ДДС (чл. 114, ал. 1, т. 12 ЗДДС).");
                return (gross, 0m, 0m, $"Основание за неначисляване на ДДС: {exemptionGrounds}");

            default:
                throw new ArgumentOutOfRangeException(nameof(treatment));
        }
    }
}
