using Microsoft.EntityFrameworkCore;

namespace Valdrio.Infrastructure.CompanyFiles.Migrations;

internal static class V2Model
{
    public static void Configure(ModelBuilder m)
    {
        V1Model.Configure(m);
        m.Entity<UnitRow>().HasAlternateKey(x => new { x.CompanyId, x.Id });
        var old = m.Entity<AuditRow>().Metadata.FindIndex([m.Entity<AuditRow>().Metadata.FindProperty("CompanyId")!, m.Entity<AuditRow>().Metadata.FindProperty("Revision")!]);
        if (old is not null) m.Entity<AuditRow>().Metadata.RemoveIndex(old);
        m.Entity<AuditRow>(b =>
        {
            foreach (var name in new[] { "AggregateType", "AggregateId", "PayloadSchema", "TransactionId" }) b.Property<string>(name).IsRequired();
            b.HasIndex("CompanyId", "AggregateType", "AggregateId", "Revision").IsUnique();
            b.HasIndex("TransactionId").IsUnique();
        });
        m.Entity<UnitStateRow>(b =>
        {
            b.ToTable("UnitStates", t => { t.HasCheckConstraint("CK_UnitStateActive", "IsActive IN (0,1)"); t.HasCheckConstraint("CK_UnitStateRevision", "Revision >= 0"); });
            b.HasKey(x => new { x.CompanyId, x.UnitId });
            b.Property(x => x.CompanyId).IsRequired(); b.Property(x => x.UnitId).IsRequired();
            b.Property(x => x.Revision).IsConcurrencyToken(); b.Property(x => x.IsActive);
            b.HasOne<UnitRow>().WithMany().HasForeignKey(x => new { x.CompanyId, x.UnitId }).HasPrincipalKey(x => new { x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<PhysicalCenterRow>(b =>
        {
            b.ToTable("PhysicalCenters", t => { t.HasCheckConstraint("CK_CenterActive", "IsActive IN (0,1)"); t.HasCheckConstraint("CK_CenterRevision", "Revision >= 0"); });
            b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.CompanyId, x.Id }); b.Property(x => x.Id).ValueGeneratedNever();
            b.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique(); b.Property(x => x.Code).IsRequired().UseCollation("BINARY");
            b.Property(x => x.Name).IsRequired(); b.Property(x => x.CompanyId).IsRequired(); b.Property(x => x.Revision).IsConcurrencyToken(); b.Property(x => x.IsActive);
            b.HasOne<CompanyRow>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<ModelFamilyRow>(b =>
        {
            b.ToTable("ModelFamilies"); b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever(); b.Property(x => x.CompanyId).IsRequired();
            b.HasAlternateKey(x => new { x.CompanyId, x.Id });
            b.HasOne<CompanyRow>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<ModelVersionRow>(b =>
        {
            b.ToTable("ModelVersions", t => { t.HasCheckConstraint("CK_ModelStatus", "Status IN ('Draft','Published')"); t.HasCheckConstraint("CK_ModelRevision", "Revision >= 0"); t.HasCheckConstraint("CK_ModelVersion", "VersionNumber >= 1"); });
            b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever(); b.HasAlternateKey(x => new { x.CompanyId, x.Id });
            b.HasIndex(x => new { x.FamilyId, x.VersionNumber }).IsUnique();
            b.HasIndex(x => new { x.CompanyId, x.FamilyId }); b.HasIndex(x => new { x.CompanyId, x.ParentVersionId }); b.Property(x => x.Revision).IsConcurrencyToken();
            b.Property(x => x.CompanyId).IsRequired(); b.Property(x => x.FamilyId).IsRequired(); b.Property(x => x.ParentVersionId);
            b.Property(x => x.Description).IsRequired(); b.Property(x => x.Status).IsRequired(); b.Property(x => x.VersionNumber); b.Property(x => x.PublishedAtUtc);
            b.Property(x => x.PrecisionPolicyVersion).IsRequired(); b.Property(x => x.MethodPolicyJson).IsRequired();
            b.HasOne<ModelFamilyRow>().WithMany().HasForeignKey(x => new { x.CompanyId, x.FamilyId }).HasPrincipalKey(x => new { x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ModelVersionRow>().WithMany().HasForeignKey(x => new { x.CompanyId, x.ParentVersionId }).HasPrincipalKey(x => new { x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<NodeRow>(b =>
        {
            b.ToTable("CalculationNodes", t => { t.HasCheckConstraint("CK_NodeRole", "Role IN ('Principal','Auxiliary')"); t.HasCheckConstraint("CK_NodeActive", "IsActive IN (0,1)"); });
            b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever(); b.HasAlternateKey(x => new { x.ModelVersionId, x.Id });
            b.HasIndex(x => new { x.ModelVersionId, x.Code }).IsUnique();
            b.HasIndex(x => new { x.CompanyId, x.ModelVersionId }); b.HasIndex(x => new { x.CompanyId, x.PhysicalCenterId });
            b.Property(x => x.Code).IsRequired().UseCollation("BINARY"); b.Property(x => x.CompanyId).IsRequired(); b.Property(x => x.ModelVersionId).IsRequired();
            b.Property(x => x.PhysicalCenterId).IsRequired(); b.Property(x => x.Role).IsRequired(); b.Property(x => x.FunctionTagsJson).IsRequired(); b.Property(x => x.IsActive);
            b.HasOne<ModelVersionRow>().WithMany().HasForeignKey(x => new { x.CompanyId, x.ModelVersionId }).HasPrincipalKey(x => new { x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<PhysicalCenterRow>().WithMany().HasForeignKey(x => new { x.CompanyId, x.PhysicalCenterId }).HasPrincipalKey(x => new { x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<PoolRow>(b =>
        {
            b.ToTable("CostPools", t => t.HasCheckConstraint("CK_CostLayer", "CostLayer IN ('Unclassified','Fixed','Variable','Mixed')"));
            b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever(); b.HasIndex(x => new { x.ModelVersionId, x.NodeId }).IsUnique();
            b.HasIndex(x => new { x.CompanyId, x.DriverUnitId }); b.HasIndex(x => new { x.CompanyId, x.ModelVersionId });
            b.Property(x => x.CompanyId).IsRequired(); b.Property(x => x.ModelVersionId).IsRequired(); b.Property(x => x.NodeId).IsRequired(); b.Property(x => x.DriverUnitId).IsRequired();
            b.Property(x => x.Description).IsRequired(); b.Property(x => x.CostLayer).IsRequired(); b.Property(x => x.CapacityPolicyJson).IsRequired();
            b.HasOne<ModelVersionRow>().WithMany().HasForeignKey(x => new { x.CompanyId, x.ModelVersionId }).HasPrincipalKey(x => new { x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<NodeRow>().WithMany().HasForeignKey(x => new { x.ModelVersionId, x.NodeId }).HasPrincipalKey(x => new { x.ModelVersionId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<UnitRow>().WithMany().HasForeignKey(x => new { x.CompanyId, x.DriverUnitId }).HasPrincipalKey(x => new { x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<PublicationRow>(b =>
        {
            b.ToTable("ModelPublications"); b.HasKey(x => x.ModelVersionId); b.Property(x => x.ModelVersionId).ValueGeneratedNever();
            b.Property(x => x.PayloadSchema).IsRequired(); b.Property(x => x.CanonicalJson).IsRequired(); b.Property(x => x.StructuralHash).IsRequired();
            b.HasOne<ModelVersionRow>().WithOne().HasForeignKey<PublicationRow>(x => x.ModelVersionId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
