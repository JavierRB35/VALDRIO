using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Valdrio.Infrastructure.CompanyFiles.Migrations;
internal static class V7Model
{
    internal static void Configure(ModelBuilder model) => V6Model.Configure(model);
}
[DbContext(typeof(InternationalImportDbContext))]
[Migration(SqliteCompanyFileService.InternationalImportMigrationId)]
internal sealed class InternationalImports : Migration
{
    // Economic payloads now accept frozen conversion bridges; no historical row is rewritten.
    protected override void Up(MigrationBuilder migration) { }
    protected override void Down(MigrationBuilder migration) => throw new NotSupportedException();
}
[DbContext(typeof(InternationalImportDbContext))]
internal sealed class InternationalImportDbContextSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder model) => V7Model.Configure(model);
}
