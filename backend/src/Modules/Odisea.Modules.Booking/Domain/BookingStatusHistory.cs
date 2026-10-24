using Odisea.SharedKernel;

namespace Odisea.Modules.Booking.Domain;

public class BookingStatusHistory : Entity
{
    public required Guid BookingId { get; set; }
    public required string FromStatus { get; set; }
    public required string ToStatus { get; set; }
    public required BookingActorType ActorType { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? Note { get; set; }
}
