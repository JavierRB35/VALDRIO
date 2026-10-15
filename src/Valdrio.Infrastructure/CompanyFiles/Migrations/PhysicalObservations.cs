using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Valdrio.Infrastructure.CompanyFiles.Migrations;

internal static class V11Model
{
    internal static void Configure(ModelBuilder model)
    {
        V10Model.Configure(model);
        model.Entity<PhysicalObservationRow>(b =>
        {
            b.ToTable("PhysicalObservations");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CompanyId).IsRequired();
            b.Property(x => x.StudyId).IsRequired();
            b.Property(x => x.ModelVersionId).IsRequired();
            b.Property(x => x.NodeId).IsRequired();
            b.Property(x => x.PoolId).IsRequired();
            b.Property(x => x.UnitId).IsRequired();
            b.Property(x => x.PeriodStart).IsRequired();
            b.Property(x => x.PeriodEnd).IsRequired();
            b.Property(x => x.State).IsRequired();
            b.Property(x => x.CanonicalJson).IsRequired();
            b.Property(x => x.ContentHash).IsRequired();
            b.HasIndex(x => new { x.CompanyId, x.StudyId, x.ModelVersionId, x.PoolId,
                x.PeriodStart, x.PeriodEnd, x.Version }).IsUnique();
            b.HasOne<StudyRow>().WithMany().HasForeignKey(x => new { x.CompanyId, x.StudyId })
                .HasPrincipalKey(x => new { x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

[DbContext(typeof(PhysicalDbContext))]
[Migration(SqliteCompanyFileService.PhysicalMigrationId)]
internal sealed class PhysicalObservations : Migration
{
    protected override void Up(MigrationBuilder migration)
    {
        migration.Sql("""
            CREATE TABLE PhysicalObservations (
              Id TEXT NOT NULL PRIMARY KEY, CompanyId TEXT NOT NULL, StudyId TEXT NOT NULL,
              ModelVersionId TEXT NOT NULL, NodeId TEXT NOT NULL, PoolId TEXT NOT NULL,
              UnitId TEXT NOT NULL, PeriodStart TEXT NOT NULL, PeriodEnd TEXT NOT NULL,
              Version INTEGER NOT NULL CHECK(Version >= 1),
              State TEXT NOT NULL CHECK(State IN ('Draft','Confirmed','Annulled')),
              CanonicalJson TEXT NOT NULL, ContentHash TEXT NOT NULL,
              CHECK(length(PeriodStart)=10 AND length(PeriodEnd)=10 AND PeriodStart<=PeriodEnd),
              FOREIGN KEY(CompanyId,StudyId) REFERENCES Studies(CompanyId,Id) ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX IX_PhysicalObservations_Scope_Version ON PhysicalObservations
              (CompanyId,StudyId,ModelVersionId,PoolId,PeriodStart,PeriodEnd,Version);
            CREATE TRIGGER PhysicalObservationImmutableUpdate BEFORE UPDATE ON PhysicalObservations
              BEGIN SELECT RAISE(ABORT,'Physical observations are immutable'); END;
            CREATE TRIGGER PhysicalObservationImmutableDelete BEFORE DELETE ON PhysicalObservations
              BEGIN SELECT RAISE(ABORT,'Physical observations are immutable'); END;
            """);
    }
    protected override void Down(MigrationBuilder migration) => throw new NotSupportedException();
}

[DbContext(typeof(PhysicalDbContext))]
internal sealed class PhysicalDbContextSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder model) => V11Model.Configure(model);
}
