using Microsoft.EntityFrameworkCore;
using Odisea.Application.Common.Interfaces;
using Odisea.Domain.Entities;
using Odisea.Infrastructure.Data.Configurations;

namespace Odisea.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Offer> Offers => Set<Offer>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<Agency> Agencies => Set<Agency>();
    public DbSet<Operator> Operators => Set<Operator>();
    public DbSet<Theme> Themes => Set<Theme>();
    public DbSet<Experience> Experiences => Set<Experience>();
    public DbSet<Publication> Publications => Set<Publication>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Membership> Memberships => Set<Membership>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OfferConfiguration).Assembly);
    }
}
