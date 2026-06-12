using Odisea.Domain.Common;
using Odisea.Domain.Enums;

namespace Odisea.Domain.Entities;

// A traveler's inbound interest captured from an embedded surface. The structured
// end of the analytics funnel: an InquirySubmit Event is the signal, a Lead is the
// actionable record an agency works in its inbox.
//
// Kind discriminates a general Inquiry (just contact + message) from a
// BookingRequest (tied to a specific Offer with party/date detail). Modelled as one
// table because the two share contact + attribution + pipeline; the booking-only
// fields are simply null for an Inquiry.
public class Lead : Entity
{
    public LeadKind Kind { get; set; } = LeadKind.Inquiry;
    public LeadStatus Status { get; set; } = LeadStatus.New;

    // Owner — resolved from the publication's agency at ingest, so an agency only
    // ever sees leads attributed to its own publications.
    public Guid AgencyId { get; set; }

    // Attribution back to the surface that produced the lead.
    public string PublicationKey { get; set; } = string.Empty;
    public Channel Channel { get; set; } = Channel.WebComponent;

    // Contact.
    public string ContactName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string? ContactPhone { get; set; }
    public string? Message { get; set; }

    // Offer of interest. Required for a BookingRequest, optional for an Inquiry.
    public Guid? OfferId { get; set; }

    // Booking detail — only meaningful for a BookingRequest.
    public int? PartySize { get; set; }
    public DateOnly? PreferredDepartureDate { get; set; }
    public int? Nights { get; set; }
}
