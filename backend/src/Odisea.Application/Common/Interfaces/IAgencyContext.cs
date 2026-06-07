namespace Odisea.Application.Common.Interfaces;

public interface IAgencyContext
{
    bool HasAgency { get; }
    Guid AgencyId { get; }
}
