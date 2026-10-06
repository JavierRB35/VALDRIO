using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Valdrio.Infrastructure.CompanyFiles.Migrations;

namespace Valdrio.Infrastructure.CompanyFiles;

// v1/v2 mappings and snapshots stay frozen; only this context owns the v3 migration.
internal sealed class StudyDbContext(SqliteConnection connection) : ModelDbContext(connection)
{
    public DbSet<StudyRow> Studies => Set<StudyRow>();
    public DbSet<StudySnapshotRow> StudySnapshots => Set<StudySnapshotRow>();
    protected override void OnModelCreating(ModelBuilder model) => V3Model.Configure(model);
}

internal sealed class StudyRow
{
    public string Id { get; set; } = "";
    public string CompanyId { get; set; } = "";
    public string SelectedModelVersionId { get; set; } = "";
    public string PeriodStart { get; set; } = "";
    public string PeriodEnd { get; set; } = "";
    public string FunctionalCurrencyCode { get; set; } = "";
    public string State { get; set; } = "";
    public long Revision { get; set; }
    public string? CurrentRunId { get; set; }
    public string ContextJson { get; set; } = "";
}

internal sealed class StudySnapshotRow
{
    public string Id { get; set; } = "";
    public string CompanyId { get; set; } = "";
    public string StudyId { get; set; } = "";
    public long InputRevision { get; set; }
    public string CapturedAtUtc { get; set; } = "";
    public string PayloadSchema { get; set; } = "";
    public string EconomicInputHash { get; set; } = "";
    public string FullSnapshotHash { get; set; } = "";
    public string CanonicalJson { get; set; } = "";
}
