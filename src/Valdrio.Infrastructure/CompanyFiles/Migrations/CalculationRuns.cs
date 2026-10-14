using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Valdrio.Infrastructure.CompanyFiles.Migrations;
internal static class V10Model
{
    internal static void Configure(ModelBuilder model)
    {
        V9Model.Configure(model);
        model.Entity<CalculationRunRow>(b =>
        {
            b.ToTable("CalculationRuns"); b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CompanyId).IsRequired(); b.Property(x => x.StudyId).IsRequired();
            b.Property(x => x.Status).IsRequired(); b.Property(x => x.CanonicalJson).IsRequired(); b.Property(x => x.ContentHash).IsRequired();
            b.HasIndex(x => new { x.CompanyId, x.StudyId });
            b.HasOne<StudyRow>().WithMany().HasForeignKey(x => new { x.CompanyId, x.StudyId })
                .HasPrincipalKey(x => new { x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
[DbContext(typeof(RunDbContext))]
[Migration(SqliteCompanyFileService.RunMigrationId)]
internal sealed class CalculationRuns : Migration
{
    protected override void Up(MigrationBuilder migration)
    {
        migration.Sql("""
            CREATE TABLE CalculationRuns (Id TEXT NOT NULL PRIMARY KEY, CompanyId TEXT NOT NULL,
                StudyId TEXT NOT NULL, Status TEXT NOT NULL CHECK(Status IN ('Started','Completed','Failed','Cancelled')),
                CanonicalJson TEXT NOT NULL, ContentHash TEXT NOT NULL,
                FOREIGN KEY(CompanyId,StudyId) REFERENCES Studies(CompanyId,Id) ON DELETE RESTRICT);
            CREATE INDEX IX_CalculationRuns_CompanyId_StudyId ON CalculationRuns(CompanyId,StudyId);
            CREATE TRIGGER CalculationRunsUpdate BEFORE UPDATE ON CalculationRuns WHEN OLD.Status<>'Started'
                BEGIN SELECT RAISE(ABORT,'Finished runs are immutable'); END;
            CREATE TRIGGER CalculationRunsDelete BEFORE DELETE ON CalculationRuns
                BEGIN SELECT RAISE(ABORT,'Run history is immutable'); END;
            """);
    }
    protected override void Down(MigrationBuilder migration) => throw new NotSupportedException();
}
[DbContext(typeof(RunDbContext))]
internal sealed class RunDbContextSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder model) => V10Model.Configure(model);
}
