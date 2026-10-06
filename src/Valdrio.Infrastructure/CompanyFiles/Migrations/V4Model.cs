using Microsoft.EntityFrameworkCore;

namespace Valdrio.Infrastructure.CompanyFiles.Migrations;

internal static class V4Model
{
    internal static void Configure(ModelBuilder model)
    {
        V3Model.Configure(model);
        model.Entity<ImportBatchRow>(b =>
        {
            b.ToTable("ImportBatches", t => t.HasCheckConstraint("CK_ImportRevision", "ExpectedRevision>=0 AND ConfirmedRevision=ExpectedRevision+1"));
            b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CompanyId).IsRequired(); b.Property(x => x.StudyId).IsRequired();
            b.Property(x => x.ExpectedRevision); b.Property(x => x.ConfirmedRevision);
            b.Property(x => x.FileHash).IsRequired(); b.Property(x => x.PreparedFingerprint).IsRequired();
            b.Property(x => x.DuplicateFingerprint).IsRequired(); b.Property(x => x.CanonicalJson).IsRequired();
            b.HasAlternateKey(x => new { x.CompanyId, x.StudyId, x.Id });
            b.HasIndex(x => new { x.CompanyId, x.StudyId, x.DuplicateFingerprint }).IsUnique();
            b.HasIndex(x => new { x.CompanyId, x.StudyId, x.ConfirmedRevision }).IsUnique();
            b.HasOne<StudyRow>().WithMany().HasForeignKey(x => new { x.CompanyId, x.StudyId })
                .HasPrincipalKey(x => new { x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ImportedRow>(b =>
        {
            b.ToTable("ImportedRows", t => t.HasCheckConstraint("CK_ImportRecord", "RecordNumber>=1"));
            b.HasKey(x => new { x.CompanyId, x.StudyId, x.BatchId, x.RecordNumber });
            b.Property(x => x.RawJson).IsRequired();
            b.Property(x => x.Selected);
            b.Property(x => x.Account).IsRequired(false); b.Property(x => x.Amount).IsRequired(false); b.Property(x => x.Currency).IsRequired(false);
            b.HasOne<ImportBatchRow>().WithMany().HasForeignKey(x => new { x.CompanyId, x.StudyId, x.BatchId })
                .HasPrincipalKey(x => new { x.CompanyId, x.StudyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ResourceRow>(b =>
        {
            b.ToTable("Resources"); b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Account).IsRequired(); b.Property(x => x.OriginalAmount).IsRequired();
            b.Property(x => x.FunctionalAmount).IsRequired(); b.Property(x => x.Currency).IsRequired();
            b.Property(x => x.PeriodStart).IsRequired(); b.Property(x => x.PeriodEnd).IsRequired();
            b.HasAlternateKey(x => new { x.CompanyId, x.StudyId, x.BatchId, x.Id });
            b.HasOne<ImportBatchRow>().WithMany().HasForeignKey(x => new { x.CompanyId, x.StudyId, x.BatchId })
                .HasPrincipalKey(x => new { x.CompanyId, x.StudyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ResourceSourceLinkRow>(b =>
        {
            b.ToTable("ResourceSourceLinks"); b.HasKey(x => x.ResourceId); b.Property(x => x.ResourceId).ValueGeneratedNever();
            b.Property(x => x.CompanyId).IsRequired(); b.Property(x => x.StudyId).IsRequired(); b.Property(x => x.BatchId).IsRequired();
            b.HasIndex(x => new { x.CompanyId, x.StudyId, x.BatchId, x.ResourceId });
            b.HasIndex(x => new { x.CompanyId, x.StudyId, x.BatchId, x.RecordNumber }).IsUnique();
            b.HasOne<ResourceRow>().WithMany().HasForeignKey(x => new { x.CompanyId, x.StudyId, x.BatchId, x.ResourceId })
                .HasPrincipalKey(x => new { x.CompanyId, x.StudyId, x.BatchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ImportedRow>().WithMany().HasForeignKey(x => new { x.CompanyId, x.StudyId, x.BatchId, x.RecordNumber })
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
