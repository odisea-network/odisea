using Microsoft.EntityFrameworkCore;
using Odisea.Domain.Entities;

namespace Odisea.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<Offer> Offers { get; }
    DbSet<PriceVariant> PriceVariants { get; }
    DbSet<Collection> Collections { get; }
    DbSet<Agency> Agencies { get; }
    DbSet<Operator> Operators { get; }
    DbSet<SupplierConnection> SupplierConnections { get; }
    DbSet<ImportJob> ImportJobs { get; }
    DbSet<SourceOffer> SourceOffers { get; }
    DbSet<OperatorAgencyEntitlement> Entitlements { get; }
    DbSet<Theme> Themes { get; }
    DbSet<Experience> Experiences { get; }
    DbSet<Publication> Publications { get; }
    DbSet<User> Users { get; }
    DbSet<Membership> Memberships { get; }
    DbSet<Event> Events { get; }
    DbSet<ApiKey> ApiKeys { get; }
    DbSet<AllowedDomain> AllowedDomains { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
