using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Infrastructure.CompanyFiles;
namespace Valdrio.Tests;
public sealed partial class T06ImportConfirmationTests
{
    [TestMethod]
    public void T08MappingAndMigrationSnapshotHaveNoPendingChanges()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open(); using var context = new AllocationDbContext(connection);
        var snapshot = context.GetService<IMigrationsAssembly>().ModelSnapshot!.Model;
        var initialized = context.GetService<IModelRuntimeInitializer>().Initialize(snapshot, designTime: true);
        var changes = context.GetService<IMigrationsModelDiffer>().GetDifferences(initialized.GetRelationalModel(),
            context.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model.GetRelationalModel());
        Assert.AreEqual(0, changes.Count, string.Join("\n", changes.Select(c => c switch
        {
            AlterColumnOperation a => a.Table + "." + a.Name + " nullable " + a.OldColumn.IsNullable + "→" + a.IsNullable,
            AddColumnOperation a => "add " + a.Table + "." + a.Name,
            CreateIndexOperation a => "index " + a.Table + "." + a.Name,
            _ => c.GetType().Name
        })));
        Assert.IsFalse(context.Database.HasPendingModelChanges());
    }
}
