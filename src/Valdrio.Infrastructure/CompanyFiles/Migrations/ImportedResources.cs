using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Valdrio.Infrastructure.CompanyFiles.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202610060004_ImportedResources")]
internal sealed class ImportedResources : Migration
{
    protected override void BuildTargetModel(ModelBuilder model) => V4Model.Configure(model);
    protected override void Up(MigrationBuilder m)
    {
        m.Sql("""
            CREATE TABLE ImportBatches (Id TEXT NOT NULL PRIMARY KEY, CompanyId TEXT NOT NULL, StudyId TEXT NOT NULL,
              ExpectedRevision INTEGER NOT NULL, ConfirmedRevision INTEGER NOT NULL, FileHash TEXT NOT NULL,
              PreparedFingerprint TEXT NOT NULL, DuplicateFingerprint TEXT NOT NULL, CanonicalJson TEXT NOT NULL,
              UNIQUE(CompanyId,StudyId,Id), CONSTRAINT CK_ImportRevision CHECK(ExpectedRevision>=0 AND ConfirmedRevision=ExpectedRevision+1),
              FOREIGN KEY(CompanyId,StudyId) REFERENCES Studies(CompanyId,Id) ON DELETE RESTRICT);
            CREATE UNIQUE INDEX IX_ImportBatches_CompanyId_StudyId_DuplicateFingerprint ON ImportBatches(CompanyId,StudyId,DuplicateFingerprint);
            CREATE UNIQUE INDEX IX_ImportBatches_CompanyId_StudyId_ConfirmedRevision ON ImportBatches(CompanyId,StudyId,ConfirmedRevision);
            CREATE TABLE ImportedRows (CompanyId TEXT NOT NULL, StudyId TEXT NOT NULL, BatchId TEXT NOT NULL,
              RecordNumber INTEGER NOT NULL CONSTRAINT CK_ImportRecord CHECK(RecordNumber>=1), RawJson TEXT NOT NULL,
              Account TEXT NULL, Amount TEXT NULL, Currency TEXT NULL, Selected INTEGER NOT NULL,
              PRIMARY KEY(CompanyId,StudyId,BatchId,RecordNumber),
              FOREIGN KEY(CompanyId,StudyId,BatchId) REFERENCES ImportBatches(CompanyId,StudyId,Id) ON DELETE RESTRICT);
            CREATE TABLE Resources (Id TEXT NOT NULL PRIMARY KEY, CompanyId TEXT NOT NULL, StudyId TEXT NOT NULL,
              BatchId TEXT NOT NULL, Account TEXT NOT NULL, OriginalAmount TEXT NOT NULL, FunctionalAmount TEXT NOT NULL,
              Currency TEXT NOT NULL, PeriodStart TEXT NOT NULL, PeriodEnd TEXT NOT NULL, UNIQUE(CompanyId,StudyId,BatchId,Id),
              FOREIGN KEY(CompanyId,StudyId,BatchId) REFERENCES ImportBatches(CompanyId,StudyId,Id) ON DELETE RESTRICT);
            CREATE TABLE ResourceSourceLinks (ResourceId TEXT NOT NULL PRIMARY KEY, CompanyId TEXT NOT NULL,
              StudyId TEXT NOT NULL, BatchId TEXT NOT NULL, RecordNumber INTEGER NOT NULL,
              FOREIGN KEY(CompanyId,StudyId,BatchId,ResourceId) REFERENCES Resources(CompanyId,StudyId,BatchId,Id) ON DELETE RESTRICT,
              FOREIGN KEY(CompanyId,StudyId,BatchId,RecordNumber) REFERENCES ImportedRows(CompanyId,StudyId,BatchId,RecordNumber) ON DELETE RESTRICT);
            CREATE UNIQUE INDEX IX_ResourceSourceLinks_CompanyId_StudyId_BatchId_RecordNumber ON ResourceSourceLinks(CompanyId,StudyId,BatchId,RecordNumber);
            CREATE INDEX IX_ResourceSourceLinks_CompanyId_StudyId_BatchId_ResourceId ON ResourceSourceLinks(CompanyId,StudyId,BatchId,ResourceId);
            """);
        foreach (var table in new[] { "ImportBatches", "ImportedRows", "Resources", "ResourceSourceLinks" })
        {
            m.Sql($"CREATE TRIGGER {table}ImmutableUpdate BEFORE UPDATE ON {table} BEGIN SELECT RAISE(ABORT,'Confirmed import is immutable'); END;");
            m.Sql($"CREATE TRIGGER {table}ImmutableDelete BEFORE DELETE ON {table} BEGIN SELECT RAISE(ABORT,'Confirmed import is immutable'); END;");
        }
    }
    protected override void Down(MigrationBuilder m) => throw new NotSupportedException("Keep the original company file; downgrade is unsupported.");
}
[DbContext(typeof(ImportDbContext))]
internal sealed class ImportDbContextSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder model) => V4Model.Configure(model);
}
