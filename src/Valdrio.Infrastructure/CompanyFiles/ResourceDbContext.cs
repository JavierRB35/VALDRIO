using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Valdrio.Infrastructure.CompanyFiles.Migrations;
namespace Valdrio.Infrastructure.CompanyFiles;
internal class ResourceDbContext(SqliteConnection connection) : ImportDbContext(connection)
{
    public DbSet<ResourceChangeRow> ResourceChanges => Set<ResourceChangeRow>();
    protected override void OnModelCreating(ModelBuilder model) => V8Model.Configure(model);
}
internal sealed class ResourceChangeRow
{
    public string Id { get; set; } = "";
    public string CompanyId { get; set; } = "";
    public string StudyId { get; set; } = "";
    public long ExpectedRevision { get; set; }
    public long ConfirmedRevision { get; set; }
    public string CanonicalJson { get; set; } = "";
}
