using Odisea.Domain.Entities;

namespace Odisea.Application.Leads.Dtos;

// Public ingest payload from an embedded surface. AgencyId is never trusted from
// the client — it's resolved from the publication at ingest. Kind defaults to
// Inquiry; supplying an OfferId + party detail makes it a BookingRequest.
public record CreateLeadRequest(
    string PublicationKey,
    string ContactName,
    string ContactEmail,
    string? ContactPhone,
    string? Message,
    Guid? OfferId,
    int? PartySize,
    DateOnly? PreferredDepartureDate,
    int? Nights,
    string? Channel
);

public record LeadDto(
    Guid Id,
    string Kind,
    string Status,
    Guid AgencyId,
    string PublicationKey,
    string Channel,
    string ContactName,
    string ContactEmail,
    string? ContactPhone,
    string? Message,
    Guid? OfferId,
    int? PartySize,
    DateOnly? PreferredDepartureDate,
    int? Nights,
    DateTime CreatedAt
);

public record UpdateLeadStatusRequest(string Status);

public static class LeadMappings
{
    public static LeadDto ToDto(this Lead l) => new(
        l.Id, l.Kind.ToString(), l.Status.ToString(), l.AgencyId,
        l.PublicationKey, l.Channel.ToString(),
        l.ContactName, l.ContactEmail, l.ContactPhone, l.Message,
        l.OfferId, l.PartySize, l.PreferredDepartureDate, l.Nights, l.CreatedAt);
}
