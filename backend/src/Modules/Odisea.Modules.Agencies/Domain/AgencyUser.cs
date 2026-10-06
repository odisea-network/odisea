namespace Odisea.Modules.Agencies.Domain;

public class AgencyUser : UserAccount
{
    public required Guid AgencyId { get; set; }
    public AgencyUserRole Role { get; set; } = AgencyUserRole.Agent;
}

public enum AgencyUserRole
{
    AgencyAdmin,
    Agent,
}
