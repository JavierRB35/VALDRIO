using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Valdrio.Infrastructure.CompanyFiles.Migrations;
internal static class V8Model
{
    internal static void Configure(ModelBuilder model)
    {
        V7Model.Configure(model);
        model.Entity<ResourceChangeRow>(b =>
        {
            b.ToTable("ResourceChanges", t => t.HasCheckConstraint("CK_ResourceRevision", "ExpectedRevision>=0 AND ConfirmedRevision=ExpectedRevision+1"));
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
[DbContext(typeof(ResourceDbContext))]
[Migration(SqliteCompanyFileService.ResourceMigrationId)]
internal sealed class ResourcePreparation : Migration
{
    protected override void Up(MigrationBuilder migration) => migration.Sql("""
        CREATE TABLE ResourceChanges (
            Id TEXT NOT NULL CONSTRAINT PK_ResourceChanges PRIMARY KEY,
            CompanyId TEXT NOT NULL, StudyId TEXT NOT NULL,
            ExpectedRevision INTEGER NOT NULL, ConfirmedRevision INTEGER NOT NULL,
            CanonicalJson TEXT NOT NULL,
            CONSTRAINT CK_ResourceRevision CHECK (ExpectedRevision>=0 AND ConfirmedRevision=ExpectedRevision+1),
            CONSTRAINT FK_ResourceChanges_Studies_CompanyId_StudyId FOREIGN KEY (CompanyId,StudyId) REFERENCES Studies(CompanyId,Id) ON DELETE RESTRICT);
        CREATE UNIQUE INDEX IX_ResourceChanges_CompanyId_StudyId_ConfirmedRevision ON ResourceChanges(CompanyId,StudyId,ConfirmedRevision);
        """);
    protected override void Down(MigrationBuilder migration) => throw new NotSupportedException();
}
[DbContext(typeof(ResourceDbContext))]
internal sealed class ResourceDbContextSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder model) => V8Model.Configure(model);
}
