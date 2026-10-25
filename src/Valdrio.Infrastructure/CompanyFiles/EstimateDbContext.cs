using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Valdrio.Infrastructure.CompanyFiles.Migrations;
namespace Valdrio.Infrastructure.CompanyFiles;
internal sealed class EstimateDbContext(SqliteConnection connection) : CompositionDbContext(connection)
{
    public DbSet<EstimateRow> Estimates => Set<EstimateRow>();
    protected override void OnModelCreating(ModelBuilder model) => V13Model.Configure(model);
}
internal sealed class EstimateRow
{
    public string Id { get; set; } = "";
    public string CompanyId { get; set; } = "";
    public string StudyId { get; set; } = "";
    public string OrderId { get; set; } = "";
    public int Version { get; set; }
    public string CanonicalJson { get; set; } = "";
    public string ContentHash { get; set; } = "";
}
