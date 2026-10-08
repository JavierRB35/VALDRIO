using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Valdrio.Infrastructure.CompanyFiles.Migrations;

internal static class V6Model
{
    internal static void Configure(ModelBuilder model)
    {
        V5Model.Configure(model);
        model.Entity<ImportBatchRow>().HasIndex(x => new { x.CompanyId, x.StudyId, x.DuplicateFingerprint }).IsUnique(false);
    }
}
[DbContext(typeof(ImportLedgerDbContext))]
[Migration(SqliteCompanyFileService.ImportLedgerMigrationId)]
internal sealed class ImportLedger : Migration
{
    protected override void Up(MigrationBuilder migration) => migration.Sql("""
        DROP INDEX IX_ImportBatches_CompanyId_StudyId_DuplicateFingerprint;
        CREATE INDEX IX_ImportBatches_CompanyId_StudyId_DuplicateFingerprint ON ImportBatches(CompanyId,StudyId,DuplicateFingerprint);
        """);
    protected override void Down(MigrationBuilder migration) => throw new NotSupportedException("Keep the original company file; downgrade is unsupported.");
}
[DbContext(typeof(ImportLedgerDbContext))]
internal sealed class ImportLedgerDbContextSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder model) => V6Model.Configure(model);
}
