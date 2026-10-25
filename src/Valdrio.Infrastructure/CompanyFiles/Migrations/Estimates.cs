using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Valdrio.Infrastructure.CompanyFiles.Migrations;
internal static class V13Model
{
    internal static void Configure(ModelBuilder model)
    {
        V12Model.Configure(model);
        model.Entity<EstimateRow>(b =>
        {
            b.ToTable("Estimates"); b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CompanyId).IsRequired(); b.Property(x => x.StudyId).IsRequired();
            b.Property(x => x.OrderId).IsRequired(); b.Property(x => x.Version);
            b.HasIndex(x => new { x.OrderId, x.Version }).IsUnique();
            b.HasIndex(x => x.CompanyId);
            b.Property(x => x.CanonicalJson).IsRequired(); b.Property(x => x.ContentHash).IsRequired();
            b.HasOne<CompanyRow>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
[DbContext(typeof(EstimateDbContext))]
[Migration(SqliteCompanyFileService.EstimateMigrationId)]
internal sealed class Estimates : Migration
{
    protected override void Up(MigrationBuilder migration) => migration.Sql("""
        CREATE TABLE Estimates (
          Id TEXT NOT NULL PRIMARY KEY, CompanyId TEXT NOT NULL, StudyId TEXT NOT NULL,
          OrderId TEXT NOT NULL, Version INTEGER NOT NULL CHECK(Version>=1),
          CanonicalJson TEXT NOT NULL, ContentHash TEXT NOT NULL,
          FOREIGN KEY(CompanyId) REFERENCES Companies(Id) ON DELETE RESTRICT);
        CREATE UNIQUE INDEX IX_Estimates_OrderId_Version ON Estimates(OrderId, Version);
        CREATE INDEX IX_Estimates_CompanyId ON Estimates(CompanyId);
        """);
    protected override void Down(MigrationBuilder migration) => throw new NotSupportedException();
}
[DbContext(typeof(EstimateDbContext))]
internal sealed class EstimateDbContextSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder model) => V13Model.Configure(model);
}
