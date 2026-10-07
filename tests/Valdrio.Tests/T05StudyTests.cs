using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Infrastructure;
using Valdrio.Infrastructure.CompanyFiles;
using Valdrio.Localization;

namespace Valdrio.Tests;

[TestClass]
public sealed class T05StudyTests
{
    private string directory = null!;
    private string FileOf(string name = "study.valdrio") => Path.Combine(directory, name);
    private sealed class Clock : IClock { public UtcInstant GetUtcNow() => UtcInstant.ParseIso("2026-10-05T00:30:00Z"); }
    private static EntityId Id() => new(Guid.NewGuid());
    private SqliteCompanyFileService Service(Action<CompanyWriteCheckpoint>? hook = null) =>
        new(new JsonCurrencyCatalog(Path.Combine(AppContext.BaseDirectory, "reference", "currency-catalog-example.json")), new Clock(), hook);
    [TestInitialize] public void Init() { directory = Path.Combine(Path.GetTempPath(), "valdrio-t05-" + Guid.NewGuid()); Directory.CreateDirectory(directory); }
    [TestCleanup] public void Cleanup() => Directory.Delete(directory, true);
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static string Query(string path, string sql)
    {
        using var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false, ForeignKeys = true }.ToString());
        db.Open(); using var cmd = db.CreateCommand(); cmd.CommandText = sql;
        return Convert.ToString(cmd.ExecuteScalar(), CultureInfo.InvariantCulture) ?? "";
    }
    private string Fixture(int schema, string? fixtureName = null)
    {
        var name = fixtureName ?? (schema == 1 ? "t03-v1.json" : "t04-v2.json");
        using var d = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name)));
        using var stream = new GZipStream(new MemoryStream(Convert.FromBase64String(d.RootElement.GetProperty("GzipBase64").GetString()!)), CompressionMode.Decompress);
        var path = FileOf("v" + schema + ".valdrio");
        using (var output = File.Create(path)) stream.CopyTo(output);
        Assert.AreEqual(d.RootElement.GetProperty("Sha256").GetString(), Hash(path)); return path;
    }
    private ICompanyFileSession Baseline() => Service().Upgrade(Fixture(2), FileOf(), "test");
    private static CreateStudy CreateCommand(ICompanyFileSession file) => new(file.Read().Id, Id(), "Octubre 日本",
        BusinessDate.ParseIso("2026-10-01"), BusinessDate.ParseIso("2026-10-31"), StudyPurpose.Management, StudyScenario.Actual,
        file.Models.ReadModels().First(m => m.Status == ModelStatus.Published).Structure.Id, new("EUR"), "test");
    private static UpdateStudyContext UpdateCommand(StudyDefinition s) => new(s.CompanyId, s.Id, s.Revision, s.Name,
        s.PeriodStart, s.PeriodEnd, s.Purpose, s.ScenarioKind, s.SelectedModelVersionId, s.Currency.FunctionalCurrencyCode, "test");
    private static CaptureStudySnapshot CaptureCommand(StudyDefinition s) => new(s.CompanyId, s.Id, s.Revision, Id(), "test");
    private static StudyDefinition Change(StudyDefinition s, string? name = null, long? revision = null, BusinessDate? start = null,
        StudyCurrencyContext? currency = null, StudyScenario? scenario = null) => new(s.Id, s.CompanyId, name ?? s.Name,
            start ?? s.PeriodStart, s.PeriodEnd, s.Purpose, scenario ?? s.ScenarioKind, null, s.SelectedModelVersionId,
            currency ?? s.Currency, s.CurrencyValidity, revision ?? s.Revision, s.State, null, null, s.CreatedAtUtc);
    private static StudySnapshotInput Change(IStudySnapshotInput input, StudyDefinition? context = null,
        StudySnapshotProvenance? provenance = null, PublishedModelStructure? model = null, PrecisionPolicy? policy = null) =>
        new(StudySnapshotInput.Schema, StudySnapshotInput.PreparationVersion, context ?? input.StudyContext,
            policy ?? input.PrecisionPolicy, model ?? input.Model, provenance ?? input.Provenance, [], [], [], []);

    [TestMethod] [DataRow(1)] [DataRow(2)]
    public void S01_UpgradeActualHistoricalFilesAndFrozenMappings(int schema)
    {
        var source = Fixture(schema); var sha = Hash(source); var companyId = Query(source, "SELECT Id FROM Companies");
        var units = Query(source, "SELECT group_concat(Id||Code) FROM Units ORDER BY Id");
        var companyAudit = Query(source, "SELECT group_concat(AfterJson) FROM AuditEvents " + (schema == 2 ? "WHERE AggregateType='Company' " : "") + "ORDER BY Revision");
        var publications = schema == 2 ? Query(source, "SELECT group_concat(CanonicalJson) FROM ModelPublications ORDER BY ModelVersionId") : "";
        Assert.AreEqual("FILE_UPGRADE_REQUIRED", Assert.ThrowsExactly<CompanyFileException>(() => Service().Open(source)).Diagnostic.Code);
        using var file = Service().Upgrade(source, FileOf(), "test");
        Assert.AreEqual(8, file.Read().SchemaVersion); Assert.AreEqual(sha, Hash(source));
        Assert.AreEqual(companyId, file.Read().Id.ToString()); Assert.AreEqual(units, Query(file.Path, "SELECT group_concat(Id||Code) FROM Units ORDER BY Id"));
        Assert.AreEqual(companyAudit, Query(file.Path, "SELECT group_concat(AfterJson) FROM AuditEvents WHERE AggregateType='Company' ORDER BY Revision"));
        if (schema == 2) Assert.AreEqual(publications, Query(file.Path, "SELECT group_concat(CanonicalJson) FROM ModelPublications ORDER BY ModelVersionId"));
        Assert.AreEqual((8 - schema).ToString(CultureInfo.InvariantCulture), Query(file.Path, "SELECT count(*) FROM AuditEvents WHERE AggregateType='CompanyFile'"));
        using var c = new SqliteConnection("Data Source=" + file.Path + ";Pooling=False"); c.Open();
        using var v3 = new StudyDbContext(c); using var v2 = new ModelDbContext(c); using var v1 = new CompanyDbContext(c);
        Assert.IsFalse(v3.Database.HasPendingModelChanges()); Assert.IsFalse(v2.Database.HasPendingModelChanges()); Assert.IsFalse(v1.Database.HasPendingModelChanges());
        using var v4 = new ImportDbContext(c); Assert.IsFalse(v4.Database.HasPendingModelChanges());
        Assert.AreEqual(8, v4.Database.GetAppliedMigrations().Count());
    }

    [TestMethod]
    public void S02_UpgradeFailuresCancellationFutureAndForeignLocksPreserveSource()
    {
        var source = Fixture(2); var sha = Hash(source);
        Assert.ThrowsExactly<CompanyFileException>(() => Service(c => { if (c == CompanyWriteCheckpoint.AfterFirstWrite) throw new IOException("fault"); }).Upgrade(source, FileOf("failed"), "test"));
        using var cancel = new CancellationTokenSource();
        Assert.ThrowsExactly<OperationCanceledException>(() => Service(c => { if (c == CompanyWriteCheckpoint.BeforeCommit) cancel.Cancel(); }).Upgrade(source, FileOf("cancel"), "test", cancel.Token));
        Assert.AreEqual(sha, Hash(source)); Assert.IsFalse(File.Exists(FileOf("failed"))); Assert.IsFalse(File.Exists(FileOf("cancel")));
        Assert.AreEqual(0, Directory.GetFiles(directory, "*.creating-*").Length);
        Query(source, "INSERT INTO __EFMigrationsLock(Id,Timestamp) VALUES(1,'2026-10-05T00:00:00Z')"); sha = Hash(source);
        Assert.AreEqual("FILE_MIGRATION_LOCKED", Assert.ThrowsExactly<CompanyFileException>(() => Service().Upgrade(source, FileOf("locked"), "test")).Diagnostic.Code);
        Assert.AreEqual(sha, Hash(source)); Query(source, "DELETE FROM __EFMigrationsLock;UPDATE FileMetadata SET SchemaVersion=99"); sha = Hash(source);
        Assert.AreEqual("FILE_SCHEMA_UNSUPPORTED", Assert.ThrowsExactly<CompanyFileException>(() => Service().Upgrade(source, FileOf("future"), "test")).Diagnostic.Code);
        Assert.AreEqual(sha, Hash(source));
    }

    [TestMethod]
    public void S01_ExistingUpgradeEventIsPreservedAndNextStepUsesRevisionOne()
    {
        var source = Fixture(2, "t04-upgraded-v2.json"); var sha = Hash(source);
        var old = Query(source, "SELECT AfterJson FROM AuditEvents WHERE AggregateType='CompanyFile' AND Revision=0");
        using var file = Service().Upgrade(source, FileOf(), "test");
        Assert.AreEqual(sha, Hash(source));
        Assert.AreEqual(old, Query(file.Path, "SELECT AfterJson FROM AuditEvents WHERE AggregateType='CompanyFile' AND Revision=0"));
        Assert.AreEqual("7", Query(file.Path, "SELECT count(*) FROM AuditEvents WHERE AggregateType='CompanyFile'"));
        Assert.AreEqual("6", Query(file.Path, "SELECT max(Revision) FROM AuditEvents WHERE AggregateType='CompanyFile'"));
    }

    [TestMethod]
    public void S_RunnerVerifiesActualReceiptWithoutDatabaseAndRejectsCorruptHash()
    {
        using var file = Baseline(); var study = file.Studies.Create(CreateCommand(file)); var receipt = file.Studies.Capture(CaptureCommand(study));
        var path = FileOf("snapshot.json"); File.WriteAllText(path, CanonicalSnapshotJson.Serialize(receipt));
        var dll = Path.Combine(AppContext.BaseDirectory, "runner", "Valdrio.Runner.dll");
        (int Code, string Output) Run()
        {
            var info = new System.Diagnostics.ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
                { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            info.ArgumentList.Add(dll); info.ArgumentList.Add("--study-snapshot"); info.ArgumentList.Add(path);
            using var p = System.Diagnostics.Process.Start(info)!; var output = p.StandardOutput.ReadToEnd(); var error = p.StandardError.ReadToEnd();
            Assert.IsTrue(p.WaitForExit(30000)); return (p.ExitCode, output + error);
        }
        var ok = Run(); Assert.AreEqual(0, ok.Code); using var doc = JsonDocument.Parse(ok.Output);
        Assert.IsFalse(doc.RootElement.GetProperty("ReadyForCalculation").GetBoolean());
        Assert.AreEqual(receipt.EconomicInputHash, doc.RootElement.GetProperty("EconomicInputHash").GetString());
        File.WriteAllText(path, CanonicalSnapshotJson.Serialize(receipt with { FullSnapshotHash = "wrong" })); Assert.AreEqual(3, Run().Code);
    }

    [TestMethod]
    public void S03_ExplicitEurStudyInUsdCompanyReopensAndRetriesWithoutDuplication()
    {
        CreateStudy command; StudyDefinition study;
        using (var file = Baseline())
        {
            Assert.AreEqual("USD", file.Read().DefaultCurrency.ToString()); command = CreateCommand(file);
            study = file.Studies.Create(command); Assert.AreEqual("EUR", study.Currency.FunctionalCurrencyCode.ToString());
            Assert.AreEqual(0L, study.Revision); Assert.AreEqual(2, study.Currency.CurrencyMetadataSnapshot.MinorUnitDigits);
            Assert.AreEqual(study, file.Studies.Create(command)); Assert.AreEqual(1, file.Studies.ReadStudies().Count);
            Assert.AreEqual("STUDY_REQUEST_REUSED", Assert.ThrowsExactly<DomainRuleException>(() => file.Studies.Create(command with { Name = "different" })).Diagnostic.Code);
        }
        using var reopened = Service().Open(FileOf()); Assert.AreEqual(study, reopened.Studies.ReadStudies().Single());
    }

    [TestMethod]
    public void S04_InvalidPeriodCurrencyPurposeAndEmptyIdentityDoNotWrite()
    {
        using var file = Baseline(); var command = CreateCommand(file); var count = Query(file.Path, "SELECT count(*) FROM AuditEvents");
        Assert.AreEqual("STUDY_PERIOD_INVALID", Assert.ThrowsExactly<DomainRuleException>(() => file.Studies.Create(command with { PeriodEnd = BusinessDate.ParseIso("2026-09-30") })).Diagnostic.Code);
        Assert.ThrowsExactly<FormatException>(() => BusinessDate.ParseIso("2026-02-30"));
        Assert.ThrowsExactly<DomainRuleException>(() => file.Studies.Create(command with { FunctionalCurrency = new("XXX") }));
        Assert.ThrowsExactly<DomainRuleException>(() => file.Studies.Create(command with { Purpose = new("inventory", "proposal") }));
        Assert.ThrowsExactly<ArgumentException>(() => file.Studies.Create(command with { Id = default }));
        Assert.AreEqual(0, file.Studies.ReadStudies().Count); Assert.AreEqual(count, Query(file.Path, "SELECT count(*) FROM AuditEvents"));
    }

    [TestMethod]
    public void S05_DraftForeignModelsAndCompositeSqlScopesAreRejected()
    {
        using var file = Baseline(); var command = CreateCommand(file); var draft = file.Models.ReadModels().First(m => m.Status == ModelStatus.Draft);
        Assert.AreEqual("STUDY_MODEL_NOT_PUBLISHED", Assert.ThrowsExactly<DomainRuleException>(() => file.Studies.Create(command with { SelectedModelVersionId = draft.Structure.Id })).Diagnostic.Code);
        Assert.ThrowsExactly<DomainRuleException>(() => file.Studies.Create(command with { CompanyId = Id() }));
        Assert.ThrowsExactly<DomainRuleException>(() => file.Studies.Create(command with { SelectedModelVersionId = Id() }));
        var study = file.Studies.Create(command);
        Assert.ThrowsExactly<SqliteException>(() => Query(file.Path, "UPDATE Studies SET CompanyId='" + Id() + "'"));
        Assert.ThrowsExactly<SqliteException>(() => Query(file.Path, "UPDATE Studies SET SelectedModelVersionId='" + draft.Structure.Id + "'"));
        Assert.AreEqual(study, file.Studies.ReadStudies().Single());
    }

    [TestMethod]
    public void S06_StudyRevisionChangesIndependentlyAndOldSnapshotStaysHistorical()
    {
        using var file = Baseline(); var study = file.Studies.Create(CreateCommand(file)); var company = file.Read();
        var receipt = file.Studies.Capture(CaptureCommand(study)); var modelJson = JsonSerializer.Serialize(file.Models.ReadModels());
        var update = UpdateCommand(study) with { PeriodStart = BusinessDate.ParseIso("2026-10-02"), Name = "new name" };
        var changed = file.Studies.UpdateContext(update); Assert.AreEqual(1L, changed.Revision);
        Assert.AreEqual(company.Revision, file.Read().Revision); Assert.AreEqual(modelJson, JsonSerializer.Serialize(file.Models.ReadModels()));
        Assert.IsFalse(receipt.IsCurrentFor(changed)); Assert.AreEqual(receipt.CanonicalJson, file.Studies.ReadSnapshots(study.CompanyId, study.Id).Single().CanonicalJson);
        var next = file.Studies.Capture(CaptureCommand(changed)); Assert.AreNotEqual(receipt.EconomicInputHash, next.EconomicInputHash);
    }

    [TestMethod]
    public void S07_StaleUpdatesAndCapturesDoNotDuplicateAuditOrSnapshots()
    {
        using var file = Baseline(); var study = file.Studies.Create(CreateCommand(file));
        _ = file.Studies.UpdateContext(UpdateCommand(study) with { Name = "revision 1" }); var count = Query(file.Path, "SELECT count(*) FROM AuditEvents");
        Assert.AreEqual("STUDY_REVISION_CHANGED", Assert.ThrowsExactly<DomainRuleException>(() => file.Studies.UpdateContext(UpdateCommand(study))).Diagnostic.Code);
        Assert.AreEqual("STUDY_REVISION_CHANGED", Assert.ThrowsExactly<DomainRuleException>(() => file.Studies.Capture(CaptureCommand(study))).Diagnostic.Code);
        Assert.AreEqual(count, Query(file.Path, "SELECT count(*) FROM AuditEvents")); Assert.AreEqual(0, file.Studies.ReadSnapshots(study.CompanyId, study.Id).Count);
    }

    [TestMethod] [DataRow("create")] [DataRow("update")] [DataRow("capture")] [DataRow("state")]
    public void S08_RealFirstWriteFailureRollsBackEverything(string operation) => Fault(operation, false);
    [TestMethod] [DataRow("create")] [DataRow("update")] [DataRow("capture")] [DataRow("state")]
    public void S09_CancellationImmediatelyBeforeCommitRollsBackEverything(string operation) => Fault(operation, true);
    private void Fault(string operation, bool cancelBeforeCommit)
    {
        StudyDefinition study; CreateStudy command;
        using (var initial = Baseline()) { command = CreateCommand(initial); study = initial.Studies.Create(command); }
        var count = Query(FileOf(), "SELECT count(*) FROM AuditEvents"); using var cancel = new CancellationTokenSource();
        using (var fault = Service(point =>
        {
            if (cancelBeforeCommit && point == CompanyWriteCheckpoint.BeforeCommit) cancel.Cancel();
            if (!cancelBeforeCommit && point == CompanyWriteCheckpoint.AfterFirstWrite) throw new IOException("after real write");
        }).Open(FileOf()))
        {
            void Mutate()
            {
                switch (operation)
                {
                    case "create": fault.Studies.Create(command with { Id = Id() }, cancel.Token); break;
                    case "update": fault.Studies.UpdateContext(UpdateCommand(study) with { Name = "changed" }, cancel.Token); break;
                    case "capture": fault.Studies.Capture(CaptureCommand(study), cancel.Token); break;
                    default: fault.Studies.ChangeState(new(study.CompanyId, study.Id, study.Revision, StudyState.Closed, "test"), cancel.Token); break;
                }
            }
            if (cancelBeforeCommit) Assert.ThrowsExactly<OperationCanceledException>(Mutate); else Assert.ThrowsExactly<CompanyFileException>(Mutate);
        }
        using var separate = Service().Open(FileOf(), CompanyFileOpenMode.ReadOnly);
        Assert.AreEqual(study, separate.Studies.ReadStudies().Single()); Assert.AreEqual(count, Query(FileOf(), "SELECT count(*) FROM AuditEvents"));
        Assert.AreEqual(0, separate.Studies.ReadSnapshots(study.CompanyId, study.Id).Count);
    }

    [TestMethod]
    public void S10_CanonicalInputOrderingAndJsonPropertyOrderAreDeterministic()
    {
        using var file = Baseline(); var study = file.Studies.Create(CreateCommand(file)); var input = file.Studies.Capture(CaptureCommand(study)).Input;
        var m = input.Model.Structure;
        var reversed = new ModelStructure(m.Id, m.CompanyId, m.FamilyId, m.VersionNumber, m.ParentVersionId, m.Description,
            m.PrecisionPolicyVersion, m.MethodPolicy, m.Nodes.Reverse().ToArray(), m.Pools.Reverse().ToArray());
        var copy = Change(input, model: new(reversed, input.Model.PhysicalCenters.Reverse().ToArray(), input.Model.DriverUnits.Reverse().ToArray()));
        Assert.AreEqual(StudySnapshotJson.Serialize(input), StudySnapshotJson.Serialize(copy));
        Assert.AreEqual(StudySnapshotJson.EconomicHash(input), StudySnapshotJson.EconomicHash(copy));
        var root = JsonNode.Parse(StudySnapshotJson.Serialize(input))!.AsObject(); var reordered = new JsonObject();
        foreach (var p in root.Reverse()) reordered.Add(p.Key, p.Value?.DeepClone());
        Assert.AreEqual(StudySnapshotJson.Serialize(input), StudySnapshotJson.Serialize(StudySnapshotJson.Deserialize(reordered.ToJsonString())));
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<ResourceSnapshotInput>)input.Resources).Add(new(Id(), new(1m, new("EUR")), new(1m, new("EUR")), null)));
    }

    [TestMethod]
    public void S11_CultureAndPresentationPreferencesNeverChangeStoredSnapshotHashes()
    {
        using var file = Baseline(); var study = file.Studies.Create(CreateCommand(file)); var receipt = file.Studies.Capture(CaptureCommand(study));
        var sha = Hash(file.Path); var old = CultureInfo.CurrentCulture;
        try
        {
            foreach (var name in new[] { "es-ES", "en-US", "de-DE", "en-GB", "tr-TR" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
                Assert.AreEqual(receipt.CanonicalJson, StudySnapshotJson.Serialize(receipt.Input));
                Assert.AreEqual(receipt.EconomicInputHash, StudySnapshotJson.EconomicHash(receipt.Input));
                Assert.AreEqual(receipt.FullSnapshotHash, PublishedModelStructure.Hash(StudySnapshotJson.Serialize(receipt.Input)));
            }
        }
        finally { CultureInfo.CurrentCulture = old; }
        Assert.AreEqual(sha, Hash(file.Path));
    }

    [TestMethod]
    public void S12_I08_PeriodDateDoesNotShiftWithUtcNearMidnight()
    {
        using var file = Baseline(); var study = file.Studies.Create(CreateCommand(file)); var receipt = file.Studies.Capture(CaptureCommand(study));
        var formatter = new PresentationFormatter(new DisplayTimeZoneResolver());
        var es = CultureInfo.GetCultureInfo("es-ES");
        var madrid = formatter.FormatInstant(receipt.CapturedAtUtc.Value, "Europe/Madrid", es);
        var la = formatter.FormatInstant(receipt.CapturedAtUtc.Value, "America/Los_Angeles", es);
        Assert.AreNotEqual(madrid, la); Assert.AreEqual("2026-10-01", receipt.Input.StudyContext.PeriodStart.ToString());
        Assert.AreEqual("2026-10-05T00:30:00.0000000Z", receipt.CapturedAtUtc.ToString());
    }

    [TestMethod]
    public void S13_PeriodScenarioCurrencyPrecisionAndModelPolicyChangeEconomicHash()
    {
        using var file = Baseline(); var study = file.Studies.Create(CreateCommand(file)); var input = file.Studies.Capture(CaptureCommand(study)).Input;
        var hash = StudySnapshotJson.EconomicHash(input); var m = input.Model.Structure;
        var usd = new StudyCurrencyContext(new("USD"), new JsonCurrencyCatalog(Path.Combine(AppContext.BaseDirectory, "reference", "currency-catalog-example.json")).GetRequired(new("USD")).Freeze());
        foreach (var changed in new[] { Change(input, Change(study, start: BusinessDate.ParseIso("2026-10-02"))),
            Change(input, Change(study, scenario: StudyScenario.Budget)), Change(input, Change(study, currency: usd)),
            Change(input, policy: input.PrecisionPolicy with { MaximumNumericCorrection = 0.00000002m }) })
            Assert.AreNotEqual(hash, StudySnapshotJson.EconomicHash(changed));
        var changedModel = new ModelStructure(m.Id, m.CompanyId, m.FamilyId, m.VersionNumber, m.ParentVersionId, m.Description,
            m.PrecisionPolicyVersion, new("different-pending-method", "proposal", PolicyResolution.Pending), m.Nodes, m.Pools);
        Assert.AreNotEqual(hash, StudySnapshotJson.EconomicHash(Change(input, model: new(changedModel, input.Model.PhysicalCenters, input.Model.DriverUnits))));
    }

    [TestMethod]
    public void S_ContextCanSelectAnotherPublishedModelAndExplicitJpyCurrency()
    {
        using var file = Baseline(); var study = file.Studies.Create(CreateCommand(file)); var first = file.Studies.Capture(CaptureCommand(study));
        foreach (var c in file.Models.ReadCenters().Where(c => !c.IsActive))
            file.Models.ChangeCenter(new(study.CompanyId, c.Id, c.Revision, c.Code, c.Name, true, "test"));
        foreach (var u in file.Models.ReadDriverUnits().Where(u => !u.IsActive))
            file.Models.ChangeUnitState(new(study.CompanyId, u.Definition.Id, u.Revision, true, "test"));
        var draft = file.Models.ReadModels().Single(m => m.Status == ModelStatus.Draft);
        var published = file.Models.Publish(new(study.CompanyId, draft.Structure.Id, draft.Revision, "test"));
        var changed = file.Studies.UpdateContext(UpdateCommand(study) with { SelectedModelVersionId = published.Structure.Id, FunctionalCurrency = new("JPY") });
        Assert.AreEqual(1L, changed.Revision); Assert.AreEqual(0, changed.Currency.CurrencyMetadataSnapshot.MinorUnitDigits);
        var photo = file.Studies.Capture(CaptureCommand(changed)); Assert.AreNotEqual(first.EconomicInputHash, photo.EconomicInputHash);
        Assert.AreEqual(study.SelectedModelVersionId, first.Input.Model.Structure.Id); Assert.AreEqual("EUR", first.Input.StudyContext.Currency.FunctionalCurrencyCode.ToString());
        Assert.AreEqual(published.Structure.Id, photo.Input.Model.Structure.Id); Assert.AreEqual("JPY", photo.Input.StudyContext.Currency.FunctionalCurrencyCode.ToString());
    }

    [TestMethod]
    public void S14_AdministrativeNamesRevisionAndProvenanceChangeOnlyFullHash()
    {
        using var file = Baseline(); var study = file.Studies.Create(CreateCommand(file)); var input = file.Studies.Capture(CaptureCommand(study)).Input;
        var changed = Change(input, Change(study, name: "other label", revision: 4), input.Provenance with { Actor = "other", CapturedAtUtc = UtcInstant.ParseIso("2026-10-06T00:00:00Z") });
        Assert.AreEqual(StudySnapshotJson.EconomicHash(input), StudySnapshotJson.EconomicHash(changed));
        Assert.AreNotEqual(PublishedModelStructure.Hash(StudySnapshotJson.Serialize(input)), PublishedModelStructure.Hash(StudySnapshotJson.Serialize(changed)));
        var labels = input.Model.PhysicalCenters.Select(c => c with { Name = "renamed" }).ToArray();
        var labelled = Change(input, model: new(input.Model.Structure, labels, input.Model.DriverUnits));
        Assert.AreEqual(StudySnapshotJson.EconomicHash(input), StudySnapshotJson.EconomicHash(labelled));
        Assert.AreNotEqual(StudySnapshotJson.Serialize(input), StudySnapshotJson.Serialize(labelled));
    }

    [TestMethod]
    public void S15_MissingUnknownDuplicateWrongTypeAndUnsupportedJsonNeverDefault()
    {
        using var file = Baseline(); var study = file.Studies.Create(CreateCommand(file)); var json = file.Studies.Capture(CaptureCommand(study)).CanonicalJson;
        var root = JsonNode.Parse(json)!.AsObject(); root["Value"]!.AsObject().Remove("Resources");
        Assert.ThrowsExactly<JsonException>(() => StudySnapshotJson.Deserialize(root.ToJsonString()));
        root = JsonNode.Parse(json)!.AsObject(); root["Value"]!["Unknown"] = 1;
        Assert.ThrowsExactly<JsonException>(() => StudySnapshotJson.Deserialize(root.ToJsonString()));
        Assert.ThrowsExactly<JsonException>(() => StudySnapshotJson.Deserialize(json.Replace("\"Resources\":[]", "\"Resources\":[],\"Resources\":[]", StringComparison.Ordinal)));
        Assert.ThrowsExactly<JsonException>(() => StudySnapshotJson.Deserialize(json.Replace("\"Resources\":[]", "\"Resources\":null", StringComparison.Ordinal)));
        Assert.ThrowsExactly<JsonException>(() => StudySnapshotJson.Deserialize(json.Replace("\"Revision\":0", "\"Revision\":\"invalid\"", StringComparison.Ordinal)));
        Assert.ThrowsExactly<JsonException>(() => StudySnapshotJson.Deserialize(json.Replace(StudySnapshotInput.Schema, "future-v99", StringComparison.Ordinal)));
        root = JsonNode.Parse(json)!.AsObject(); root["Value"]!["StudyContext"]!["Currency"]!["FunctionalCurrencyCode"] = "USD";
        Assert.ThrowsExactly<JsonException>(() => StudySnapshotJson.Deserialize(root.ToJsonString()));
    }

    [TestMethod]
    public void S16_MasterRenameAndDeactivationDoNotChangeHistoricalAppliedDefinitions()
    {
        using var file = Baseline(); var study = file.Studies.Create(CreateCommand(file)); var old = file.Studies.Capture(CaptureCommand(study));
        var center = file.Models.ReadCenters().First();
        file.Models.ChangeCenter(new(study.CompanyId, center.Id, center.Revision, center.Code, "current name", !center.IsActive, "test"));
        var next = file.Studies.Capture(CaptureCommand(study));
        Assert.AreEqual(old.EconomicInputHash, next.EconomicInputHash); Assert.AreEqual(JsonSerializer.Serialize(old.Input.Model), JsonSerializer.Serialize(next.Input.Model));
        Assert.AreEqual(old.CanonicalJson, file.Studies.ReadSnapshots(study.CompanyId, study.Id).First(s => s.Id == old.Id).CanonicalJson);
        Assert.AreEqual(0L, file.Studies.ReadStudies().Single().Revision);
    }

    [TestMethod]
    public void S17_EmptyContextIsCapturedWithReasonsAndNoFakeRunOrCost()
    {
        using var file = Baseline(); var study = file.Studies.Create(CreateCommand(file)); var request = CaptureCommand(study);
        var receipt = file.Studies.Capture(request); Assert.AreEqual(receipt, file.Studies.Capture(request));
        Assert.AreEqual(1, file.Studies.ReadSnapshots(study.CompanyId, study.Id).Count); Assert.AreEqual(0L, file.Studies.ReadStudies().Single().Revision);
        Assert.ThrowsExactly<DomainRuleException>(() => file.Studies.Capture(request with { Actor = "different" }));
        var input = receipt.Input; var readiness = StudyPreparationService.Evaluate(input);
        Assert.IsFalse(readiness.ReadyForCalculation); Assert.AreEqual(4, readiness.Diagnostics.Count); Assert.IsNull(study.CurrentRunId);
        Assert.AreEqual("0", Query(file.Path, "SELECT count(*) FROM sqlite_master WHERE name IN('CalculationRuns','TerminalCostFacts')"));
        Assert.AreEqual(0, input.Resources.Count + input.AppliedFx.Count + input.Activity.Count + input.Consumptions.Count);
    }

    [TestMethod]
    public void S19_BackupRestoreContainsExactStudySnapshotAndAudit()
    {
        using var file = Baseline(); var study = file.Studies.Create(CreateCommand(file)); var receipt = file.Studies.Capture(CaptureCommand(study));
        file.Backup(FileOf("backup")); using var restored = Service().Restore(FileOf("backup"), FileOf("restored"));
        Assert.AreEqual(study, restored.Studies.ReadStudies().Single()); Assert.AreEqual(receipt, restored.Studies.ReadSnapshots(study.CompanyId, study.Id).Single());
        Assert.AreEqual(Query(file.Path, "SELECT count(*) FROM AuditEvents"), Query(restored.Path, "SELECT count(*) FROM AuditEvents"));
    }

    [TestMethod]
    public void S_ImmutableSnapshotTriggersAuditTamperReadOnlyAndSessionLifetime()
    {
        StudyDefinition study; StudySnapshotReceipt receipt;
        using (var file = Baseline())
        {
            study = file.Studies.Create(CreateCommand(file)); receipt = file.Studies.Capture(CaptureCommand(study));
            Assert.ThrowsExactly<SqliteException>(() => Query(file.Path, "UPDATE StudySnapshots SET CanonicalJson='{}'"));
            Assert.ThrowsExactly<SqliteException>(() => Query(file.Path, "DELETE FROM StudySnapshots"));
            Assert.ThrowsExactly<SqliteException>(() => Query(file.Path, "UPDATE Studies SET CurrentRunId='fake'"));
        }
        using (var read = Service().Open(FileOf(), CompanyFileOpenMode.ReadOnly))
        {
            Assert.ThrowsExactly<CompanyFileException>(() => read.Studies.Capture(CaptureCommand(study)));
            Assert.ThrowsExactly<CompanyFileException>(() => read.Studies.UpdateContext(UpdateCommand(study)));
            read.Dispose(); Assert.ThrowsExactly<ObjectDisposedException>(() => read.Studies.ReadStudies());
        }
        Query(FileOf(), "UPDATE AuditEvents SET AfterJson='{}' WHERE AggregateType='StudySnapshot'"); var sha = Hash(FileOf());
        Assert.AreEqual("FILE_INVALID_DATA", Assert.ThrowsExactly<CompanyFileException>(() => Service().Open(FileOf())).Diagnostic.Code);
        Assert.AreEqual(sha, Hash(FileOf()));
    }

    [TestMethod]
    public void S_CloseReopenAndAdministrativeRenameHaveExplicitRevisionSemantics()
    {
        using var file = Baseline(); var study = file.Studies.Create(CreateCommand(file)); var original = file.Studies.Capture(CaptureCommand(study));
        var renamed = file.Studies.UpdateContext(UpdateCommand(study) with { Name = "renamed" });
        var photo = file.Studies.Capture(CaptureCommand(renamed)); Assert.AreEqual(original.EconomicInputHash, photo.EconomicInputHash);
        var closed = file.Studies.ChangeState(new(study.CompanyId, study.Id, renamed.Revision, StudyState.Closed, "test"));
        Assert.AreEqual("STUDY_CLOSED", Assert.ThrowsExactly<DomainRuleException>(() => file.Studies.UpdateContext(UpdateCommand(closed))).Diagnostic.Code);
        var reopened = file.Studies.ChangeState(new(study.CompanyId, study.Id, closed.Revision, StudyState.Open, "test"));
        Assert.AreEqual(3L, reopened.Revision); Assert.AreEqual(original.EconomicInputHash, file.Studies.Capture(CaptureCommand(reopened)).EconomicInputHash);
    }
}
