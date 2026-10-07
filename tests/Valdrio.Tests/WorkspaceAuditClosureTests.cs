using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Valdrio.Application;
using Valdrio.Desktop;
using Valdrio.Domain;
using Valdrio.Infrastructure;
using Valdrio.Infrastructure.CompanyFiles;
using Valdrio.Localization;

namespace Valdrio.Tests;

[TestClass]
public sealed class WorkspaceAuditClosureTests
{
    private string directory = null!;
    private ICurrencyCatalog Catalog => new JsonCurrencyCatalog(Path.Combine(
        AppContext.BaseDirectory, "reference", "currency-catalog-example.json"));
    private SqliteCompanyFileService Service() => new(Catalog, new SystemClock());

    [TestInitialize]
    public void Init()
    {
        directory = Path.Combine(Path.GetTempPath(), "valdrio-closure-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(directory, true);

    private string Baseline()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "t04-v2.json")));
        using var compressed = new MemoryStream(Convert.FromBase64String(json.RootElement.GetProperty("GzipBase64").GetString()!));
        using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
        var source = Path.Combine(directory, "v2.valdrio");
        using (var output = File.Create(source)) gzip.CopyTo(output);
        var upgraded = Path.Combine(directory, "current.valdrio");
        using var session = Service().Upgrade(source, upgraded, "audit-test");
        return upgraded;
    }

    [TestMethod]
    public async Task ImportPreviewPreservesUnsavedStudyAndCannotConfirmWithoutTarget()
    {
        var preferences = new PresentationPreferencesService(new LanguageCatalog(), UserPresentationPreferences.Default);
        var formatter = new PresentationFormatter(new DisplayTimeZoneResolver());
        var texts = new UiTextCatalog();
        var dialogs = new Dialogs();
        using var company = new CompanyFileViewModel(Service(), dialogs, Catalog, texts, preferences, formatter);
        using var imports = new ImportColumnMappingViewModel(new ImportSourceSessionFactory(), new PreviewDialogs(), texts, preferences);
        using var studies = new StudyWorkspaceViewModel(company, Catalog, texts, preferences, formatter, new NoPromptDialogs()) { ImportColumns = imports };
        await company.OpenPath(Baseline());
        studies.Name = "Pending study";
        studies.PeriodStart = new DateTime(2026, 1, 1);
        Assert.IsTrue(await studies.AttachImportTarget());
        Assert.AreEqual("Pending study", studies.Name);
        Assert.IsTrue(studies.IsDirty);
        Assert.IsNull(studies.Current);
        Assert.IsFalse(imports.PrepareCommand.CanExecute(null));
        Assert.IsFalse(imports.ConfirmCommand.CanExecute(null));
        Assert.IsTrue(imports.OpenCommand.CanExecute(null));
        Assert.IsFalse(studies.HasStudies);
        Assert.IsTrue(studies.HasPublishedModels);
    }
    private sealed class PreviewDialogs : IImportSourceDialogs { public string? SelectSource() => null; }
    private sealed class NoPromptDialogs : IModelEditorDialogs
    {
        public PendingEditChoice ResolvePending(string title, string body) => throw new AssertFailedException("Preview must not ask to save or discard the study.");
    }

    [TestMethod]
    [DataRow("CreateCenter")]
    [DataRow("ChangeCenter")]
    [DataRow("ToggleCenter")]
    [DataRow("ChangeUnitState")]
    [DataRow("CreateDraft")]
    [DataRow("SaveDraft")]
    [DataRow("Publish")]
    [DataRow("Clone")]
    public async Task E02_AllModelAndMasterCommitsRemainVisibleAfterRefreshFailure(string operation)
    {
        var wrapper = new FaultService(Service());
        var preferences = new PresentationPreferencesService(new LanguageCatalog(), UserPresentationPreferences.Default);
        var formatter = new PresentationFormatter(new DisplayTimeZoneResolver());
        var texts = new UiTextCatalog();
        var dialogs = new Dialogs();
        using var company = new CompanyFileViewModel(wrapper, dialogs, Catalog, texts, preferences, formatter);
        using var vm = new ModelWorkspaceViewModel(company, texts, preferences, dialogs, formatter);
        await company.OpenPath(Baseline());
        var session = wrapper.Last!;
        var before = session.Inner.Models.ReadWorkspace();
        var command = operation switch
        {
            "CreateCenter" => vm.CreateCenterCommand,
            "ChangeCenter" => vm.SaveCenterCommand,
            "ToggleCenter" => vm.ToggleCenterCommand,
            "ChangeUnitState" => vm.ToggleUnitCommand,
            "CreateDraft" => vm.NewDraftCommand,
            "SaveDraft" => vm.SaveCommand,
            "Publish" => vm.PublishCommand,
            _ => vm.CloneCommand
        };
        if (operation == "CreateCenter") { vm.CenterCode = "AUDIT-NEW"; vm.CenterName = "Confirmed center"; }
        else if (operation is "ChangeCenter" or "ToggleCenter")
        {
            vm.Master = vm.Centers.First();
            if (operation == "ChangeCenter") vm.CenterName += " confirmed";
        }
        else if (operation == "ChangeUnitState") vm.UnitMaster = vm.Units.First();
        else if (operation == "CreateDraft") vm.Description = "Confirmed draft";
        else
        {
            vm.ModelChoice = vm.Models.First(m => m.Value.Status == (operation == "Clone" ? ModelStatus.Published : ModelStatus.Draft));
            await vm.LoadCommand.ExecuteAsync(null);
            if (operation == "SaveDraft") vm.Description += " confirmed";
        }

        Assert.IsTrue(command.CanExecute(null), operation);
        session.FailAfterMethod = operation == "ToggleCenter" ? "ChangeCenter" : operation;
        var writesBefore = session.Writes;
        await command.ExecuteAsync(null);
        var persisted = session.Inner.Models.ReadWorkspace();
        Assert.AreEqual(writesBefore + 1, session.Writes);
        Assert.IsTrue(vm.HasPendingRefresh);
        Assert.AreEqual(texts.Get("Model.CommittedRefreshPending", CultureInfo.GetCultureInfo("en")), vm.Status);
        Assert.IsFalse(vm.IsDirty, operation);
        if (operation is "CreateCenter" or "ChangeCenter" or "ToggleCenter")
        {
            Assert.IsNotNull(vm.Master);
            Assert.AreEqual(persisted.Centers.Single(c => c.Id == vm.Master.Value.Id), vm.Master.Value);
            Assert.AreEqual(vm.Master.Value.Name, vm.CenterName);
        }
        else if (operation == "ChangeUnitState")
        {
            Assert.IsNotNull(vm.UnitMaster);
            var unit = persisted.DriverUnits.Single(u => u.Definition.Id == vm.UnitMaster.Value.Definition.Id);
            Assert.AreEqual(unit.Revision, vm.UnitMaster.Value.Revision);
            Assert.AreEqual(unit.IsActive, vm.UnitMaster.Value.IsActive);
        }
        else
        {
            Assert.IsNotNull(vm.Current);
            var model = persisted.Models.Single(m => m.Structure.Id == vm.Current.Structure.Id);
            Assert.AreEqual(model.Revision, vm.Current.Revision);
            Assert.AreEqual(model.Status, vm.Current.Status);
            Assert.AreEqual(model.Structure.Description, vm.Description);
        }
        if (operation == "CreateDraft") Assert.AreEqual(before.Models.Count + 1, persisted.Models.Count);
        if (operation == "CreateCenter") Assert.AreEqual(before.Centers.Count + 1, persisted.Centers.Count);

        // Recovery retries only the read, preserving the already committed write.
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.IsFalse(vm.HasPendingRefresh);
        Assert.AreEqual(writesBefore + 1, session.Writes);
        Assert.AreEqual(persisted.Models.Count, vm.Models.Count);
        Assert.AreEqual(persisted.Centers.Count, vm.Centers.Count);
        Assert.IsFalse(vm.Status.Contains("MODEL_REVISION_CHANGED", StringComparison.Ordinal));
        Assert.AreEqual(0, session.IndividualReads, "The actual SQLite snapshot read replaces three separately validated reads.");
    }

    [TestMethod]
    public async Task E02_RefreshPreservesPendingModelNodeAndMasterEditsWithoutRebasingTheirRevision()
    {
        var wrapper = new FaultService(Service());
        var preferences = new PresentationPreferencesService(new LanguageCatalog(), UserPresentationPreferences.Default);
        var formatter = new PresentationFormatter(new DisplayTimeZoneResolver());
        var dialogs = new Dialogs();
        using var company = new CompanyFileViewModel(wrapper, dialogs, Catalog, new UiTextCatalog(), preferences, formatter);
        using var vm = new ModelWorkspaceViewModel(company, new UiTextCatalog(), preferences, dialogs, formatter);
        await company.OpenPath(Baseline());
        vm.ModelChoice = vm.Models.Single(m => m.Value.Status == ModelStatus.Draft);
        await vm.LoadCommand.ExecuteAsync(null);
        vm.Description = "Pending description";
        vm.Master = vm.Centers.First();
        var center = vm.Master.Value;
        vm.CenterName = "Pending center";
        vm.SelectedNode = vm.Nodes.First();
        vm.NodeCode = "PENDING-NODE";
        var modelRevision = vm.Current!.Revision;
        var session = wrapper.Last!;
        session.Inner.Models.ChangeCenter(new(center.CompanyId, center.Id, center.Revision, center.Code, "External center", center.IsActive, "test"));
        var external = session.Inner.Models.ReadModels().Single(m => m.Structure.Id == vm.Current.Structure.Id);
        var structure = external.Structure;
        session.Inner.Models.SaveDraft(new(new ModelStructure(structure.Id, structure.CompanyId, structure.FamilyId,
            structure.VersionNumber, structure.ParentVersionId, "External description", structure.PrecisionPolicyVersion,
            structure.MethodPolicy, structure.Nodes, structure.Pools), external.Revision, "test"));

        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.AreEqual("Pending description", vm.Description);
        Assert.AreEqual("Pending center", vm.CenterName);
        Assert.AreEqual("PENDING-NODE", vm.NodeCode);
        Assert.AreEqual(modelRevision, vm.Current.Revision);
        Assert.AreEqual(center.Revision, vm.Master!.Value.Revision);
        Assert.IsTrue(vm.IsDirty);
        await vm.SaveCenterCommand.ExecuteAsync(null);
        Assert.IsTrue(vm.Status.StartsWith("MODEL_REVISION_CHANGED", StringComparison.Ordinal));
        Assert.AreEqual("Pending center", vm.CenterName);
    }

    [TestMethod]
    public async Task M02_CleanRefreshReconcilesMasterFieldsAndModelRevision()
    {
        var wrapper = new FaultService(Service());
        var preferences = new PresentationPreferencesService(new LanguageCatalog(), UserPresentationPreferences.Default);
        var formatter = new PresentationFormatter(new DisplayTimeZoneResolver());
        var dialogs = new Dialogs();
        using var company = new CompanyFileViewModel(wrapper, dialogs, Catalog, new UiTextCatalog(), preferences, formatter);
        using var vm = new ModelWorkspaceViewModel(company, new UiTextCatalog(), preferences, dialogs, formatter);
        await company.OpenPath(Baseline());
        vm.ModelChoice = vm.Models.Single(m => m.Value.Status == ModelStatus.Draft);
        await vm.LoadCommand.ExecuteAsync(null);
        vm.Master = vm.Centers.First();
        var center = vm.Master.Value;
        var real = wrapper.Last!.Inner.Models;
        real.ChangeCenter(new(center.CompanyId, center.Id, center.Revision, center.Code, "External clean", center.IsActive, "test"));
        var current = vm.Current!;
        var changed = real.SaveDraft(new(current.Structure, current.Revision, "test"));
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.AreEqual("External clean", vm.CenterName);
        Assert.AreEqual(center.Revision + 1, vm.Master!.Value.Revision);
        Assert.AreEqual(changed.Revision, vm.Current!.Revision);
        Assert.IsFalse(vm.IsDirty);
    }

    [TestMethod]
    public void E07_AuditOperationCannotDisguiseAStudyStateTransition()
    {
        var path = Baseline();
        StudyDefinition study;
        using (var file = Service().Open(path))
        {
            study = file.Studies.Create(new(file.Read().Id, new EntityId(Guid.NewGuid()), "Audit study",
                BusinessDate.ParseIso("2026-10-01"), BusinessDate.ParseIso("2026-10-31"), StudyPurpose.Management,
                StudyScenario.Actual, file.Models.ReadModels().Single(m => m.Status == ModelStatus.Published).Structure.Id,
                new CurrencyCode("EUR"), "test"));
            study = file.Studies.ChangeState(new(study.CompanyId, study.Id, study.Revision, StudyState.Closed, "test"));
        }
        using (var legitimate = Service().Open(path, CompanyFileOpenMode.ReadOnly))
            Assert.AreEqual(StudyState.Closed, legitimate.Studies.ReadStudies().Single().State);
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()))
        {
            db.Open(); using var command = db.CreateCommand();
            command.CommandText = "UPDATE AuditEvents SET Operation='UpdateStudyContext' WHERE AggregateType='Study' AND AggregateId=$id AND Revision=$revision";
            command.Parameters.AddWithValue("$id", study.Id.ToString());
            command.Parameters.AddWithValue("$revision", study.Revision);
            Assert.AreEqual(1, command.ExecuteNonQuery());
        }
        Assert.AreEqual("FILE_INVALID_DATA", Assert.ThrowsExactly<CompanyFileException>(() =>
            Service().Open(path, CompanyFileOpenMode.ReadOnly)).Diagnostic.Code);
    }

    [TestMethod]
    public void E07_StudyCreationAuditCannotStartClosed()
    {
        var path = Baseline();
        StudyDefinition study;
        using (var file = Service().Open(path))
            study = file.Studies.Create(new(file.Read().Id, new EntityId(Guid.NewGuid()), "Creation audit",
                BusinessDate.ParseIso("2026-10-01"), BusinessDate.ParseIso("2026-10-31"), StudyPurpose.Management,
                StudyScenario.Actual, file.Models.ReadModels().Single(m => m.Status == ModelStatus.Published).Structure.Id,
                new CurrencyCode("EUR"), "test"));
        var closed = new StudyDefinition(study.Id, study.CompanyId, study.Name, study.PeriodStart, study.PeriodEnd,
            study.Purpose, study.ScenarioKind, null, study.SelectedModelVersionId, study.Currency, study.CurrencyValidity,
            study.Revision, StudyState.Closed, null, null, study.CreatedAtUtc);
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()))
        {
            db.Open(); using var command = db.CreateCommand();
            command.CommandText = "UPDATE Studies SET State='Closed' WHERE Id=$id; UPDATE AuditEvents SET AfterJson=$after WHERE AggregateType='Study' AND AggregateId=$id AND Revision=0";
            command.Parameters.AddWithValue("$id", study.Id.ToString());
            command.Parameters.AddWithValue("$after", CanonicalSnapshotJson.Serialize(closed));
            Assert.AreEqual(2, command.ExecuteNonQuery());
        }
        Assert.AreEqual("FILE_INVALID_DATA", Assert.ThrowsExactly<CompanyFileException>(() =>
            Service().Open(path, CompanyFileOpenMode.ReadOnly)).Diagnostic.Code);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void M03_FailedPreferenceSaveIsVisibleTranslatedAndRetryable(bool accessDenied)
    {
        var store = new RetryStore { Failure = accessDenied ? new UnauthorizedAccessException("test") : new IOException("test") };
        var preferences = new PresentationPreferencesService(new LanguageCatalog(), UserPresentationPreferences.Default);
        var texts = new UiTextCatalog();
        var vm = new MainWindowViewModel(texts, new LanguageCatalog(), preferences,
            new PresentationFormatter(new DisplayTimeZoneResolver()), new T02RoundingExampleService(),
            new CurrencyMetadataExampleService(Catalog), new TemporalContractExampleService(new TimeZoneResolver()),
            new FxConversionExampleService(), new PresentationPreferencesPersistence(store));
        var laterSubscriber = 0;
        preferences.Changed += (_, _) => laterSubscriber++;
        vm.SelectedUiLanguageTag = "es";
        Assert.AreEqual("es", preferences.Current.UiLanguageTag);
        Assert.AreEqual(1, laterSubscriber);
        Assert.IsTrue(vm.ShowPreferencesSaveWarning);
        Assert.AreEqual(texts.Get("Preferences.SaveFailed", CultureInfo.GetCultureInfo("es")), vm.PreferencesSaveWarning);
        Assert.IsTrue(vm.RetrySavePreferencesCommand.CanExecute(null));
        store.Failure = null;
        vm.RetrySavePreferencesCommand.Execute(null);
        Assert.AreEqual(preferences.Current, store.Saved);
        Assert.IsFalse(vm.ShowPreferencesSaveWarning);
        Assert.AreEqual("", vm.PreferencesSaveWarning);
        Assert.IsFalse(vm.RetrySavePreferencesCommand.CanExecute(null));
    }

    private sealed class RetryStore : IUserPresentationPreferencesStore
    {
        public Exception? Failure { get; set; }
        public UserPresentationPreferences? Saved { get; private set; }
        public bool TryLoad(out UserPresentationPreferences? value) { value = Saved; return value is not null; }
        public bool TryLoad(out UserPresentationPreferences? value, out string? reason)
        { reason = null; return TryLoad(out value); }
        public void Save(UserPresentationPreferences value)
        {
            if (Failure is not null) ExceptionDispatchInfo.Capture(Failure).Throw();
            Saved = value;
        }
    }

    private sealed class Dialogs : ICompanyFileDialogs, IModelEditorDialogs
    {
        public PendingEditChoice ResolvePending(string title, string body) => PendingEditChoice.Discard;
        public string? SelectNewFile() => null;
        public string? SelectFile() => null;
        public string? SelectBackupDestination() => null;
        public string? SelectBackupSource() => null;
        public string? SelectRestoreDestination() => null;
    }

    public class FaultProxy : DispatchProxy
    {
        public IModelWorkspace Target = null!;
        public FaultSession Session = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            var name = method!.Name;
            if (name == nameof(IModelWorkspace.ReadWorkspace) && Session.FailRead)
            {
                Session.FailRead = false;
                throw new CompanyFileException("FILE_IO_FAILED", "File.IoFailed");
            }
            if (name is "ReadCenters" or "ReadDriverUnits" or "ReadModels") Session.IndividualReads++;
            try
            {
                var value = method.Invoke(Target, args);
                if (name is "CreateCenter" or "ChangeCenter" or "ChangeUnitState" or "CreateDraft" or "SaveDraft" or "Publish" or "Clone")
                    Session.Writes++;
                if (name == Session.FailAfterMethod) { Session.FailRead = true; Session.FailAfterMethod = null; }
                return value;
            }
            catch (TargetInvocationException error) when (error.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw;
            }
        }
    }

    public sealed class FaultSession : ICompanyFileSession
    {
        public IImportWorkspace Imports => Inner.Imports;
        public ICompanyFileSession Inner { get; }
        public string? FailAfterMethod { get; set; }
        public bool FailRead { get; set; }
        public int Writes { get; set; }
        public int IndividualReads { get; set; }
        public FaultSession(ICompanyFileSession inner)
        {
            Inner = inner;
            Models = DispatchProxy.Create<IModelWorkspace, FaultProxy>();
            var proxy = (FaultProxy)Models; proxy.Target = inner.Models; proxy.Session = this;
        }
        public IModelWorkspace Models { get; }
        public IStudyWorkspace Studies => Inner.Studies;
        public string Path => Inner.Path;
        public CompanyFileOpenMode Mode => Inner.Mode;
        public CompanyFileSnapshot Read(CancellationToken token = default) => Inner.Read(token);
        public CompanyFileSnapshot AddUnits(IEnumerable<UnitDefinition> units, long revision, string actor, CancellationToken token = default) =>
            Inner.AddUnits(units, revision, actor, token);
        public void Backup(string destination, CancellationToken token = default) => Inner.Backup(destination, token);
        public void Dispose() => Inner.Dispose();
    }

    private sealed class FaultService(ICompanyFileService inner) : ICompanyFileService
    {
        public FaultSession? Last { get; private set; }
        private ICompanyFileSession Wrap(ICompanyFileSession value) => Last = new(value);
        public ICompanyFileSession Open(string path, CompanyFileOpenMode mode = CompanyFileOpenMode.Edit, CancellationToken cancellationToken = default) =>
            Wrap(inner.Open(path, mode, cancellationToken));
        public ICompanyFileSession Create(CreateCompanyFile command, CancellationToken token = default) => Wrap(inner.Create(command, token));
        public ICompanyFileSession Restore(string source, string destination, CancellationToken token = default) => Wrap(inner.Restore(source, destination, token));
        public ICompanyFileSession Upgrade(string source, string destination, string actor, CancellationToken token = default) => Wrap(inner.Upgrade(source, destination, actor, token));
    }
}
