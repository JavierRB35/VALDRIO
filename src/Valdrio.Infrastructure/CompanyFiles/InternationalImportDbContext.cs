using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Valdrio.Infrastructure.CompanyFiles.Migrations;
namespace Valdrio.Infrastructure.CompanyFiles;
internal sealed class InternationalImportDbContext(SqliteConnection connection) : ImportDbContext(connection)
{
    protected override void OnModelCreating(ModelBuilder model) => V7Model.Configure(model);
}
