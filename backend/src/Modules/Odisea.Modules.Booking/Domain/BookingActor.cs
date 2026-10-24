namespace Odisea.Modules.Booking.Domain;

/// Who caused a status change. Recorded on every history row so operators can audit a
/// reservation's path without correlating logs.
public sealed record BookingActor(BookingActorType Type, Guid? UserId)
{
    public static readonly BookingActor System = new(BookingActorType.System, null);
}

public enum BookingActorType
{
    Agency,
    ApiClient,
    Operator,
    System,
}
