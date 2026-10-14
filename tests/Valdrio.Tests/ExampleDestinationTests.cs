using Valdrio.Desktop;
using Valdrio.Localization;
using Valdrio.Infrastructure.CompanyFiles;

namespace Valdrio.Tests;

public sealed partial class T06ImportConfirmationTests
{
    [TestMethod]
    public async Task ExampleOpensBundledV9AsEditableV10WithoutChangingTemplate()
    {
        var template = Path.Combine(AppContext.BaseDirectory, "examples", "services.valdrio");
        Assert.IsTrue(File.Exists(template), "The actual distributed example must be included in the test output.");
        var originalHash = Hash(template);
        Assert.IsTrue(Service().RequiresUpgrade(template), "Bundled v9 example must be exercised as legacy input.");

        var copy = FileOf("services-example-upgraded.valdrio");
        var dialogs = new QueuedCompanyFileDialogs();
        dialogs.Paths.Enqueue(copy);
        var preferences = new PresentationPreferencesService(new LanguageCatalog(), UserPresentationPreferences.Default);
        using var vm = new CompanyFileViewModel(Service(), dialogs, Catalog, new UiTextCatalog(), preferences,
            new PresentationFormatter(new DisplayTimeZoneResolver()));
        await vm.OpenExampleCommand.ExecuteAsync(null);

        Assert.IsNotNull(vm.Snapshot, vm.Status);
        Assert.IsNull(vm.DiagnosticCode, vm.Status);
        Assert.AreEqual(SqliteCompanyFileService.SchemaVersion, vm.Snapshot.SchemaVersion);
        Assert.AreEqual(copy, vm.ActivePath);
        Assert.IsTrue(vm.CanWrite, "The upgraded example must be editable.");
        Assert.IsTrue(vm.ActiveStudies!.ReadStudies().Count > 0, "Example analyses must remain available.");
        Assert.IsFalse(Service().RequiresUpgrade(copy));
        Assert.AreEqual(originalHash, Hash(template), "Bundled template must remain byte-for-byte unchanged.");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExampleRejectsExistingDestinationAndKeepsCurrentProject(bool directory)
    {
        using (var baseline = Baseline()) { }
        var current = FileOf("current.valdrio");
        var destination = FileOf("existing-example.valdrio");
        if (directory) Directory.CreateDirectory(destination);
        else File.WriteAllText(destination, "Existing user data must survive");
        var dialogs = new QueuedCompanyFileDialogs();
        dialogs.Paths.Enqueue(destination);
        var preferences = new PresentationPreferencesService(new LanguageCatalog(), UserPresentationPreferences.Default);
        using var vm = new CompanyFileViewModel(Service(), dialogs, Catalog, new UiTextCatalog(), preferences,
            new PresentationFormatter(new DisplayTimeZoneResolver()));
        await vm.OpenPath(current);
        Assert.IsNotNull(vm.Snapshot, vm.DiagnosticCode);
        var id = vm.Snapshot!.Id;
        await vm.OpenExampleCommand.ExecuteAsync(null);
        Assert.AreEqual("FILE_ALREADY_EXISTS", vm.DiagnosticCode);
        Assert.AreEqual(current, vm.ActivePath);
        Assert.AreEqual(id, vm.Snapshot!.Id);
        if (directory) Assert.IsTrue(Directory.Exists(destination));
        else Assert.AreEqual("Existing user data must survive", File.ReadAllText(destination));
        Assert.IsTrue(vm.OpenExampleCommand.CanExecute(null));
        dialogs.Paths.Enqueue(null);
        await vm.OpenExampleCommand.ExecuteAsync(null);
        Assert.AreEqual(current, vm.ActivePath);
    }
}
