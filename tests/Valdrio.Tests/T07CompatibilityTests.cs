using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Infrastructure.CompanyFiles;
namespace Valdrio.Tests;

public sealed partial class T06ImportConfirmationTests
{
    [TestMethod]
    public void T07_GenuineV7UpgradePreservesImportsAndSnapshotsAndUsesFrozenSchema()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "t06a-v7.json")));
        using var input = new GZipStream(new MemoryStream(Convert.FromBase64String(doc.RootElement.GetProperty("GzipBase64").GetString()!)), CompressionMode.Decompress);
        var source = FileOf("v7.valdrio"); using (var output = File.Create(source)) input.CopyTo(output);
        var hash = Hash(source); Assert.AreEqual(doc.RootElement.GetProperty("Sha256").GetString(), hash);
        var importJson = Query(source, "SELECT group_concat(CanonicalJson) FROM ImportBatches ORDER BY Id");
        var snapshotJson = Query(source, "SELECT group_concat(CanonicalJson) FROM StudySnapshots ORDER BY Id");
        Assert.AreEqual("FILE_UPGRADE_REQUIRED", Assert.ThrowsExactly<CompanyFileException>(() => Service().Open(source)).Diagnostic.Code);
        using var file = Service().Upgrade(source, FileOf("upgraded.valdrio"), "test");
        Assert.AreEqual(hash, Hash(source)); Assert.AreEqual(9, file.Read().SchemaVersion);
        Assert.AreEqual(importJson, Query(file.Path, "SELECT group_concat(CanonicalJson) FROM ImportBatches ORDER BY Id"));
        Assert.AreEqual(snapshotJson, Query(file.Path, "SELECT group_concat(CanonicalJson) FROM StudySnapshots ORDER BY Id"));
        var study = file.Studies.ReadStudies().Single();
        Assert.AreEqual(358.35m, file.Resources.ReadResources(study.CompanyId, study.Id).Preparation.Coverage.Single().Effective.Amount);
        using var connection = new SqliteConnection("Data Source=" + file.Path + ";Pooling=False"); connection.Open();
        using var v7 = new InternationalImportDbContext(connection); using var v8 = new ResourceDbContext(connection);
        Assert.IsFalse(v7.Database.HasPendingModelChanges()); Assert.IsFalse(v8.Database.HasPendingModelChanges());
        Assert.AreEqual(9, v8.Database.GetAppliedMigrations().Count());
    }
    [TestMethod]
    public async Task T07_ZeroAccountMustRetainAnExplicitRoute()
    {
        using var file = Baseline(); var w = await ImportedResources(file, "Amount;Ignored;Account\n0;x;ZERO");
        Assert.IsTrue(w.Preparation.HasPendingParts); var hash = Hash(file.Path);
        var error = Assert.ThrowsExactly<DomainRuleException>(() => file.Resources.ChangeResources(Mutation(w, ResourceMutationKind.EditResource, w.Resources.Single().Id, [])));
        Assert.AreEqual("RES_PARTS_REQUIRED", error.Diagnostic.Code); Assert.AreEqual(hash, Hash(file.Path));
    }
    [TestMethod]
    public async Task T07_ChangingOnlyObjectNamePreservesEconomicHashButNotFullSnapshot()
    {
        using var file = Baseline(); var w = await ImportedResources(file);
        var obj = new ResourceCostObject(Id(), w.Study.CompanyId, w.Study.Id, "PRODUCT", "Original label", ResourceObjectKind.Product);
        file.Resources.ChangeResources(Mutation(w, ResourceMutationKind.PutObject, obj: obj));
        w = file.Resources.ReadResources(w.Study.CompanyId, w.Study.Id);
        file.Resources.ChangeResources(Mutation(w, ResourceMutationKind.EditResource, w.Resources.Single().Id,
            [new ResourcePartDraft(Id(), Eur(1000), ResourceRoutingKind.DirectObject, [new(obj.Id, Eur(1000))])]));
        w = file.Resources.ReadResources(w.Study.CompanyId, w.Study.Id);
        var previous = file.Studies.Capture(new(w.Study.CompanyId, w.Study.Id, w.Study.Revision, Id(), "test"));
        var renamed = file.Resources.ChangeResources(Mutation(w, ResourceMutationKind.PutObject, obj: obj with { Name = "Etiqueta 日本" }));
        var next = file.Studies.Capture(new(w.Study.CompanyId, w.Study.Id, renamed.ConfirmedStudy.Revision, Id(), "test"));
        Assert.AreEqual(previous.EconomicInputHash, next.EconomicInputHash);
        Assert.AreNotEqual(previous.FullSnapshotHash, next.FullSnapshotHash);
    }
    [TestMethod]
    public async Task T07_CapacityOwnerMustMatchPublishedPoolNodeAndUnit()
    {
        using var file = Baseline(); var w = await ImportedResources(file); var pool = w.Model.Structure.Pools.First();
        var amount = Eur(1000); var hash = Hash(file.Path);
        var owner = new ResourceCapacityOwner(pool.Id, Id(), pool.DriverUnitId, Normal: new Quantity(100, pool.DriverUnitId));
        var part = new ResourcePartDraft(Id(), amount, ResourceRoutingKind.AllocationPool, poolId: pool.Id,
            classification: new(ResourceBehavior.Unclassified, null, ResourceCostLayer.Structure, ResourceCapacityTreatment.AssessAtPool, owner));
        var error = Assert.ThrowsExactly<DomainRuleException>(() => file.Resources.ChangeResources(Mutation(w, ResourceMutationKind.EditResource, w.Resources.Single().Id, [part])));
        Assert.AreEqual("RES_CAPACITY_OWNER_INVALID", error.Diagnostic.Code); Assert.AreEqual(hash, Hash(file.Path));
    }
    [TestMethod]
    public async Task T07_RequestPayloadAndAdjustmentIdentityCannotBeReusedAfterReset()
    {
        using var file = Baseline(); var w = await ImportedResources(file); var delta = Delta(-100);
        var command = Mutation(w, ResourceMutationKind.EditResource, w.Resources.Single().Id, [Pending(900)], [delta]);
        file.Resources.ChangeResources(command);
        var reused = new ResourceMutation(command.Kind, command.CompanyId, command.StudyId, command.ExpectedRevision, command.RequestId,
            command.Actor, "Different reason", command.ResourceId, command.Parts, command.Adjustments, null, null, [], []);
        Assert.AreEqual("RES_REQUEST_REUSED", Assert.ThrowsExactly<DomainRuleException>(() => file.Resources.ChangeResources(reused)).Diagnostic.Code);
        w = file.Resources.ReadResources(w.Study.CompanyId, w.Study.Id);
        file.Resources.ChangeResources(Mutation(w, ResourceMutationKind.ResetPreparation));
        w = file.Resources.ReadResources(w.Study.CompanyId, w.Study.Id);
        Assert.AreEqual("RES_DUPLICATE_ID", Assert.ThrowsExactly<DomainRuleException>(() => file.Resources.ChangeResources(
            Mutation(w, ResourceMutationKind.EditResource, w.Resources.Single().Id, [Pending(900)], [delta]))).Diagnostic.Code);
    }
}
