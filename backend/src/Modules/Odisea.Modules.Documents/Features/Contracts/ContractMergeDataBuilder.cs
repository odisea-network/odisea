using System.Globalization;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Booking.PublicApi;
using Odisea.Modules.Documents.Domain;
using Odisea.SharedKernel;

namespace Odisea.Modules.Documents.Features.Contracts;

/// Turns company, agency and booking facts into the strings a template prints. Every value is
/// pre-formatted here (dates dd.MM.yyyy, amounts 1 036,38) so templates stay free of logic.
public sealed class ContractMergeDataBuilder(IClock clock)
{
    // Contracts are Bulgarian legal documents: Bulgarian number formatting, Sofia wall-clock time.
    private static readonly CultureInfo Numbers = CultureInfo.GetCultureInfo("bg-BG");
    internal static readonly TimeZoneInfo LocalZone = ResolveZone("Europe/Sofia", "FLE Standard Time");

    public MergeData Build(
        CompanyProfile? company,
        AgencyContractInfo? agency,
        BookingContractInfo? booking,
        IReadOnlyDictionary<string, string>? fields)
    {
        var data = new MergeData();
        var s = data.Scalars;

        var today = TimeZoneInfo.ConvertTime(clock.UtcNow, LocalZone);
        s["Today"] = today.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        s["Year"] = today.Year.ToString(CultureInfo.InvariantCulture);

        if (company is not null)
        {
            s["Company.Name"] = company.Name;
            s["Company.Eik"] = company.Eik;
            s["Company.VatNumber"] = company.VatNumber ?? "";
            s["Company.Address"] = company.Address;
            s["Company.City"] = company.City;
            s["Company.Mol"] = company.Mol;
            s["Company.Iban"] = company.Iban ?? "";
            s["Company.BankName"] = company.BankName ?? "";
        }

        if (agency is not null)
        {
            s["Agency.Name"] = agency.Name;
            s["Agency.Country"] = agency.Country;
            s["Agency.Status"] = agency.Status;
            s["Agency.ContactEmail"] = agency.ContactEmail ?? "";
            s["Agency.CreditLimit"] = Money(agency.CreditLimit);
            s["Agency.LegalName"] = agency.LegalName ?? "";
            s["Agency.Eik"] = agency.Eik ?? "";
            s["Agency.VatNumber"] = agency.VatNumber ?? "";
            s["Agency.Address"] = agency.Address ?? "";
            s["Agency.City"] = agency.City ?? "";
            s["Agency.Mol"] = agency.Mol ?? "";
        }

        if (booking is not null)
        {
            s["Booking.Ref"] = booking.Ref;
            s["Booking.Status"] = booking.Status;
            s["Booking.ProgramName"] = booking.ProgramName;
            s["Booking.HotelName"] = booking.HotelName;
            s["Booking.CheckIn"] = Date(booking.CheckIn);
            s["Booking.CheckOut"] = Date(booking.CheckOut);
            s["Booking.Nights"] = Int(booking.CheckOut.DayNumber - booking.CheckIn.DayNumber);
            s["Booking.Adults"] = Int(booking.Adults);
            s["Booking.Children"] = Int(booking.Children);
            s["Booking.PaxCount"] = Int(booking.Adults + booking.Children);
            s["Booking.SellAmount"] = Money(booking.SellAmount);
            s["Booking.CommissionAmount"] = Money(booking.CommissionAmount);
            s["Booking.Currency"] = booking.Currency;
            s["Booking.PaidAmount"] = Money(booking.PaidAmount);
            s["Booking.BalanceDue"] = Money(booking.BalanceDue);
            s["Booking.PaymentDueAt"] = DateTime(booking.PaymentDueAt);
            s["Booking.CreatedAt"] = Date(booking.CreatedAt);
            s["Booking.ConfirmedAt"] = Date(booking.ConfirmedAt);

            var lead = booking.Passengers.FirstOrDefault(p => p.IsLead) ?? booking.Passengers.FirstOrDefault();
            if (lead is not null)
            {
                s["Lead.FullName"] = FullName(lead);
                s["Lead.Title"] = lead.Title ?? "";
                s["Lead.FirstName"] = lead.FirstName;
                s["Lead.LastName"] = lead.LastName;
                s["Lead.Gender"] = lead.Gender ?? "";
                s["Lead.BirthDate"] = Date(lead.BirthDate);
                s["Lead.Nationality"] = lead.Nationality ?? "";
                s["Lead.DocumentType"] = lead.DocumentType ?? "";
                s["Lead.DocumentNumber"] = lead.DocumentNumber ?? "";
                s["Lead.DocumentIssuingCountry"] = lead.DocumentIssuingCountry ?? "";
                s["Lead.DocumentExpiryDate"] = Date(lead.DocumentExpiryDate);
                s["Lead.Email"] = lead.Email ?? "";
                s["Lead.Phone"] = lead.Phone ?? "";
            }

            var rows = booking.Passengers.Select((p, i) => PassengerRow(p, i + 1)).ToList();
            data.Collections[PlaceholderCatalogue.Passengers] = rows;
            s["Passengers.Count"] = Int(rows.Count);
            s["Passengers.Names"] = string.Join(", ", booking.Passengers.Select(FullName));
        }

        if (fields is not null)
            foreach (var (name, value) in fields)
                s[name] = value;

        return data;
    }

    private static Dictionary<string, string> PassengerRow(BookingPassengerInfo p, int index) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["#"] = Int(index),
            ["FullName"] = FullName(p),
            ["Title"] = p.Title ?? "",
            ["FirstName"] = p.FirstName,
            ["LastName"] = p.LastName,
            ["Gender"] = p.Gender ?? "",
            ["BirthDate"] = Date(p.BirthDate),
            ["Type"] = p.PassengerType ?? "",
            ["Nationality"] = p.Nationality ?? "",
            ["DocumentType"] = p.DocumentType ?? "",
            ["DocumentNumber"] = p.DocumentNumber ?? "",
            ["DocumentIssuingCountry"] = p.DocumentIssuingCountry ?? "",
            ["DocumentExpiryDate"] = Date(p.DocumentExpiryDate),
            ["Email"] = p.Email ?? "",
            ["Phone"] = p.Phone ?? "",
        };

    private static string FullName(BookingPassengerInfo p) => $"{p.FirstName} {p.LastName}";

    private static string Date(DateOnly? date) =>
        date?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) ?? "";

    private static string Date(DateTimeOffset? at) =>
        at is { } value ? TimeZoneInfo.ConvertTime(value, LocalZone).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) : "";

    private static string DateTime(DateTimeOffset? at) =>
        at is { } value ? TimeZoneInfo.ConvertTime(value, LocalZone).ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture) : "";

    private static string Money(decimal? amount) => amount?.ToString("N2", Numbers) ?? "";

    private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static TimeZoneInfo ResolveZone(string ianaId, string windowsId)
    {
        foreach (var id in new[] { ianaId, windowsId })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }
        return TimeZoneInfo.Utc;
    }
}
