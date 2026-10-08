using Odisea.SharedKernel;

namespace Odisea.Modules.Catalog.Domain;

/// One hotel the operator allows inside a program.
public class ProgramHotel : Entity
{
    public required Guid ProgramId { get; set; }
    public required Guid HotelId { get; set; }
}
