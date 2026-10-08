using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Valdrio.Infrastructure.CompanyFiles.Migrations;
internal static class V9Model
{
    internal static void Configure(ModelBuilder model)
    {
        V8Model.Configure(model);
        model.Entity<AllocationKeyEventRow>(b =>
        {
            b.ToTable("AllocationKeyEvents", t => t.HasCheckConstraint("CK_AllocationKeyRevision", "Revision>=0"));
            b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CompanyId).IsRequired(); b.Property(x => x.ModelVersionId).IsRequired();
            b.Property(x => x.Revision); b.Property(x => x.KeyId).IsRequired(); b.Property(x => x.CanonicalJson).IsRequired();
            b.HasIndex(x => new { x.CompanyId, x.KeyId, x.Revision }).IsUnique();
            b.HasIndex(x => new { x.CompanyId, x.ModelVersionId });
            b.HasOne<ModelVersionRow>().WithMany().HasForeignKey(x => new { x.CompanyId, x.ModelVersionId })
                .HasPrincipalKey(x => new { x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<AllocationChangeRow>(b =>
        {
            b.ToTable("AllocationChanges", t => t.HasCheckConstraint("CK_AllocationRevision", "ExpectedRevision>=0 AND ConfirmedRevision=ExpectedRevision+1"));
            b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CompanyId).IsRequired(); b.Property(x => x.StudyId).IsRequired();
            b.Property(x => x.ExpectedRevision); b.Property(x => x.ConfirmedRevision);
            b.Property(x => x.CanonicalJson).IsRequired();
            b.HasIndex(x => new { x.CompanyId, x.StudyId, x.ConfirmedRevision }).IsUnique();
            b.HasOne<StudyRow>().WithMany().HasForeignKey(x => new { x.CompanyId, x.StudyId })
                .HasPrincipalKey(x => new { x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
[DbContext(typeof(AllocationDbContext))]
[Migration(SqliteCompanyFileService.AllocationMigrationId)]
internal sealed class AllocationKeys : Migration
{
    protected override void Up(MigrationBuilder migration)
    {
        migration.Sql("""
            CREATE TABLE AllocationKeyEvents (
                Id TEXT NOT NULL PRIMARY KEY, CompanyId TEXT NOT NULL, ModelVersionId TEXT NOT NULL,
                KeyId TEXT NOT NULL, Revision INTEGER NOT NULL CHECK(Revision>=0), CanonicalJson TEXT NOT NULL,
                FOREIGN KEY(CompanyId,ModelVersionId) REFERENCES ModelVersions(CompanyId,Id) ON DELETE RESTRICT);
            CREATE UNIQUE INDEX IX_AllocationKeyEvents_CompanyId_KeyId_Revision ON AllocationKeyEvents(CompanyId,KeyId,Revision);
            CREATE INDEX IX_AllocationKeyEvents_CompanyId_ModelVersionId ON AllocationKeyEvents(CompanyId,ModelVersionId);
            CREATE TABLE AllocationChanges (
                Id TEXT NOT NULL PRIMARY KEY, CompanyId TEXT NOT NULL, StudyId TEXT NOT NULL,
                ExpectedRevision INTEGER NOT NULL, ConfirmedRevision INTEGER NOT NULL, CanonicalJson TEXT NOT NULL,
                CHECK(ExpectedRevision>=0 AND ConfirmedRevision=ExpectedRevision+1),
                FOREIGN KEY(CompanyId,StudyId) REFERENCES Studies(CompanyId,Id) ON DELETE RESTRICT);
            CREATE UNIQUE INDEX IX_AllocationChanges_CompanyId_StudyId_ConfirmedRevision ON AllocationChanges(CompanyId,StudyId,ConfirmedRevision);
            """);
        foreach (var table in new[] { "AllocationKeyEvents", "AllocationChanges" })
        {
            migration.Sql($"CREATE TRIGGER {table}Update BEFORE UPDATE ON {table} BEGIN SELECT RAISE(ABORT,'Allocation history is immutable'); END;");
            migration.Sql($"CREATE TRIGGER {table}Delete BEFORE DELETE ON {table} BEGIN SELECT RAISE(ABORT,'Allocation history is immutable'); END;");
        }
    }
    protected override void Down(MigrationBuilder migration) => throw new NotSupportedException();
}
[DbContext(typeof(AllocationDbContext))]
internal sealed class AllocationDbContextSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder model) => V9Model.Configure(model);
}
