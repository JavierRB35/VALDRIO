using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Valdrio.Infrastructure.CompanyFiles.Migrations;

namespace Valdrio.Infrastructure.CompanyFiles;

internal sealed class GroupedImportDbContext(SqliteConnection connection) : ImportDbContext(connection)
{
    protected override void OnModelCreating(ModelBuilder model) => V5Model.Configure(model);
}
