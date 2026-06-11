using Microsoft.EntityFrameworkCore;
using Odisea.Domain.Entities;

namespace Odisea.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<Offer> Offers { get; }
    DbSet<Collection> Collections { get; }
    DbSet<Agency> Agencies { get; }
    DbSet<Operator> Operators { get; }
    DbSet<Theme> Themes { get; }
    DbSet<Experience> Experiences { get; }
    DbSet<Publication> Publications { get; }
    DbSet<User> Users { get; }
    DbSet<Membership> Memberships { get; }
    DbSet<Event> Events { get; }

    // Embed security (issue #27). NOTE: these entities are NOT yet in a migration —
    // #23 owns the next migration this round and must consolidate ApiKey +
    // AllowedDomain (and the Publication.AllowedDomains → AllowedDomain relation).
    DbSet<ApiKey> ApiKeys { get; }
    DbSet<AllowedDomain> AllowedDomains { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
