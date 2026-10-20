using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Valdrio.Infrastructure.CompanyFiles.Migrations;

internal static class V12Model
{
    internal static void Configure(ModelBuilder model)
    {
        V11Model.Configure(model);
        model.Entity<CompositionCatalogRow>(b =>
        {
            b.ToTable("CompositionCatalogs");
            b.HasKey(x => x.CompanyId);
            b.Property(x => x.CompanyId).ValueGeneratedNever();
            b.Property(x => x.Revision);
            b.Property(x => x.CanonicalJson).IsRequired();
            b.Property(x => x.ContentHash).IsRequired();
            b.HasOne<CompanyRow>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
[DbContext(typeof(CompositionDbContext))]
[Migration(SqliteCompanyFileService.CompositionMigrationId)]
internal sealed class Compositions : Migration
{
    protected override void Up(MigrationBuilder migration) => migration.Sql("""
        CREATE TABLE CompositionCatalogs (
          CompanyId TEXT NOT NULL PRIMARY KEY, Revision INTEGER NOT NULL CHECK(Revision>=1),
          CanonicalJson TEXT NOT NULL, ContentHash TEXT NOT NULL,
          FOREIGN KEY(CompanyId) REFERENCES Companies(Id) ON DELETE RESTRICT);
        """);
    protected override void Down(MigrationBuilder migration) => throw new NotSupportedException();
}
[DbContext(typeof(CompositionDbContext))]
internal sealed class CompositionDbContextSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder model) => V12Model.Configure(model);
}
