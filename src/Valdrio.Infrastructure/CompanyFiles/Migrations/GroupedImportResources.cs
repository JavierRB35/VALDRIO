using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Valdrio.Infrastructure.CompanyFiles.Migrations;

internal static class V5Model
{
    internal static void Configure(ModelBuilder model)
    {
        V4Model.Configure(model);
        model.Entity<ResourceSourceLinkRow>().HasKey(x => new { x.ResourceId, x.RecordNumber });
    }
}

[DbContext(typeof(GroupedImportDbContext))]
[Migration("202610070005_GroupedImportResources")]
internal sealed class GroupedImportResources : Migration
{
    protected override void BuildTargetModel(ModelBuilder model) => V5Model.Configure(model);
    protected override void Up(MigrationBuilder migration) => migration.Sql("""
        CREATE TABLE GroupedResourceSourceLinks (ResourceId TEXT NOT NULL, CompanyId TEXT NOT NULL,
          StudyId TEXT NOT NULL, BatchId TEXT NOT NULL, RecordNumber INTEGER NOT NULL,
          PRIMARY KEY(ResourceId,RecordNumber),
          FOREIGN KEY(CompanyId,StudyId,BatchId,ResourceId) REFERENCES Resources(CompanyId,StudyId,BatchId,Id) ON DELETE RESTRICT,
          FOREIGN KEY(CompanyId,StudyId,BatchId,RecordNumber) REFERENCES ImportedRows(CompanyId,StudyId,BatchId,RecordNumber) ON DELETE RESTRICT);
        INSERT INTO GroupedResourceSourceLinks SELECT ResourceId,CompanyId,StudyId,BatchId,RecordNumber FROM ResourceSourceLinks;
        DROP TABLE ResourceSourceLinks;
        ALTER TABLE GroupedResourceSourceLinks RENAME TO ResourceSourceLinks;
        CREATE UNIQUE INDEX IX_ResourceSourceLinks_CompanyId_StudyId_BatchId_RecordNumber ON ResourceSourceLinks(CompanyId,StudyId,BatchId,RecordNumber);
        CREATE INDEX IX_ResourceSourceLinks_CompanyId_StudyId_BatchId_ResourceId ON ResourceSourceLinks(CompanyId,StudyId,BatchId,ResourceId);
        CREATE TRIGGER ResourceSourceLinksImmutableUpdate BEFORE UPDATE ON ResourceSourceLinks BEGIN SELECT RAISE(ABORT,'Confirmed import is immutable'); END;
        CREATE TRIGGER ResourceSourceLinksImmutableDelete BEFORE DELETE ON ResourceSourceLinks BEGIN SELECT RAISE(ABORT,'Confirmed import is immutable'); END;
        """);
    protected override void Down(MigrationBuilder migration) => throw new NotSupportedException("Keep the original company file; downgrade is unsupported.");
}

[DbContext(typeof(GroupedImportDbContext))]
internal sealed class GroupedImportDbContextSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder model) => V5Model.Configure(model);
}
