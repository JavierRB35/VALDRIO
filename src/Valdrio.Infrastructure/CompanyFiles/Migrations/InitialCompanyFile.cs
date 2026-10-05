using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Valdrio.Infrastructure.CompanyFiles.Migrations;

[DbContext(typeof(CompanyDbContext))]
[Migration("202610050001_InitialCompanyFile")]
internal sealed class InitialCompanyFile : Migration
{
    public InitialCompanyFile() { }
    protected override void BuildTargetModel(ModelBuilder model) => V1Model.Configure(model);
    protected override void Up(MigrationBuilder m) => m.Sql("""
        CREATE TABLE FileMetadata (Slot INTEGER NOT NULL PRIMARY KEY CHECK(Slot = 1), Format TEXT NOT NULL, SchemaVersion INTEGER NOT NULL, CatalogVersion TEXT NOT NULL);
        CREATE TABLE Companies (Id TEXT NOT NULL PRIMARY KEY, Slot INTEGER NOT NULL CHECK(Slot = 1), Name TEXT NOT NULL, Currency TEXT NOT NULL, CreatedAt TEXT NOT NULL, Revision INTEGER NOT NULL CHECK(Revision >= 0));
        CREATE UNIQUE INDEX IX_Companies_Slot ON Companies(Slot);
        CREATE TABLE PrecisionPolicies (CompanyId TEXT NOT NULL PRIMARY KEY REFERENCES Companies(Id) ON DELETE RESTRICT, Version TEXT NOT NULL, AmountScale INTEGER NOT NULL, QuantityScale INTEGER NOT NULL, PercentageScale INTEGER NOT NULL, MaximumInitialMagnitude TEXT NOT NULL, MaximumNumericCorrection TEXT NOT NULL);
        CREATE TABLE Units (Id TEXT NOT NULL PRIMARY KEY, CompanyId TEXT NOT NULL REFERENCES Companies(Id) ON DELETE RESTRICT, Code TEXT COLLATE BINARY NOT NULL, Dimension TEXT NOT NULL, Symbol TEXT NOT NULL, DecimalScale INTEGER NOT NULL CHECK(DecimalScale BETWEEN 0 AND 12), IsDiscrete INTEGER NOT NULL CHECK(IsDiscrete IN(0,1)));
        CREATE UNIQUE INDEX IX_Units_CompanyId_Code ON Units(CompanyId, Code);
        CREATE TABLE AuditEvents (Id TEXT NOT NULL PRIMARY KEY, CompanyId TEXT NOT NULL REFERENCES Companies(Id) ON DELETE RESTRICT, Revision INTEGER NOT NULL, Operation TEXT NOT NULL, Actor TEXT NOT NULL, AtUtc TEXT NOT NULL, BeforeJson TEXT NOT NULL, AfterJson TEXT NOT NULL);
        CREATE UNIQUE INDEX IX_AuditEvents_CompanyId_Revision ON AuditEvents(CompanyId, Revision);
        """);
    protected override void Down(MigrationBuilder m) => m.Sql("""
        DROP TABLE AuditEvents; DROP TABLE Units; DROP TABLE PrecisionPolicies; DROP TABLE Companies; DROP TABLE FileMetadata;
        """);
}

[DbContext(typeof(CompanyDbContext))]
internal sealed class CompanyDbContextModelSnapshot : ModelSnapshot
{
    public CompanyDbContextModelSnapshot() { }
    protected override void BuildModel(ModelBuilder model) => V1Model.Configure(model);
}
