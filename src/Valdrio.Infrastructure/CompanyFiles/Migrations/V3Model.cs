using Microsoft.EntityFrameworkCore;

namespace Valdrio.Infrastructure.CompanyFiles.Migrations;

internal static class V3Model
{
    internal static void Configure(ModelBuilder m)
    {
        V2Model.Configure(m);
        m.Entity<StudyRow>(b =>
        {
            b.ToTable("Studies", t =>
            {
                t.HasCheckConstraint("CK_StudyRevision", "Revision>=0");
                t.HasCheckConstraint("CK_StudyState", "State IN('Open','Closed')");
                t.HasCheckConstraint("CK_StudyPeriod", "length(PeriodStart)=10 AND length(PeriodEnd)=10 AND PeriodStart<=PeriodEnd");
                t.HasCheckConstraint("CK_StudyNoRun", "CurrentRunId IS NULL");
            });
            b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
            b.HasAlternateKey(x => new { x.CompanyId, x.Id });
            b.Property(x => x.CompanyId).IsRequired();
            b.Property(x => x.SelectedModelVersionId).IsRequired();
            b.Property(x => x.PeriodStart).IsRequired(); b.Property(x => x.PeriodEnd).IsRequired();
            b.Property(x => x.FunctionalCurrencyCode).IsRequired(); b.Property(x => x.State).IsRequired();
            b.Property(x => x.Revision).IsConcurrencyToken(); b.Property(x => x.ContextJson).IsRequired();
            b.Property(x => x.CurrentRunId);
            b.HasIndex(x => new { x.CompanyId, x.SelectedModelVersionId });
            b.HasOne<ModelVersionRow>().WithMany().HasForeignKey(x => new { x.CompanyId, x.SelectedModelVersionId })
                .HasPrincipalKey(x => new { x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<StudySnapshotRow>(b =>
        {
            b.ToTable("StudySnapshots", t => t.HasCheckConstraint("CK_SnapshotRevision", "InputRevision>=0"));
            b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CompanyId).IsRequired(); b.Property(x => x.StudyId).IsRequired();
            b.Property(x => x.InputRevision);
            b.Property(x => x.CapturedAtUtc).IsRequired(); b.Property(x => x.PayloadSchema).IsRequired();
            b.Property(x => x.EconomicInputHash).IsRequired(); b.Property(x => x.FullSnapshotHash).IsRequired();
            b.Property(x => x.CanonicalJson).IsRequired();
            b.HasIndex(x => new { x.CompanyId, x.StudyId });
            b.HasOne<StudyRow>().WithMany().HasForeignKey(x => new { x.CompanyId, x.StudyId })
                .HasPrincipalKey(x => new { x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
