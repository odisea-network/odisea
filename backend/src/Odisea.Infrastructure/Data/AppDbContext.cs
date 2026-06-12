using Microsoft.EntityFrameworkCore;
using Odisea.Application.Common.Interfaces;
using Odisea.Domain.Entities;
using Odisea.Infrastructure.Data.Configurations;

namespace Odisea.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Offer> Offers => Set<Offer>();
    public DbSet<PriceVariant> PriceVariants => Set<PriceVariant>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<Agency> Agencies => Set<Agency>();
    public DbSet<Operator> Operators => Set<Operator>();
    public DbSet<SupplierConnection> SupplierConnections => Set<SupplierConnection>();
    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();
    public DbSet<SourceOffer> SourceOffers => Set<SourceOffer>();
    public DbSet<Theme> Themes => Set<Theme>();
    public DbSet<Experience> Experiences => Set<Experience>();
    public DbSet<Publication> Publications => Set<Publication>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<AllowedDomain> AllowedDomains => Set<AllowedDomain>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OfferConfiguration).Assembly);
    }
}
