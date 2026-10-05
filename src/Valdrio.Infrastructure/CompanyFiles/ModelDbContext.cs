using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Valdrio.Infrastructure.CompanyFiles.Migrations;

namespace Valdrio.Infrastructure.CompanyFiles;
// The v1 context and its historical snapshot remain unchanged.
internal class ModelDbContext(SqliteConnection connection) : DbContext
{
    public DbSet<FileMetadataRow> Metadata => Set<FileMetadataRow>();
    public DbSet<CompanyRow> Companies => Set<CompanyRow>();
    public DbSet<PolicyRow> Policies => Set<PolicyRow>();
    public DbSet<UnitRow> Units => Set<UnitRow>();
    public DbSet<AuditRow> Audit => Set<AuditRow>();
    public DbSet<UnitStateRow> UnitStates => Set<UnitStateRow>();
    public DbSet<PhysicalCenterRow> Centers => Set<PhysicalCenterRow>();
    public DbSet<ModelFamilyRow> Families => Set<ModelFamilyRow>();
    public DbSet<ModelVersionRow> Models => Set<ModelVersionRow>();
    public DbSet<NodeRow> Nodes => Set<NodeRow>();
    public DbSet<PoolRow> Pools => Set<PoolRow>();
    public DbSet<PublicationRow> Publications => Set<PublicationRow>();

    protected override void OnConfiguring(DbContextOptionsBuilder options) => options.UseSqlite(connection);
    protected override void OnModelCreating(ModelBuilder model) => V2Model.Configure(model);
}

internal sealed class UnitStateRow
{
    public string CompanyId { get; set; } = "";
    public string UnitId { get; set; } = "";
    public bool IsActive { get; set; }
    public long Revision { get; set; }
}

internal sealed class PhysicalCenterRow
{
    public string Id { get; set; } = "";
    public string CompanyId { get; set; } = "";
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
    public long Revision { get; set; }
}

internal sealed class ModelFamilyRow
{
    public string Id { get; set; } = "";
    public string CompanyId { get; set; } = "";
}

internal sealed class ModelVersionRow
{
    public string Id { get; set; } = "";
    public string CompanyId { get; set; } = "";
    public string FamilyId { get; set; } = "";
    public int VersionNumber { get; set; }
    public string? ParentVersionId { get; set; }
    public string Description { get; set; } = "";
    public string Status { get; set; } = "";
    public long Revision { get; set; }
    public string? PublishedAtUtc { get; set; }
    public string PrecisionPolicyVersion { get; set; } = "";
    public string MethodPolicyJson { get; set; } = "";
}

internal sealed class NodeRow
{
    public string Id { get; set; } = "";
    public string CompanyId { get; set; } = "";
    public string ModelVersionId { get; set; } = "";
    public string PhysicalCenterId { get; set; } = "";
    public string Code { get; set; } = "";
    public string Role { get; set; } = "";
    public string FunctionTagsJson { get; set; } = "";
    public bool IsActive { get; set; }
}

internal sealed class PoolRow
{
    public string Id { get; set; } = "";
    public string CompanyId { get; set; } = "";
    public string ModelVersionId { get; set; } = "";
    public string NodeId { get; set; } = "";
    public string DriverUnitId { get; set; } = "";
    public string Description { get; set; } = "";
    public string CostLayer { get; set; } = "";
    public string CapacityPolicyJson { get; set; } = "";
}

internal sealed class PublicationRow
{
    public string ModelVersionId { get; set; } = "";
    public string PayloadSchema { get; set; } = "";
    public string CanonicalJson { get; set; } = "";
    public string StructuralHash { get; set; } = "";
}
