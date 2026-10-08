using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Valdrio.Infrastructure.CompanyFiles.Migrations;
namespace Valdrio.Infrastructure.CompanyFiles;
internal sealed class AllocationDbContext(SqliteConnection connection) : ResourceDbContext(connection)
{
    public DbSet<AllocationKeyEventRow> AllocationKeyEvents => Set<AllocationKeyEventRow>();
    public DbSet<AllocationChangeRow> AllocationChanges => Set<AllocationChangeRow>();
    protected override void OnModelCreating(ModelBuilder model) => V9Model.Configure(model);
}
internal sealed class AllocationKeyEventRow
{
    public string Id { get; set; } = "";
    public string CompanyId { get; set; } = "";
    public string ModelVersionId { get; set; } = "";
    public string KeyId { get; set; } = "";
    public long Revision { get; set; }
    public string CanonicalJson { get; set; } = "";
}
internal sealed class AllocationChangeRow
{
    public string Id { get; set; } = "";
    public string CompanyId { get; set; } = "";
    public string StudyId { get; set; } = "";
    public long ExpectedRevision { get; set; }
    public long ConfirmedRevision { get; set; }
    public string CanonicalJson { get; set; } = "";
}
