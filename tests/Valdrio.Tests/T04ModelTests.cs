using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Infrastructure;
using Valdrio.Infrastructure.CompanyFiles;

namespace Valdrio.Tests;
[TestClass]
public sealed class T04ModelTests
{
    private string directory = null!;
    private string FileOf(string name = "company.valdrio") => Path.Combine(directory, name);
    private sealed class Clock : IClock
    {
        public UtcInstant GetUtcNow() => UtcInstant.ParseIso("2026-10-05T14:00:00.0000000Z");
    }

    private SqliteCompanyFileService Service(Action<CompanyWriteCheckpoint>? hook = null) => new(new JsonCurrencyCatalog(Path.Combine(AppContext.BaseDirectory, "reference", "currency-catalog-example.json")), new Clock(), hook);
    private ReferenceV1.SqliteCompanyFileService OriginalService() => new(new JsonCurrencyCatalog(Path.Combine(AppContext.BaseDirectory, "reference", "currency-catalog-example.json")), new Clock());
    private ICompanyFileSession New() => Service().Create(new(FileOf(), "Fábrica 工場", new("USD"), PrecisionPolicy.Pdec1Proposed));
    private static EntityId Id() => new(Guid.NewGuid());
    private static UnitDefinition Hour() => new(new(Id()), "h", "time", "h", 12, false);
    private static UnitDefinition Lot() => new(new(Id()), "lot", "batch", "lot", 0, true);
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static string Query(string path, string sql)
    {
        using var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false, ForeignKeys = true }.ToString());
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToString(cmd.ExecuteScalar(), CultureInfo.InvariantCulture) ?? "";
    }

    private static void Sql(string path, string sql) => _ = Query(path, sql);
    [TestInitialize]
    public void Init()
    {
        directory = Path.Combine(Path.GetTempPath(), "valdrio-t04-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
    }

    [TestCleanup]
    public void Clean() => Directory.Delete(directory, true);
    private string V1()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "t03-v1.json")));
        using var input = new MemoryStream(Convert.FromBase64String(doc.RootElement.GetProperty("GzipBase64").GetString()!));
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = File.Create(FileOf("v1.valdrio"));
        gzip.CopyTo(output);
        output.Close();
        Assert.AreEqual(doc.RootElement.GetProperty("Sha256").GetString(), Hash(FileOf("v1.valdrio")));
        return FileOf("v1.valdrio");
    }

    private static ModelStructure Edit(ModelStructure m, IReadOnlyList<CalculationNodeDefinition>? nodes = null, IReadOnlyList<CostPoolDefinition>? pools = null, string? description = null) => new(m.Id, m.CompanyId, m.FamilyId, m.VersionNumber, m.ParentVersionId, description ?? m.Description, m.PrecisionPolicyVersion, m.MethodPolicy, nodes ?? m.Nodes, pools ?? m.Pools);
    private ModelVersionSnapshot Example(ICompanyFileSession file)
    {
        var company = file.Read();
        var h = Hour();
        var lot = Lot();
        file.AddUnits([h, lot], company.Revision, "test");
        var press = file.Models.CreateCenter(new(company.Id, "PRENSA-001", "Prensa 日本", "test"));
        var maintenance = file.Models.CreateCenter(new(company.Id, "MANT-001", "Mantenimiento", "test"));
        var draft = file.Models.CreateDraft(new(company.Id, "Modelo industrial", "test"));
        var m = draft.Structure;
        var setup = new CalculationNodeDefinition(Id(), m.Id, press.Id, "SETUP", NodeRole.Principal, ["production"], true);
        var process = new CalculationNodeDefinition(Id(), m.Id, press.Id, "PROCESO", NodeRole.Principal, ["production"], true);
        var aux = new CalculationNodeDefinition(Id(), m.Id, maintenance.Id, "MANT", NodeRole.Auxiliary, ["production", "maintenance"], true);
        var pools = new[]
        {
            new CostPoolDefinition(Id(), m.Id, setup.Id, lot.Id, "Setup por lote", CostLayer.Unclassified, ModelPolicyReference.PendingCapacity),
            new CostPoolDefinition(Id(), m.Id, process.Id, h.Id, "Proceso por hora", CostLayer.Unclassified, ModelPolicyReference.PendingCapacity),
            new CostPoolDefinition(Id(), m.Id, aux.Id, h.Id, "Servicio por hora", CostLayer.Unclassified, ModelPolicyReference.PendingCapacity)
        };
        return file.Models.SaveDraft(new(Edit(m, [setup, process, aux], pools), 0, "test"));
    }

    [TestMethod]
    public void M01_UpgradeGenuineT03ArtifactPreservesSourceIdentityPolicyUnitsAndCompanyAudit()
    {
        var source = V1();
        var sha = Hash(source);
        var company = Query(source, "SELECT Id FROM Companies");
        var audit = Query(source, "SELECT group_concat(AfterJson) FROM AuditEvents ORDER BY Revision");
        Assert.AreEqual("FILE_UPGRADE_REQUIRED", Assert.ThrowsExactly<CompanyFileException>(() => Service().Open(source)).Diagnostic.Code);
        using var upgraded = Service().Upgrade(source, FileOf("upgraded.valdrio"), "test");
        Assert.AreEqual(SqliteCompanyFileService.SchemaVersion, upgraded.Read().SchemaVersion);
        Assert.AreEqual(company, upgraded.Read().Id.ToString());
        Assert.AreEqual("00123", upgraded.Read().Units.Single().Code);
        Assert.AreEqual(PrecisionPolicy.Pdec1Proposed, upgraded.Read().PrecisionPolicy);
        Assert.AreEqual(sha, Hash(source));
        var upgradedHash = Hash(upgraded.Path);
        Assert.AreEqual("FILE_SCHEMA_UNSUPPORTED", Assert.ThrowsExactly<ReferenceV1.CompanyFileException>(() => OriginalService().Open(upgraded.Path)).Diagnostic.Code);
        Assert.AreEqual(upgradedHash, Hash(upgraded.Path));
        using (var originalReader = OriginalService().Open(source))
            Assert.AreEqual(company, originalReader.Read().Id.ToString());
        Assert.AreEqual(audit, Query(upgraded.Path, "SELECT group_concat(AfterJson) FROM AuditEvents WHERE AggregateType='Company' ORDER BY Revision"));
        Assert.AreEqual("8", Query(upgraded.Path, "SELECT count(*) FROM AuditEvents WHERE AggregateType='CompanyFile'"));
        using var conn = new SqliteConnection("Data Source=" + upgraded.Path + ";Pooling=False");
        conn.Open();
        using var db = new ModelDbContext(conn);
        Assert.IsFalse(db.Database.HasPendingModelChanges());
    }

    [TestMethod]
    public void M02_MigrationFailureAndCancellationNeverPublishOrChangeOriginal()
    {
        var source = V1();
        var sha = Hash(source);
        Assert.ThrowsExactly<CompanyFileException>(() => Service(p =>
        {
            if (p == CompanyWriteCheckpoint.AfterFirstWrite)
                throw new IOException("injected");
        }).Upgrade(source, FileOf("failed.valdrio"), "test"));
        using var cancel = new CancellationTokenSource();
        Assert.ThrowsExactly<OperationCanceledException>(() => Service(p =>
        {
            if (p == CompanyWriteCheckpoint.BeforeCommit)
                cancel.Cancel();
        }).Upgrade(source, FileOf("cancel.valdrio"), "test", cancel.Token));
        Assert.AreEqual(sha, Hash(source));
        using (var originalReader = OriginalService().Open(source))
            Assert.AreEqual(1, originalReader.Read().SchemaVersion);
        Assert.IsFalse(File.Exists(FileOf("failed.valdrio")));
        Assert.IsFalse(File.Exists(FileOf("cancel.valdrio")));
        Assert.AreEqual(0, Directory.GetFiles(directory, "*.creating-*").Length);
        using var c = new SqliteConnection("Data Source=" + source + ";Mode=ReadOnly;Pooling=False");
        c.Open();
        using var legacy = new CompanyDbContext(c);
        Assert.AreEqual(1, legacy.Metadata.Single().SchemaVersion);
        Assert.IsFalse(legacy.Database.HasPendingModelChanges());
        Assert.AreEqual(2, legacy.Audit.Count());
    }

    [TestMethod]
    public void M03_FutureForeignAndStaleMigrationLockRejectedWithoutMutation()
    {
        var source = V1();
        Sql(source, "UPDATE FileMetadata SET SchemaVersion=99");
        var sha = Hash(source);
        Assert.AreEqual("FILE_SCHEMA_UNSUPPORTED", Assert.ThrowsExactly<CompanyFileException>(() => Service().Upgrade(source, FileOf("no.valdrio"), "test")).Diagnostic.Code);
        Assert.AreEqual(sha, Hash(source));
        Sql(source, "UPDATE FileMetadata SET SchemaVersion=1; INSERT INTO __EFMigrationsLock(Id,Timestamp) VALUES(1,'2026-10-05T12:00:00Z')");
        sha = Hash(source);
        Assert.AreEqual("FILE_MIGRATION_LOCKED", Assert.ThrowsExactly<CompanyFileException>(() => Service().Upgrade(source, FileOf("lock.valdrio"), "test")).Diagnostic.Code);
        Assert.AreEqual(sha, Hash(source));
        Sql(source, "DELETE FROM __EFMigrationsLock; UPDATE FileMetadata SET Format='OTHER'");
        sha = Hash(source);
        Assert.AreEqual("FILE_FOREIGN_FORMAT", Assert.ThrowsExactly<CompanyFileException>(() => Service().Upgrade(source, FileOf("foreign.valdrio"), "test")).Diagnostic.Code);
        Assert.AreEqual(sha, Hash(source));
    }

    [TestMethod]
    public void M04_CenterCodesUnicodeAndPhysicalUniqueConstraint()
    {
        using var file = New();
        var id = file.Read().Id;
        var c = file.Models.CreateCenter(new(id, "00123 工場", "Centro 日本", "test"));
        Assert.AreEqual("MODEL_DUPLICATE", Assert.ThrowsExactly<DomainRuleException>(() => file.Models.CreateCenter(new(id, c.Code, "Other", "test"))).Diagnostic.Code);
        Assert.ThrowsExactly<SqliteException>(() => Sql(file.Path, "INSERT INTO PhysicalCenters SELECT '" + Id() + "',CompanyId,Code,Name,IsActive,Revision FROM PhysicalCenters"));
        using var read = Service().Open(file.Path, CompanyFileOpenMode.ReadOnly);
        Assert.AreEqual(c, read.Models.ReadCenters().Single());
        Assert.AreEqual(0L, file.Read().Revision);
    }

    [TestMethod]
    public void M05_M06_SetupProcessSeparatePoolsRolesFunctionsAndPendingPolicies()
    {
        using var file = New();
        var m = Example(file);
        Assert.AreEqual(3, m.Structure.Pools.Count);
        Assert.AreEqual(0, file.Models.ValidateModel(file.Read().Id, m.Structure.Id).Count);
        var setup = m.Structure.Nodes.Single(n => n.Code == "SETUP");
        var process = m.Structure.Nodes.Single(n => n.Code == "PROCESO");
        Assert.AreNotEqual(m.Structure.Pools.Single(p => p.NodeId == setup.Id).DriverUnitId, m.Structure.Pools.Single(p => p.NodeId == process.Id).DriverUnitId);
        Assert.IsTrue(m.Structure.Nodes.Single(n => n.Role == NodeRole.Auxiliary).FunctionTags.Contains("production"));
        Assert.ThrowsExactly<DomainRuleException>(() => new CalculationNodeDefinition(Id(), m.Structure.Id, setup.PhysicalCenterId, "X", (NodeRole)2, [], true));
        Assert.AreEqual(PolicyResolution.Pending, m.Structure.MethodPolicy.Resolution);
        Assert.ThrowsExactly<DomainRuleException>(() => (new ModelPolicyReference("NormalizedManagement", "1", PolicyResolution.Accepted)).EnsureValid());
    }

    [TestMethod]
    public void M07_CrossVersionAndCompanyRejectedByDomainAndCompoundForeignKeys()
    {
        using var file = New();
        var a = Example(file);
        var b = file.Models.CreateDraft(new(file.Read().Id, "B", "test"));
        var pool = a.Structure.Pools[0];
        Assert.ThrowsExactly<DomainRuleException>(() => Edit(a.Structure, pools: [pool with { ModelVersionId = b.Structure.Id }]));
        Assert.ThrowsExactly<DomainRuleException>(() => file.Models.CreateDraft(new(Id(), "Alien", "test")));
        Assert.ThrowsExactly<SqliteException>(() => Sql(file.Path, "UPDATE CostPools SET ModelVersionId='" + b.Structure.Id + "' WHERE Id='" + pool.Id + "'"));
        Assert.ThrowsExactly<SqliteException>(() => Sql(file.Path, "UPDATE CalculationNodes SET CompanyId='" + Id() + "' WHERE Id='" + a.Structure.Nodes[0].Id + "'"));
        Assert.ThrowsExactly<SqliteException>(() => Sql(file.Path, "UPDATE ModelVersions SET Status='Published' WHERE Id='" + b.Structure.Id + "'"));
        Assert.AreEqual(3, file.Models.ReadModels().Single(x => x.Structure.Id == a.Structure.Id).Structure.Pools.Count);
    }

    [TestMethod]
    public void M08_MissingPoolAndInactiveUnitBlockPublicationWithoutPartialRows()
    {
        using var file = New();
        var m = Example(file);
        var saved = file.Models.SaveDraft(new(Edit(m.Structure, pools: m.Structure.Pools.Skip(1).ToArray()), m.Revision, "test"));
        Assert.IsTrue(file.Models.ValidateModel(file.Read().Id, m.Structure.Id).Any(d => d.Code == "MODEL_POOL_REQUIRED"));
        Assert.ThrowsExactly<DomainRuleException>(() => file.Models.Publish(new(file.Read().Id, m.Structure.Id, saved.Revision, "test")));
        Assert.AreEqual("0", Query(file.Path, "SELECT count(*) FROM ModelPublications"));
        m = file.Models.SaveDraft(new(m.Structure, saved.Revision, "test"));
        var unit = file.Models.ReadDriverUnits()[0];
        file.Models.ChangeUnitState(new(file.Read().Id, unit.Definition.Id, 0, false, "test"));
        Assert.IsTrue(file.Models.ValidateModel(file.Read().Id, m.Structure.Id).Any(d => d.Code == "MODEL_DRIVER_UNAVAILABLE"));
        Assert.ThrowsExactly<DomainRuleException>(() => file.Models.Publish(new(file.Read().Id, m.Structure.Id, m.Revision, "test")));
        var missing = m.Structure.Pools[0] with
        {
            DriverUnitId = new(Id())
        };
        Assert.ThrowsExactly<DomainRuleException>(() => file.Models.SaveDraft(new(Edit(m.Structure, pools: m.Structure.Pools.Select(p => p.Id == missing.Id ? missing : p).ToArray()), m.Revision, "test")));
        Assert.ThrowsExactly<DomainRuleException>(() => Edit(m.Structure, pools: m.Structure.Pools.Append(m.Structure.Pools[0] with { Id = Id() }).ToArray()));
    }

    [TestMethod]
    public void M09_PublishedEditsAndInsertDeleteRejectedByPortsAndPersistence()
    {
        using var file = New();
        var m = Example(file);
        var published = file.Models.Publish(new(file.Read().Id, m.Structure.Id, m.Revision, "test"));
        var json = published.PublishedCanonicalJson;
        Assert.AreEqual("MODEL_PUBLISHED_IMMUTABLE", Assert.ThrowsExactly<DomainRuleException>(() => file.Models.SaveDraft(new(Edit(m.Structure, description: "Changed"), published.Revision, "test"))).Diagnostic.Code);
        foreach (var sql in new[]
        {
            "UPDATE ModelVersions SET Description='Changed'",
            "UPDATE CalculationNodes SET Code='Changed'",
            "DELETE FROM CostPools",
            "UPDATE ModelPublications SET CanonicalJson='{}'",
            "DELETE FROM ModelVersions",
            "DELETE FROM ModelPublications",
            "INSERT INTO CalculationNodes SELECT '" + Id() + "',CompanyId,ModelVersionId,PhysicalCenterId,'X',Role,FunctionTagsJson,IsActive FROM CalculationNodes LIMIT 1"
        }

        )
            Assert.ThrowsExactly<SqliteException>(() => Sql(file.Path, sql));
        Assert.AreEqual(json, file.Models.ReadModels().Single().PublishedCanonicalJson);
    }

    [TestMethod]
    public void M10_M14_DeactivateRenameMasterPreservesFrozenHistoryAndBlocksNewSelection()
    {
        using var file = New();
        var m = Example(file);
        var pub = file.Models.Publish(new(file.Read().Id, m.Structure.Id, m.Revision, "test"));
        var c = file.Models.ReadCenters().Single(x => x.Code == "PRENSA-001");
        var changed = file.Models.ChangeCenter(new(file.Read().Id, c.Id, 0, "PRENSA-NEW", "Nuevo nombre", false, "test"));
        Assert.AreEqual(1L, changed.Revision);
        var read = file.Models.ReadModels().Single();
        Assert.AreEqual(pub.StructuralHash, read.StructuralHash);
        Assert.AreEqual("Prensa 日本", read.Publication!.PhysicalCenters.Single(x => x.Id == c.Id).Name);
        var clone = file.Models.Clone(new(file.Read().Id, m.Structure.Id, pub.Revision, Id(), "Clone", "test"));
        Assert.IsTrue(file.Models.ValidateModel(file.Read().Id, clone.Structure.Id).Any(d => d.Code == "MODEL_CENTER_UNAVAILABLE"));
        var draft = file.Models.CreateDraft(new(file.Read().Id, "New draft", "test"));
        var n = new CalculationNodeDefinition(Id(), draft.Structure.Id, c.Id, "NEW", NodeRole.Principal, [], true);
        Assert.ThrowsExactly<DomainRuleException>(() => file.Models.SaveDraft(new(Edit(draft.Structure, [n]), 0, "test")));
    }

    [TestMethod]
    public void M11_CloneRemapsAllIdsAndEditsOnlyNewVersion()
    {
        using var file = New();
        var m = Example(file);
        var pub = file.Models.Publish(new(file.Read().Id, m.Structure.Id, m.Revision, "test"));
        var clone = file.Models.Clone(new(file.Read().Id, m.Structure.Id, pub.Revision, Id(), "Versión 2", "test"));
        Assert.AreEqual(m.Structure.Id, clone.Structure.ParentVersionId);
        Assert.AreEqual(2, clone.Structure.VersionNumber);
        Assert.AreEqual(m.Structure.FamilyId, clone.Structure.FamilyId);
        Assert.IsFalse(clone.Structure.Nodes.Any(n => m.Structure.Nodes.Any(old => old.Id == n.Id)));
        Assert.IsFalse(clone.Structure.Pools.Any(p => m.Structure.Pools.Any(old => old.Id == p.Id)));
        Assert.IsTrue(clone.Structure.Pools.All(p => p.ModelVersionId == clone.Structure.Id && clone.Structure.Nodes.Any(n => n.Id == p.NodeId)));
        file.Models.SaveDraft(new(Edit(clone.Structure, description: "Editable"), 0, "test"));
        Assert.AreEqual(pub.PublishedCanonicalJson, file.Models.ReadModels().Single(x => x.Status == ModelStatus.Published).PublishedCanonicalJson);
    }

    [TestMethod]
    [DataRow("Save")]
    [DataRow("Publish")]
    [DataRow("Clone")]
    [DataRow("Center")]
    [DataRow("Unit")]
    public void M12_InjectedFailureAfterRealWriteRollsBackAggregateAndAudit(string operation)
    {
        ModelVersionSnapshot m;
        EntityId company;
        using (var file = New())
        {
            m = Example(file);
            company = file.Read().Id;
            if (operation == "Clone")
                m = file.Models.Publish(new(company, m.Structure.Id, m.Revision, "test"));
        }

        var before = Query(FileOf(), "SELECT count(*) FROM AuditEvents");
        string all;
        using (var read = Service().Open(FileOf()))
            all = CanonicalSnapshotJson.Serialize(read.Models.ReadModels());
        using (var file = Service(p =>
        {
            if (p == CompanyWriteCheckpoint.AfterFirstWrite)
                throw new IOException("after actual write");
        }).Open(FileOf()))
        {
            Assert.ThrowsExactly<CompanyFileException>(() =>
            {
                switch (operation)
                {
                    case "Save":
                        file.Models.SaveDraft(new(Edit(m.Structure, description: "Failure"), m.Revision, "test"));
                        break;
                    case "Publish":
                        file.Models.Publish(new(company, m.Structure.Id, m.Revision, "test"));
                        break;
                    case "Clone":
                        file.Models.Clone(new(company, m.Structure.Id, m.Revision, Id(), "Failure", "test"));
                        break;
                    case "Center":
                        file.Models.CreateCenter(new(company, "FAILED", "Failure", "test"));
                        break;
                    case "Unit":
                        file.Models.ChangeUnitState(new(company, m.Structure.Pools[0].DriverUnitId, 0, false, "test"));
                        break;
                }
            });
        }

        using var reopened = Service().Open(FileOf());
        Assert.AreEqual(all, CanonicalSnapshotJson.Serialize(reopened.Models.ReadModels()));
        Assert.AreEqual(before, Query(FileOf(), "SELECT count(*) FROM AuditEvents"));
        Assert.IsFalse(reopened.Models.ReadCenters().Any(c => c.Code == "FAILED"));
        Assert.IsTrue(reopened.Models.ReadDriverUnits().All(u => u.IsActive));
    }

    [TestMethod]
    public void M12_CancelBeforeCommitRollsBackPublication()
    {
        ModelVersionSnapshot m;
        EntityId company;
        using (var file = New())
        {
            m = Example(file);
            company = file.Read().Id;
        }

        using var cancel = new CancellationTokenSource();
        using (var file = Service(p =>
        {
            if (p == CompanyWriteCheckpoint.BeforeCommit)
                cancel.Cancel();
        }).Open(FileOf()))
            Assert.ThrowsExactly<OperationCanceledException>(() => file.Models.Publish(new(company, m.Structure.Id, m.Revision, "test"), cancel.Token));
        using var read = Service().Open(FileOf());
        Assert.AreEqual(ModelStatus.Draft, read.Models.ReadModels().Single().Status);
        Assert.AreEqual("0", Query(FileOf(), "SELECT count(*) FROM ModelPublications"));
    }

    [TestMethod]
    public void M13_StaleRevisionsPublishRepeatAndCloneIdempotency()
    {
        using var file = New();
        var m = Example(file);
        var company = file.Read().Id;
        Assert.AreEqual("MODEL_REVISION_CHANGED", Assert.ThrowsExactly<DomainRuleException>(() => file.Models.SaveDraft(new(m.Structure, 0, "test"))).Diagnostic.Code);
        var pub = file.Models.Publish(new(company, m.Structure.Id, m.Revision, "test"));
        Assert.ThrowsExactly<DomainRuleException>(() => file.Models.Publish(new(company, m.Structure.Id, m.Revision, "test")));
        var command = new CloneModel(company, m.Structure.Id, pub.Revision, Id(), "Clone", "test");
        var clone = file.Models.Clone(command);
        var repeated = file.Models.Clone(command);
        Assert.AreEqual(clone.Structure.Id, repeated.Structure.Id);
        Assert.AreEqual(2, file.Models.ReadModels().Count);
        Assert.AreEqual("MODEL_REQUEST_CONFLICT", Assert.ThrowsExactly<DomainRuleException>(() => file.Models.Clone(command with { Description = "Different" })).Diagnostic.Code);
        Assert.AreEqual(1L, file.Read().Revision);
    }

    [TestMethod]
    public void M15_StructureSortingCultureAndDefensiveCollectionsAreStable()
    {
        using var file = New();
        var m = Example(file);
        var baseJson = CanonicalSnapshotJson.Serialize(m.Structure);
        var original = CultureInfo.CurrentCulture;
        try
        {
            foreach (var culture in new[]
            {
                "es-ES",
                "en-US",
                "de-DE",
                "en-GB"
            }

            )
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                var reversed = Edit(m.Structure, m.Structure.Nodes.Reverse().ToArray(), m.Structure.Pools.Reverse().ToArray());
                Assert.AreEqual(baseJson, CanonicalSnapshotJson.Serialize(reversed));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }

        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<CalculationNodeDefinition>)m.Structure.Nodes).Clear());
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<string>)m.Structure.Nodes[0].FunctionTags).Clear());
        var pub = file.Models.Publish(new(file.Read().Id, m.Structure.Id, m.Revision, "test"));
        Assert.AreEqual(pub.StructuralHash, PublishedModelStructure.Hash(pub.PublishedCanonicalJson!));
        Assert.AreEqual(pub.PublishedCanonicalJson, CanonicalSnapshotJson.Serialize(CanonicalSnapshotJson.Deserialize<PublishedModelStructure>(pub.PublishedCanonicalJson!)));
    }

    [TestMethod]
    public void M17_BackupRestoreRetainsPublishedCloneAndIndependentAuditStreams()
    {
        using var file = New();
        var m = Example(file);
        var pub = file.Models.Publish(new(file.Read().Id, m.Structure.Id, m.Revision, "test"));
        file.Models.Clone(new(file.Read().Id, m.Structure.Id, pub.Revision, Id(), "Clone", "test"));
        var before = CanonicalSnapshotJson.Serialize(file.Models.ReadModels());
        file.Backup(FileOf("backup.valdrio-backup"));
        var sha = Hash(file.Path);
        using var restored = Service().Restore(FileOf("backup.valdrio-backup"), FileOf("restored.valdrio"));
        Assert.AreEqual(before, CanonicalSnapshotJson.Serialize(restored.Models.ReadModels()));
        Assert.AreEqual(sha, Hash(file.Path));
        Assert.AreEqual(Query(file.Path, "SELECT count(*) FROM AuditEvents"), Query(restored.Path, "SELECT count(*) FROM AuditEvents"));
    }

    [TestMethod]
    public void M_AuditTamperAndUnknownSchemaRejectedBeforeWrite()
    {
        using (var file = New())
        {
            Example(file);
        }

        Sql(FileOf(), "UPDATE AuditEvents SET AfterJson='{}' WHERE Operation='SaveDraft'");
        var sha = Hash(FileOf());
        Assert.ThrowsExactly<CompanyFileException>(() => Service().Open(FileOf()));
        Assert.AreEqual(sha, Hash(FileOf()));
    }

    [TestMethod]
    public void M_ReadOnlyWorkspaceRejectsEveryWrite()
    {
        using var file = New();
        var m = Example(file);
        using var read = Service().Open(file.Path, CompanyFileOpenMode.ReadOnly);
        Assert.AreEqual("FILE_READ_ONLY", Assert.ThrowsExactly<CompanyFileException>(() => read.Models.SaveDraft(new(m.Structure, m.Revision, "test"))).Diagnostic.Code);
        Assert.ThrowsExactly<CompanyFileException>(() => read.Models.CreateCenter(new(file.Read().Id, "X", "X", "test")));
    }
}
