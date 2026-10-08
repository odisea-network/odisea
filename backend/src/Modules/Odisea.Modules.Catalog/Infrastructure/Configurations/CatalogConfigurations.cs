using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odisea.Modules.Catalog.Domain;

namespace Odisea.Modules.Catalog.Infrastructure.Configurations;

public class DestinationConfiguration : IEntityTypeConfiguration<Destination>
{
    public void Configure(EntityTypeBuilder<Destination> builder)
    {
        builder.Property(d => d.Name).HasMaxLength(200);
        builder.Property(d => d.Country).HasMaxLength(2);
        builder.HasIndex(d => new { d.Name, d.Country }).IsUnique();
    }
}

public class HotelConfiguration : IEntityTypeConfiguration<Hotel>
{
    public void Configure(EntityTypeBuilder<Hotel> builder)
    {
        builder.Property(h => h.Name).HasMaxLength(200);
        builder.HasOne<Destination>().WithMany().HasForeignKey(h => h.DestinationId);
        builder.HasIndex(h => h.DestinationId);
    }
}

public class ProgramConfiguration : IEntityTypeConfiguration<Domain.Program>
{
    public void Configure(EntityTypeBuilder<Domain.Program> builder)
    {
        builder.Property(p => p.Name).HasMaxLength(200);
        builder.Property(p => p.ProviderCode).HasMaxLength(30);
        builder.Property(p => p.Season).HasMaxLength(50);
        builder.Property(p => p.Description).HasMaxLength(2000);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasOne<Destination>().WithMany().HasForeignKey(p => p.DestinationId);
        builder.HasMany(p => p.Departures).WithOne().HasForeignKey(d => d.ProgramId);
        builder.HasMany(p => p.Hotels).WithOne().HasForeignKey(h => h.ProgramId);
        builder.HasIndex(p => p.Status);
    }
}

public class ProgramDepartureConfiguration : IEntityTypeConfiguration<ProgramDeparture>
{
    public void Configure(EntityTypeBuilder<ProgramDeparture> builder)
    {
        builder.Property(d => d.TransportNote).HasMaxLength(500);
        builder.HasIndex(d => new { d.ProgramId, d.StartDate });
    }
}

public class ProgramHotelConfiguration : IEntityTypeConfiguration<ProgramHotel>
{
    public void Configure(EntityTypeBuilder<ProgramHotel> builder)
    {
        builder.HasIndex(h => new { h.ProgramId, h.HotelId }).IsUnique();
        builder.HasOne<Hotel>().WithMany().HasForeignKey(h => h.HotelId);
    }
}

public class HotelMappingConfiguration : IEntityTypeConfiguration<HotelMapping>
{
    public void Configure(EntityTypeBuilder<HotelMapping> builder)
    {
        builder.Property(m => m.ProviderCode).HasMaxLength(30);
        builder.Property(m => m.ExternalHotelCode).HasMaxLength(100);
        builder.HasOne<Hotel>().WithMany().HasForeignKey(m => m.HotelId);
        builder.HasIndex(m => new { m.ProviderCode, m.ExternalHotelCode }).IsUnique();
        builder.HasIndex(m => new { m.HotelId, m.ProviderCode }).IsUnique();
    }
}

public class LocationMappingConfiguration : IEntityTypeConfiguration<LocationMapping>
{
    public void Configure(EntityTypeBuilder<LocationMapping> builder)
    {
        builder.Property(m => m.ProviderCode).HasMaxLength(30);
        builder.Property(m => m.ExternalLocationCode).HasMaxLength(100);
        builder.HasOne<Destination>().WithMany().HasForeignKey(m => m.DestinationId);
        builder.HasIndex(m => new { m.ProviderCode, m.ExternalLocationCode }).IsUnique();
        builder.HasIndex(m => new { m.DestinationId, m.ProviderCode }).IsUnique();
    }
}
