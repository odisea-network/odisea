using Odisea.SharedKernel;

namespace Odisea.Modules.Catalog.Domain;

public class ProgramDeparture : Entity
{
    public required Guid ProgramId { get; set; }
    public required DateOnly StartDate { get; set; }
    public required DateOnly EndDate { get; set; }
    public string? TransportNote { get; set; }

    public DateRange Period => new(StartDate, EndDate);
}
