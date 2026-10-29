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
        builder.Property(p => p.Slug).HasMaxLength(Slug.MaxLength);
        builder.Property(p => p.Season).HasMaxLength(50);
        builder.Property(p => p.Description).HasMaxLength(Domain.Program.MaxDescriptionLength);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasOne<Destination>().WithMany().HasForeignKey(p => p.DestinationId);
        builder.HasMany(p => p.Departures).WithOne().HasForeignKey(d => d.ProgramId);
        builder.HasMany(p => p.Hotels).WithOne().HasForeignKey(h => h.ProgramId);
        builder.HasMany(p => p.Itinerary).WithOne().HasForeignKey(d => d.ProgramId);
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.Slug).IsUnique();

        // Same table, plain column names (no content_ prefix): the content is part of the program.
        builder.OwnsOne(p => p.Content, content =>
        {
            content.Property(c => c.Tagline).HasColumnName("tagline").HasMaxLength(160);
            content.Property(c => c.Summary).HasColumnName("summary").HasMaxLength(600);
            content.Property(c => c.Highlights).HasColumnName("highlights");
            content.Property(c => c.Inclusions).HasColumnName("inclusions");
            content.Property(c => c.Exclusions).HasColumnName("exclusions");
            content.Property(c => c.Tags).HasColumnName("tags");
            content.Property(c => c.TransportMode).HasColumnName("transport_mode").HasConversion<string>().HasMaxLength(20);
            content.Property(c => c.TransportDetails).HasColumnName("transport_details").HasMaxLength(4000);
            content.Property(c => c.DeparturePoint).HasColumnName("departure_point").HasMaxLength(200);
            content.Property(c => c.MinGroupSize).HasColumnName("min_group_size");
            content.Property(c => c.MaxGroupSize).HasColumnName("max_group_size");
            content.Property(c => c.PracticalInfo).HasColumnName("practical_info").HasMaxLength(8000);
            content.Property(c => c.CancellationPolicy).HasColumnName("cancellation_policy").HasMaxLength(8000);
            content.Property(c => c.PaymentTerms).HasColumnName("payment_terms").HasMaxLength(4000);
            content.Property(c => c.InsurancePolicy).HasColumnName("insurance_policy").HasMaxLength(4000);
            content.Property(c => c.ChildPolicy).HasColumnName("child_policy").HasMaxLength(2000);
        });
        builder.Navigation(p => p.Content).IsRequired();
    }
}

public class ProgramItineraryDayConfiguration : IEntityTypeConfiguration<ProgramItineraryDay>
{
    public void Configure(EntityTypeBuilder<ProgramItineraryDay> builder)
    {
        builder.Property(d => d.Title).HasMaxLength(200);
        builder.Property(d => d.Description).HasMaxLength(4000);
        builder.Property(d => d.Meals).HasConversion<string>().HasMaxLength(40);
        builder.Property(d => d.Overnight).HasMaxLength(200);
        builder.HasIndex(d => new { d.ProgramId, d.Day }).IsUnique();
    }
}

public class ProgramImageConfiguration : IEntityTypeConfiguration<ProgramImage>
{
    public void Configure(EntityTypeBuilder<ProgramImage> builder)
    {
        builder.Property(i => i.ContentType).HasMaxLength(40);
        builder.Property(i => i.Sha256).HasMaxLength(64);
        builder.Property(i => i.Caption).HasMaxLength(200);
        builder.Property(i => i.AltText).HasMaxLength(300);
        builder.HasOne<Domain.Program>().WithMany().HasForeignKey(i => i.ProgramId);
        builder.HasIndex(i => new { i.ProgramId, i.SortOrder });
        // One cover per program, enforced by the database.
        builder.HasIndex(i => i.ProgramId).IsUnique().HasFilter("is_cover").HasDatabaseName("ix_program_images_cover");
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
