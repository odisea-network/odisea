namespace Odisea.Modules.Documents.Features.Contracts;

/// What a template can print: scalars by dotted name, plus collections rendered as repeating table rows.
public sealed class MergeData
{
    public Dictionary<string, string> Scalars { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, List<Dictionary<string, string>>> Collections { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// The collection a placeholder iterates ("Passengers" for "Passengers.FullName"), or null for a scalar.
    /// Scalars win, so "Passengers.Count" in a table row does not turn the row into a repeater.
    public string? CollectionOf(string name)
    {
        if (Scalars.ContainsKey(name))
            return null;
        var dot = name.IndexOf('.');
        if (dot < 0)
            return null;
        var prefix = name[..dot];
        return Collections.ContainsKey(prefix) ? prefix : null;
    }

    public static string? ResolveItem(string collection, IReadOnlyDictionary<string, string> item, string name) =>
        name.Length > collection.Length + 1
        && name.StartsWith(collection, StringComparison.OrdinalIgnoreCase)
        && name[collection.Length] == '.'
            ? item.GetValueOrDefault(name[(collection.Length + 1)..])
            : null;
}

public sealed record PlaceholderDefinition(string Name, string Group, string Description, bool Repeating = false);

/// Every placeholder the API fills by itself. Anything else found in a template is a custom
/// field the operator supplies when generating (free text such as special terms).
public static class PlaceholderCatalogue
{
    public const string Passengers = "Passengers";

    public static readonly IReadOnlyList<PlaceholderDefinition> All =
    [
        new("Today", "System", "Generation date, dd.MM.yyyy"),
        new("Year", "System", "Generation year"),

        new("Company.Name", "Company", "Operator legal name (company profile)"),
        new("Company.Eik", "Company", "Operator ЕИК"),
        new("Company.VatNumber", "Company", "Operator VAT number"),
        new("Company.Address", "Company", "Operator seat address"),
        new("Company.City", "Company", "Operator city"),
        new("Company.Mol", "Company", "Operator МОЛ (representative)"),
        new("Company.Iban", "Company", "Operator IBAN"),
        new("Company.BankName", "Company", "Operator bank"),

        new("Agency.Name", "Agency", "Agency trade name"),
        new("Agency.Country", "Agency", "Agency country, ISO 3166-1 alpha-2"),
        new("Agency.Status", "Agency", "Active or Suspended"),
        new("Agency.ContactEmail", "Agency", "Agency contact e-mail"),
        new("Agency.CreditLimit", "Agency", "Agency credit limit in EUR"),
        new("Agency.LegalName", "Agency", "Agency registered company name"),
        new("Agency.Eik", "Agency", "Agency ЕИК"),
        new("Agency.VatNumber", "Agency", "Agency VAT number"),
        new("Agency.Address", "Agency", "Agency billing address"),
        new("Agency.City", "Agency", "Agency billing city"),
        new("Agency.Mol", "Agency", "Agency МОЛ (representative)"),

        new("Booking.Ref", "Booking", "Reservation reference, e.g. ODI-2027-000007"),
        new("Booking.Status", "Booking", "Reservation status"),
        new("Booking.ProgramName", "Booking", "Program name"),
        new("Booking.HotelName", "Booking", "Hotel name"),
        new("Booking.CheckIn", "Booking", "Check-in date, dd.MM.yyyy"),
        new("Booking.CheckOut", "Booking", "Check-out date, dd.MM.yyyy"),
        new("Booking.Nights", "Booking", "Number of nights"),
        new("Booking.Adults", "Booking", "Adults"),
        new("Booking.Children", "Booking", "Children"),
        new("Booking.PaxCount", "Booking", "Total travellers"),
        new("Booking.SellAmount", "Booking", "Sell price, formatted 1 036,38"),
        new("Booking.CommissionAmount", "Booking", "Agency commission"),
        new("Booking.Currency", "Booking", "Currency code"),
        new("Booking.PaidAmount", "Booking", "Payments received so far"),
        new("Booking.BalanceDue", "Booking", "Outstanding balance"),
        new("Booking.PaymentDueAt", "Booking", "Payment deadline, dd.MM.yyyy HH:mm (Sofia time)"),
        new("Booking.CreatedAt", "Booking", "Reservation date, dd.MM.yyyy"),
        new("Booking.ConfirmedAt", "Booking", "Operator confirmation date, dd.MM.yyyy"),

        new("Lead.FullName", "Lead passenger", "Lead traveller full name"),
        new("Lead.Title", "Lead passenger", "Title (Mr, Mrs, ...)"),
        new("Lead.FirstName", "Lead passenger", "First name"),
        new("Lead.LastName", "Lead passenger", "Last name"),
        new("Lead.Gender", "Lead passenger", "Male or Female"),
        new("Lead.BirthDate", "Lead passenger", "Birth date, dd.MM.yyyy"),
        new("Lead.Nationality", "Lead passenger", "Nationality, ISO 3166-1 alpha-2"),
        new("Lead.DocumentType", "Lead passenger", "Passport or IdCard"),
        new("Lead.DocumentNumber", "Lead passenger", "Travel document number"),
        new("Lead.DocumentIssuingCountry", "Lead passenger", "Document issuing country"),
        new("Lead.DocumentExpiryDate", "Lead passenger", "Document expiry, dd.MM.yyyy"),
        new("Lead.Email", "Lead passenger", "E-mail"),
        new("Lead.Phone", "Lead passenger", "Phone"),

        new("Passengers.Count", "Passengers", "Number of travellers"),
        new("Passengers.Names", "Passengers", "All traveller names, comma-separated"),

        new("Passengers.#", "Passengers (table row)", "Row number, starting at 1", Repeating: true),
        new("Passengers.FullName", "Passengers (table row)", "Full name", Repeating: true),
        new("Passengers.Title", "Passengers (table row)", "Title", Repeating: true),
        new("Passengers.FirstName", "Passengers (table row)", "First name", Repeating: true),
        new("Passengers.LastName", "Passengers (table row)", "Last name", Repeating: true),
        new("Passengers.Gender", "Passengers (table row)", "Male or Female", Repeating: true),
        new("Passengers.BirthDate", "Passengers (table row)", "Birth date, dd.MM.yyyy", Repeating: true),
        new("Passengers.Type", "Passengers (table row)", "Adult, Child or Infant at check-in", Repeating: true),
        new("Passengers.Nationality", "Passengers (table row)", "Nationality", Repeating: true),
        new("Passengers.DocumentType", "Passengers (table row)", "Passport or IdCard", Repeating: true),
        new("Passengers.DocumentNumber", "Passengers (table row)", "Travel document number", Repeating: true),
        new("Passengers.DocumentIssuingCountry", "Passengers (table row)", "Issuing country", Repeating: true),
        new("Passengers.DocumentExpiryDate", "Passengers (table row)", "Document expiry, dd.MM.yyyy", Repeating: true),
        new("Passengers.Email", "Passengers (table row)", "E-mail", Repeating: true),
        new("Passengers.Phone", "Passengers (table row)", "Phone", Repeating: true),
    ];

    private static readonly HashSet<string> Names = new(All.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);

    public static bool IsKnown(string name) => Names.Contains(name);

    public static string[] CustomFields(IEnumerable<string> placeholders) =>
        placeholders.Where(p => !IsKnown(p)).ToArray();
}
