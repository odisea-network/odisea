using Odisea.SharedKernel;

namespace Odisea.Modules.Booking.Domain;

public class BookingPassenger : Entity
{
    public required Guid BookingId { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public DateOnly? BirthDate { get; set; }
    public bool IsLead { get; set; }
}
