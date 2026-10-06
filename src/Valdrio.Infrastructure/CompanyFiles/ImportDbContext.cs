using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Valdrio.Infrastructure.CompanyFiles.Migrations;

namespace Valdrio.Infrastructure.CompanyFiles;

internal sealed class ImportDbContext(SqliteConnection connection) : StudyDbContext(connection)
{
    public DbSet<ImportBatchRow> ImportBatches => Set<ImportBatchRow>();
    public DbSet<ImportedRow> ImportedRows => Set<ImportedRow>();
    public DbSet<ResourceRow> Resources => Set<ResourceRow>();
    public DbSet<ResourceSourceLinkRow> ResourceSourceLinks => Set<ResourceSourceLinkRow>();
    protected override void OnModelCreating(ModelBuilder model) => V4Model.Configure(model);
}
internal sealed class ImportBatchRow
{
    public string Id { get; set; } = "";
    public string CompanyId { get; set; } = "";
    public string StudyId { get; set; } = "";
    public long ExpectedRevision { get; set; }
    public long ConfirmedRevision { get; set; }
    public string FileHash { get; set; } = "";
    public string PreparedFingerprint { get; set; } = "";
    public string DuplicateFingerprint { get; set; } = "";
    public string CanonicalJson { get; set; } = "";
}
internal sealed class ImportedRow
{
    public string CompanyId { get; set; } = "";
    public string StudyId { get; set; } = "";
    public string BatchId { get; set; } = "";
    public int RecordNumber { get; set; }
    public string RawJson { get; set; } = "";
    public string? Account { get; set; }
    public string? Amount { get; set; }
    public string? Currency { get; set; }
    public bool Selected { get; set; }
}
internal sealed class ResourceRow
{
    public string Id { get; set; } = "";
    public string CompanyId { get; set; } = "";
    public string StudyId { get; set; } = "";
    public string BatchId { get; set; } = "";
    public string Account { get; set; } = "";
    public string OriginalAmount { get; set; } = "";
    public string FunctionalAmount { get; set; } = "";
    public string Currency { get; set; } = "";
    public string PeriodStart { get; set; } = "";
    public string PeriodEnd { get; set; } = "";
}
internal sealed class ResourceSourceLinkRow
{
    public string ResourceId { get; set; } = "";
    public string CompanyId { get; set; } = "";
    public string StudyId { get; set; } = "";
    public string BatchId { get; set; } = "";
    public int RecordNumber { get; set; }
}
