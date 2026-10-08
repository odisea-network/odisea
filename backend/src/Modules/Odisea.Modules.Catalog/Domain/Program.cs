using Odisea.SharedKernel;

namespace Odisea.Modules.Catalog.Domain;

/// A packaged product the operator sells: destination + season + selected hotels
/// + departures, fulfilled through one reservation provider.
public class Program : Entity
{
    public required string Name { get; set; }
    public required Guid DestinationId { get; set; }
    public required string ProviderCode { get; set; }
    public string? Season { get; set; }
    public string? Description { get; set; }
    public ProgramStatus Status { get; private set; } = ProgramStatus.Draft;

    public List<ProgramDeparture> Departures { get; } = [];
    public List<ProgramHotel> Hotels { get; } = [];

    public void Publish()
    {
        if (Status == ProgramStatus.Archived)
            throw new InvalidProgramStateException("An archived program cannot be published.");
        if (Departures.Count == 0 || Hotels.Count == 0)
            throw new InvalidProgramStateException(
                "A program needs at least one departure and one allowed hotel before publishing.");
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
