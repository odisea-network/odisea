using Odisea.SharedKernel;

namespace Odisea.Modules.Booking.Domain;

/// One traveller on a booking. Everything a hotel, airline or transport operator may
/// ask for is optional while the booking is being assembled; submit enforces completeness.
public class BookingPassenger : Entity
{
    public required Guid BookingId { get; set; }

    /// Zero-based order as submitted by the client; responses and error messages keep it.
    public required int Position { get; set; }

    public string? Title { get; set; }
    public Gender? Gender { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public DateOnly? BirthDate { get; set; }

    /// ISO 3166-1 alpha-2, upper case.
    public string? Nationality { get; set; }

    public TravelDocumentType? DocumentType { get; set; }

    /// Encrypted at rest (DataProtection value converter in BookingDbContext). Never log it.
    public string? DocumentNumber { get; set; }

    /// ISO 3166-1 alpha-2, upper case.
    public string? DocumentIssuingCountry { get; set; }

    public DateOnly? DocumentExpiryDate { get; set; }
    public bool IsLead { get; set; }

    /// Contact details are collected for the lead passenger.
    public string? Email { get; set; }

    public string? Phone { get; set; }

    public PassengerType? TypeAt(DateOnly checkIn) =>
        BirthDate is { } birthDate ? DeriveType(birthDate, checkIn) : null;

    /// Age in completed years at check-in: under 2 → Infant, under 12 → Child, otherwise Adult.
    public static PassengerType DeriveType(DateOnly birthDate, DateOnly checkIn)
    {
        var age = checkIn.Year - birthDate.Year;
        if (birthDate > checkIn.AddYears(-age))
            age--;

        return age switch
        {
            < 2 => PassengerType.Infant,
            < 12 => PassengerType.Child,
            _ => PassengerType.Adult,
        };
    }
}

public enum Gender
{
    Male,
    Female,
}

public enum TravelDocumentType
{
    Passport,
    IdCard,
}

public enum PassengerType
{
    Adult,
    Child,
    Infant,
}
