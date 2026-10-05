using Microsoft.EntityFrameworkCore;

namespace Valdrio.Infrastructure.CompanyFiles.Migrations;

// Frozen schema v1, shared by the first context and its historical snapshot.
// Never edit for a future schema: introduce V2Model and a new migration instead.
internal static class V1Model
{
    public static void Configure(ModelBuilder m)
    {
        m.HasAnnotation("ProductVersion", "10.0.12");
        m.Entity<FileMetadataRow>(b =>
        {
            b.ToTable("FileMetadata", t => t.HasCheckConstraint("CK_MetadataSlot", "Slot = 1"));
            b.HasKey(x => x.Slot); b.Property(x => x.Slot).ValueGeneratedNever();
            b.Property(x => x.SchemaVersion);
            b.Property(x => x.Format).IsRequired(); b.Property(x => x.CatalogVersion).IsRequired();
        });
        m.Entity<CompanyRow>(b =>
        {
            b.ToTable("Companies", t => { t.HasCheckConstraint("CK_CompanySlot", "Slot = 1");
                t.HasCheckConstraint("CK_Revision", "Revision >= 0"); });
            b.HasKey(x => x.Id); b.HasIndex(x => x.Slot).IsUnique();
            b.Property(x => x.Id).IsRequired().ValueGeneratedNever(); b.Property(x => x.Slot);
            b.Property(x => x.Name).IsRequired(); b.Property(x => x.Currency).IsRequired();
            b.Property(x => x.CreatedAt).IsRequired(); b.Property(x => x.Revision).IsConcurrencyToken();
        });
        m.Entity<PolicyRow>(b =>
        {
            b.ToTable("PrecisionPolicies"); b.HasKey(x => x.CompanyId);
            b.Property(x => x.CompanyId).IsRequired().ValueGeneratedNever();
            b.Property(x => x.AmountScale); b.Property(x => x.QuantityScale); b.Property(x => x.PercentageScale);
            b.HasOne<CompanyRow>().WithOne().HasForeignKey<PolicyRow>(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Version).IsRequired();
            b.Property(x => x.MaximumInitialMagnitude).HasConversion<CanonicalDecimalConverter>().HasColumnType("TEXT");
            b.Property(x => x.MaximumNumericCorrection).HasConversion<CanonicalDecimalConverter>().HasColumnType("TEXT");
        });
        m.Entity<UnitRow>(b =>
        {
            b.ToTable("Units", t => { t.HasCheckConstraint("CK_UnitScale", "DecimalScale BETWEEN 0 AND 12");
                t.HasCheckConstraint("CK_UnitDiscrete", "IsDiscrete IN (0,1)"); });
            b.HasKey(x => x.Id); b.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
            b.Property(x => x.Id).IsRequired().ValueGeneratedNever(); b.Property(x => x.CompanyId).IsRequired();
            b.Property(x => x.DecimalScale); b.Property(x => x.IsDiscrete);
            b.HasOne<CompanyRow>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Code).IsRequired().UseCollation("BINARY");
            b.Property(x => x.Dimension).IsRequired(); b.Property(x => x.Symbol).IsRequired();
        });
        m.Entity<AuditRow>(b =>
        {
            b.ToTable("AuditEvents"); b.HasKey(x => x.Id);
            b.Property(x => x.Id).IsRequired().ValueGeneratedNever(); b.Property(x => x.CompanyId).IsRequired();
            b.Property(x => x.Revision);
            b.HasOne<CompanyRow>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.CompanyId, x.Revision }).IsUnique();
            b.Property(x => x.Operation).IsRequired(); b.Property(x => x.Actor).IsRequired();
            b.Property(x => x.AtUtc).IsRequired(); b.Property(x => x.BeforeJson).IsRequired(); b.Property(x => x.AfterJson).IsRequired();
        });
    }
}
