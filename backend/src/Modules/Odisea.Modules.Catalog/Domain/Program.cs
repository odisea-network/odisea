using Odisea.SharedKernel;

namespace Odisea.Modules.Catalog.Domain;

/// A packaged product the operator sells: destination + season + selected hotels
/// + departures, fulfilled through one reservation provider.
public class Program : Entity
{
    public const int MaxDescriptionLength = 20_000;

    public required string Name { get; set; }
    public required Guid DestinationId { get; set; }
    public required string ProviderCode { get; set; }

    /// URL-safe identity for agency websites and public pages; unique, derived from the name
    /// unless the operator sets it.
    public required string Slug { get; set; }

    public string? Season { get; set; }

    /// The long description (Markdown). The sell-ready sections live in Content.
    public string? Description { get; set; }

    public ProgramStatus Status { get; private set; } = ProgramStatus.Draft;
    public ProgramContent Content { get; set; } = new();

    public List<ProgramDeparture> Departures { get; } = [];
    public List<ProgramHotel> Hotels { get; } = [];
    public List<ProgramItineraryDay> Itinerary { get; } = [];

    /// Publishing is gated on the readiness report's required checks, so a program agencies
    /// can see always has the minimum a customer needs to decide.
    public void Publish(ReadinessReport readiness)
    {
        if (Status == ProgramStatus.Archived)
            throw new InvalidProgramStateException("An archived program cannot be published.");
        if (!readiness.CanPublish)
            throw new InvalidProgramStateException(
                "The program is not ready to publish. Missing: "
                + string.Join("; ", readiness.Blocking.Select(c => c.Title.ToLowerInvariant())) + ".");
        Status = ProgramStatus.Published;
    }

    public void Archive() => Status = ProgramStatus.Archived;
}

public enum ProgramStatus
{
    Draft,
    Published,
    Archived,
}

public sealed class InvalidProgramStateException(string message) : DomainException(message);

/// Content that does not meet the catalogue's rules (lengths, counts, formats, slug clashes).
public sealed class ProgramValidationException(string message) : DomainException(message);
