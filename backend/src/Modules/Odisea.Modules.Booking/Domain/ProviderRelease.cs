namespace Odisea.Modules.Booking.Domain;

/// Where giving a supplier hold back stands, once the booking itself has left the active states.
public enum ProviderReleaseStatus
{
    /// Nothing to release: the supplier never held this booking, or it predates tracking.
    None,
    /// The booking is closed in Odisea; the supplier still has to let the hold go (first try or retry).
    Pending,
    Released,
    /// The supplier refused and still reports the booking as held: an operator has to resolve it.
    Failed,
}
