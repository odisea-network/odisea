using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Documents.Domain;

namespace Odisea.Modules.Documents.Infrastructure;

public class DocumentsDbContext(DbContextOptions<DocumentsDbContext> options) : DbContext(options)
{
    public const string Schema = "documents";

    public DbSet<FinancialDocument> FinancialDocuments => Set<FinancialDocument>();
    public DbSet<CompanyProfile> CompanyProfiles => Set<CompanyProfile>();
    public DbSet<DocumentCounter> DocumentCounters => Set<DocumentCounter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        var doc = modelBuilder.Entity<FinancialDocument>();
        doc.Property(d => d.Number).HasMaxLength(10);
        doc.HasIndex(d => d.Number).IsUnique();
        doc.HasIndex(d => new { d.AgencyId, d.IssueDate });
        doc.HasIndex(d => d.BookingId);
        doc.Property(d => d.Kind).HasConversion<string>().HasMaxLength(20);
        doc.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);
        doc.Property(d => d.VatTreatment).HasConversion<string>().HasMaxLength(20);
        doc.Property(d => d.SupplierName).HasMaxLength(200);
        doc.Property(d => d.SupplierEik).HasMaxLength(13);
        doc.Property(d => d.SupplierVatNumber).HasMaxLength(15);
        doc.Property(d => d.SupplierAddress).HasMaxLength(300);
        doc.Property(d => d.SupplierCity).HasMaxLength(100);
        doc.Property(d => d.SupplierMol).HasMaxLength(200);
        doc.Property(d => d.SupplierIban).HasMaxLength(34);
        doc.Property(d => d.SupplierBankName).HasMaxLength(100);
        doc.Property(d => d.RecipientName).HasMaxLength(200);
        doc.Property(d => d.RecipientEik).HasMaxLength(13);
        doc.Property(d => d.RecipientVatNumber).HasMaxLength(15);
        doc.Property(d => d.RecipientAddress).HasMaxLength(300);
        doc.Property(d => d.RecipientCity).HasMaxLength(100);
        doc.Property(d => d.RecipientMol).HasMaxLength(200);
        doc.Property(d => d.BookingRef).HasMaxLength(20);
        doc.Property(d => d.RelatedDocumentNumber).HasMaxLength(10);
        doc.Property(d => d.TaxBase).HasPrecision(18, 2);
        doc.Property(d => d.VatAmount).HasPrecision(18, 2);
        doc.Property(d => d.Total).HasPrecision(18, 2);
        doc.Property(d => d.VatRatePercent).HasPrecision(5, 2);
        doc.Property(d => d.Currency).HasMaxLength(3);
        doc.Property(d => d.Description).HasMaxLength(1000);
        doc.Property(d => d.LegalNote).HasMaxLength(500);
        doc.Property(d => d.AnnulmentReason).HasMaxLength(500);

        var profile = modelBuilder.Entity<CompanyProfile>();
        profile.Property(p => p.Name).HasMaxLength(200);
        profile.Property(p => p.Eik).HasMaxLength(13);
        profile.Property(p => p.VatNumber).HasMaxLength(15);
        profile.Property(p => p.Address).HasMaxLength(300);
        profile.Property(p => p.City).HasMaxLength(100);
        profile.Property(p => p.Mol).HasMaxLength(200);
        profile.Property(p => p.Iban).HasMaxLength(34);
        profile.Property(p => p.BankName).HasMaxLength(100);

        var counter = modelBuilder.Entity<DocumentCounter>();
        counter.HasKey(c => c.Series);
        counter.Property(c => c.Series).HasMaxLength(20);
    }
}
