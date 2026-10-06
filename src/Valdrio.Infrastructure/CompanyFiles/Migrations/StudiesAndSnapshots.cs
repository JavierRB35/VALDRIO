using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Valdrio.Infrastructure.CompanyFiles.Migrations;

[DbContext(typeof(StudyDbContext))]
[Migration("202610050003_StudiesAndSnapshots")]
internal sealed class StudiesAndSnapshots : Migration
{
    public StudiesAndSnapshots() { }
    protected override void BuildTargetModel(ModelBuilder model) => V3Model.Configure(model);
    protected override void Up(MigrationBuilder m)
    {
        m.Sql("""
            CREATE TABLE Studies (Id TEXT NOT NULL PRIMARY KEY, CompanyId TEXT NOT NULL, SelectedModelVersionId TEXT NOT NULL,
              PeriodStart TEXT NOT NULL, PeriodEnd TEXT NOT NULL, FunctionalCurrencyCode TEXT NOT NULL, State TEXT NOT NULL,
              Revision INTEGER NOT NULL, CurrentRunId TEXT NULL, ContextJson TEXT NOT NULL, UNIQUE(CompanyId,Id),
              CONSTRAINT CK_StudyRevision CHECK(Revision>=0), CONSTRAINT CK_StudyState CHECK(State IN('Open','Closed')),
              CONSTRAINT CK_StudyPeriod CHECK(length(PeriodStart)=10 AND length(PeriodEnd)=10 AND PeriodStart<=PeriodEnd),
              CONSTRAINT CK_StudyNoRun CHECK(CurrentRunId IS NULL),
              FOREIGN KEY(CompanyId,SelectedModelVersionId) REFERENCES ModelVersions(CompanyId,Id) ON DELETE RESTRICT);
            CREATE INDEX IX_Studies_CompanyId_SelectedModelVersionId ON Studies(CompanyId,SelectedModelVersionId);
            CREATE TABLE StudySnapshots (Id TEXT NOT NULL PRIMARY KEY, CompanyId TEXT NOT NULL, StudyId TEXT NOT NULL,
              InputRevision INTEGER NOT NULL CONSTRAINT CK_SnapshotRevision CHECK(InputRevision>=0), CapturedAtUtc TEXT NOT NULL,
              PayloadSchema TEXT NOT NULL, EconomicInputHash TEXT NOT NULL, FullSnapshotHash TEXT NOT NULL, CanonicalJson TEXT NOT NULL,
              FOREIGN KEY(CompanyId,StudyId) REFERENCES Studies(CompanyId,Id) ON DELETE RESTRICT);
            CREATE INDEX IX_StudySnapshots_CompanyId_StudyId ON StudySnapshots(CompanyId,StudyId);
            CREATE TRIGGER StudyRequiresPublicationInsert BEFORE INSERT ON Studies BEGIN
              SELECT CASE WHEN NOT EXISTS(SELECT 1 FROM ModelVersions m JOIN ModelPublications p ON p.ModelVersionId=m.Id
                WHERE m.Id=NEW.SelectedModelVersionId AND m.CompanyId=NEW.CompanyId AND m.Status='Published') THEN RAISE(ABORT,'Published model required') END;
            END;
            CREATE TRIGGER StudyRequiresPublicationUpdate BEFORE UPDATE ON Studies BEGIN
              SELECT CASE WHEN NOT EXISTS(SELECT 1 FROM ModelVersions m JOIN ModelPublications p ON p.ModelVersionId=m.Id
                WHERE m.Id=NEW.SelectedModelVersionId AND m.CompanyId=NEW.CompanyId AND m.Status='Published') THEN RAISE(ABORT,'Published model required') END;
              SELECT CASE WHEN NEW.Id<>OLD.Id OR NEW.CompanyId<>OLD.CompanyId THEN RAISE(ABORT,'Study identity is immutable') END;
            END;
            CREATE TRIGGER StudySnapshotUpdate BEFORE UPDATE ON StudySnapshots BEGIN SELECT RAISE(ABORT,'Study snapshot is immutable'); END;
            CREATE TRIGGER StudySnapshotDelete BEFORE DELETE ON StudySnapshots BEGIN SELECT RAISE(ABORT,'Study snapshot is immutable'); END;
            """);
    }
    protected override void Down(MigrationBuilder m) => throw new NotSupportedException("Keep the original company file; downgrade is unsupported.");
}

[DbContext(typeof(StudyDbContext))]
internal sealed class StudyDbContextSnapshot : ModelSnapshot
{
    public StudyDbContextSnapshot() { }
    protected override void BuildModel(ModelBuilder model) => V3Model.Configure(model);
}
