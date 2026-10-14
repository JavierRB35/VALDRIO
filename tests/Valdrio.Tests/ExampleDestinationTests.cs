using Valdrio.Desktop;
using Valdrio.Localization;

namespace Valdrio.Tests;

public sealed partial class T06ImportConfirmationTests
{
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
