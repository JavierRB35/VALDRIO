using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Valdrio.Domain;
using Valdrio.Infrastructure.CompanyFiles.Migrations;

namespace Valdrio.Infrastructure.CompanyFiles;

public sealed class CanonicalDecimalConverter() : ValueConverter<decimal, string>(
    value => CanonicalDecimal.Format(value), value => CanonicalDecimal.Parse(value));

internal sealed class CompanyDbContext(SqliteConnection connection) : DbContext
{
    public DbSet<FileMetadataRow> Metadata => Set<FileMetadataRow>();
    public DbSet<CompanyRow> Companies => Set<CompanyRow>();
    public DbSet<PolicyRow> Policies => Set<PolicyRow>();
    public DbSet<UnitRow> Units => Set<UnitRow>();
    public DbSet<AuditRow> Audit => Set<AuditRow>();
    protected override void OnConfiguring(DbContextOptionsBuilder options) => options.UseSqlite(connection);
    protected override void OnModelCreating(ModelBuilder model) => V1Model.Configure(model);
}

internal sealed class FileMetadataRow
{
    public int Slot { get; set; }
    public string Format { get; set; } = "";
    public int SchemaVersion { get; set; }
    public string CatalogVersion { get; set; } = "";
}
internal sealed class CompanyRow
{
    public string Id { get; set; } = "";
    public int Slot { get; set; }
    public string Name { get; set; } = "";
    public string Currency { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public long Revision { get; set; }
}
internal sealed class PolicyRow
{
    public string CompanyId { get; set; } = "";
    public string Version { get; set; } = "";
    public int AmountScale { get; set; }
    public int QuantityScale { get; set; }
    public int PercentageScale { get; set; }
    public decimal MaximumInitialMagnitude { get; set; }
    public decimal MaximumNumericCorrection { get; set; }
}
internal sealed class UnitRow
{
    public string Id { get; set; } = "";
    public string CompanyId { get; set; } = "";
    public string Code { get; set; } = "";
    public string Dimension { get; set; } = "";
    public string Symbol { get; set; } = "";
    public int DecimalScale { get; set; }
    public bool IsDiscrete { get; set; }
}
internal sealed class AuditRow
{
    public string Id { get; set; } = "";
    public string CompanyId { get; set; } = "";
    public long Revision { get; set; }
    public string Operation { get; set; } = "";
    public string Actor { get; set; } = "";
    public string AtUtc { get; set; } = "";
    public string BeforeJson { get; set; } = "";
    public string AfterJson { get; set; } = "";
}
