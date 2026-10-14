using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Valdrio.Infrastructure.CompanyFiles.Migrations;

namespace Valdrio.Infrastructure.CompanyFiles;

// V10 remains frozen: the last model alone owns new migrations and data.
internal sealed class PhysicalDbContext(SqliteConnection connection) : RunDbContext(connection)
{
    public DbSet<PhysicalObservationRow> PhysicalObservations => Set<PhysicalObservationRow>();
    protected override void OnModelCreating(ModelBuilder model) => V11Model.Configure(model);
}

internal sealed class PhysicalObservationRow
{
    public string Id { get; set; } = "";
    public string CompanyId { get; set; } = "";
    public string StudyId { get; set; } = "";
    public string ModelVersionId { get; set; } = "";
    public string NodeId { get; set; } = "";
    public string PoolId { get; set; } = "";
    public string UnitId { get; set; } = "";
    public string PeriodStart { get; set; } = "";
    public string PeriodEnd { get; set; } = "";
    public long Version { get; set; }
    public string State { get; set; } = "";
    public string CanonicalJson { get; set; } = "";
    public string ContentHash { get; set; } = "";
}
