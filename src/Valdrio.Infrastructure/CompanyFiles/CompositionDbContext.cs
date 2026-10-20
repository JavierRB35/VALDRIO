using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Valdrio.Infrastructure.CompanyFiles.Migrations;
namespace Valdrio.Infrastructure.CompanyFiles;

internal sealed class CompositionDbContext(SqliteConnection connection) : PhysicalDbContext(connection)
{
    public DbSet<CompositionCatalogRow> CompositionCatalogs => Set<CompositionCatalogRow>();
    protected override void OnModelCreating(ModelBuilder model) => V12Model.Configure(model);
}
internal sealed class CompositionCatalogRow
{
    public string CompanyId { get; set; } = "";
    public long Revision { get; set; }
    public string CanonicalJson { get; set; } = "";
    public string ContentHash { get; set; } = "";
}
