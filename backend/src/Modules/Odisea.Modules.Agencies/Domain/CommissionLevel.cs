using Odisea.SharedKernel;

namespace Odisea.Modules.Agencies.Domain;

public class CommissionLevel : Entity
{
    public required string Name { get; set; }
    public decimal Percent { get; set; }
}
