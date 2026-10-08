using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Catalog.Domain;

namespace Odisea.Modules.Catalog.Infrastructure;

public class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public const string Schema = "catalog";

    public DbSet<Destination> Destinations => Set<Destination>();
    public DbSet<Hotel> Hotels => Set<Hotel>();
    public DbSet<Domain.Program> Programs => Set<Domain.Program>();
    public DbSet<ProgramDeparture> ProgramDepartures => Set<ProgramDeparture>();
    public DbSet<ProgramHotel> ProgramHotels => Set<ProgramHotel>();
    public DbSet<HotelMapping> HotelMappings => Set<HotelMapping>();
    public DbSet<LocationMapping> LocationMappings => Set<LocationMapping>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
    }
}
