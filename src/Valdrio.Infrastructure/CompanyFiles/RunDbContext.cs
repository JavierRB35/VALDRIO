using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Valdrio.Infrastructure.CompanyFiles.Migrations;
namespace Valdrio.Infrastructure.CompanyFiles;
internal class RunDbContext(SqliteConnection connection) : AllocationDbContext(connection)
{
    public DbSet<CalculationRunRow> Runs => Set<CalculationRunRow>();
    protected override void OnModelCreating(ModelBuilder model) => V10Model.Configure(model);
}
internal sealed class CalculationRunRow
{
    public string Id { get; set; } = "";
    public string CompanyId { get; set; } = "";
    public string StudyId { get; set; } = "";
    public string Status { get; set; } = "";
    public string CanonicalJson { get; set; } = "";
    public string ContentHash { get; set; } = "";
}
