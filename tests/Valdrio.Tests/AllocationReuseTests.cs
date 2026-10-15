using Valdrio.Application;
using Valdrio.Calculation;
using Valdrio.Domain;
using Valdrio.Infrastructure.CompanyFiles;
namespace Valdrio.Tests;

public sealed partial class T06ImportConfirmationTests
{
    [TestMethod]
    public async Task RP01_EditorRequiresPreviewAndSelectionAndInvalidatesWhenSourceChanges()
    {
        using var file = Baseline();
        var first = await ImportedResources(file);
        var key = file.Allocations.SaveAllocationKey(KeyCommand(first)).After;
        var rule = new PrimaryRule(Id(), first.Study.CompanyId, first.Model.Structure.Id,
            AccountSelectorKind.Prefix, "6", 1, key.Id, true);
        file.Allocations.ChangeAllocations(AllocationCommand(first, new([rule], [], [], [], [])));
        var second = await ImportedResources(file);
        var editor = new Valdrio.Desktop.AllocationReuseEditor(file.Allocations, file.Studies, second.Study,
            System.Globalization.CultureInfo.GetCultureInfo("es"));
        Assert.IsFalse(editor.CanAdopt);
        Assert.IsFalse(editor.CanPreview);
        editor.Source = editor.Sources.Single(s => s.Value.Id == first.Study.Id);
        Assert.IsTrue(editor.CanPreview);
        editor.Preview();
        Assert.IsFalse(editor.CanAdopt);
        editor.Items.Single().Selected = true;
        Assert.IsTrue(editor.CanAdopt);
        Assert.IsTrue(editor.Adopt());
        Assert.IsNotNull(editor.Receipt);
        editor.Source = null;
        Assert.IsFalse(editor.CanAdopt);
        Assert.AreEqual(0, editor.Items.Count);
    }

    private static AllocationReuseSelection SelectReuse(AllocationWorkspaceSnapshot source, params EntityId[] ids) =>
        new(source.Resources.Study.Id, source.Resources.Study.Revision, AllocationReuse.Fingerprint(source), ids, [], false);
    private static ChangeAllocations ReuseCommand(AllocationWorkspaceSnapshot source, AllocationWorkspaceSnapshot target, AllocationReuseSelection selection) =>
        new(target.Resources.Study.CompanyId, target.Resources.Study.Id, target.Resources.Study.Revision, Id(), "test",
            AllocationReuse.Adopt(source, target, selection)) { Reuse = selection };

    [TestMethod]
    public async Task RP01_AdoptionResolvesNewPrefixAccountsWithoutCopyingClosuresAndReopensWithAudit()
    {
        string path;
        using (var file = Baseline())
        {
            path = file.Path;
            var january = await ImportedResources(file);
            var key = file.Allocations.SaveAllocationKey(KeyCommand(january)).After;
            var rule = new PrimaryRule(Id(), january.Study.CompanyId, january.Model.Structure.Id,
                AccountSelectorKind.Prefix, "6", 1, key.Id, true);
            file.Allocations.ChangeAllocations(new(january.Study.CompanyId, january.Study.Id, january.Study.Revision,
                Id(), "test", new([rule], [], [], [], [])));
            var source = file.Allocations.ReadAllocations(january.Study.CompanyId, january.Study.Id);
            var february = await ImportedResources(file, "Amount;Ignored;Account\n2000;x;6000\n100;x;6010\n0;x;6020\n-100;x;6030");
            var target = file.Allocations.ReadAllocations(february.Study.CompanyId, february.Study.Id);
            Assert.IsTrue(target.Resources.Preparation.HasPendingParts);
            Assert.IsFalse(PrimaryAllocationService.Calculate(target).IsComplete);
            var beforeSource = CanonicalSnapshotJson.Serialize(source);
            var command = ReuseCommand(source, target, SelectReuse(source, rule.Id));
            var receipt = file.Allocations.ChangeAllocations(command);
            Assert.AreEqual(CanonicalSnapshotJson.Serialize(receipt), CanonicalSnapshotJson.Serialize(file.Allocations.ChangeAllocations(command)));
            var adopted = file.Allocations.ReadAllocations(february.Study.CompanyId, february.Study.Id);
            Assert.AreEqual(1, adopted.Configuration.PrimaryRules.Count);
            Assert.AreEqual(0, adopted.Configuration.ExplicitAssignments.Count);
            Assert.IsFalse(adopted.Configuration.PrimaryClosed);
            Assert.IsTrue(PrimaryAllocationService.Calculate(adopted).IsComplete);
            Assert.IsTrue(adopted.Coverage.Where(c => c.ComponentGroup == "").All(c => c.Diagnostic is null && c.KeyId == key.Id));
            Assert.IsTrue(adopted.Coverage.Any(c => c.ComponentGroup == "all" && c.Diagnostic is not null));
            Assert.AreEqual(beforeSource, CanonicalSnapshotJson.Serialize(file.Allocations.ReadAllocations(january.Study.CompanyId, january.Study.Id)));
            var again = ReuseCommand(source, adopted, SelectReuse(source, rule.Id));
            file.Allocations.ChangeAllocations(again);
            Assert.AreEqual(1, file.Allocations.ReadAllocations(february.Study.CompanyId, february.Study.Id).Configuration.PrimaryRules.Count);
            Assert.AreEqual(january.Study.Id, file.Allocations.ReadAllocationHistory(february.Study.CompanyId, february.Study.Id)[0].Request.Reuse!.SourceStudyId);
        }
        using (var reopened = Service().Open(path))
            Assert.AreEqual(3, reopened.Studies.ReadStudies().Count); // baseline + source + destination
        if (Environment.GetEnvironmentVariable("VALDRIO_T14_UI_DIR") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            File.Copy(path, Path.Combine(output, "reuse-demo.valdrio"), true);
        }
    }

    [TestMethod]
    public async Task RP01_StalePreviewAndTargetConflictNeverOverwriteAndMonthlyDriversNeedReview()
    {
        using var file = Baseline();
        var w = await ImportedResources(file);
        var key = file.Allocations.SaveAllocationKey(KeyCommand(w)).After;
        var driver = file.Allocations.SaveAllocationKey(KeyCommand(w, AllocationKeyMode.DriverUnits)).After;
        var rule = new PrimaryRule(Id(), w.Study.CompanyId, w.Model.Structure.Id, AccountSelectorKind.Prefix, "6", 1, key.Id, true);
        var measured = rule with { Id = Id(), SelectorValue = "7", KeyVersionId = driver.Id };
        file.Allocations.ChangeAllocations(new(w.Study.CompanyId, w.Study.Id, w.Study.Revision, Id(), "test", new([rule, measured], [], [], [], [])));
        var source = file.Allocations.ReadAllocations(w.Study.CompanyId, w.Study.Id);
        var other = await ImportedResources(file);
        var target = file.Allocations.ReadAllocations(other.Study.CompanyId, other.Study.Id);
        Assert.AreEqual("Driver", AllocationReuse.Preview(source, target).Items.Single(i => i.RuleId == measured.Id).Status);
        Assert.ThrowsExactly<DomainRuleException>(() => ReuseCommand(source, target, SelectReuse(source, measured.Id)));
        var stale = ReuseCommand(source, target, SelectReuse(source, rule.Id));
        file.Allocations.ChangeAllocations(new(w.Study.CompanyId, w.Study.Id, source.Resources.Study.Revision, Id(), "test", AllocationConfiguration.Empty));
        Assert.AreEqual("REUSE_SOURCE_CHANGED", Assert.ThrowsExactly<DomainRuleException>(() => file.Allocations.ChangeAllocations(stale)).Diagnostic.Code);
        Assert.AreEqual(0, file.Allocations.ReadAllocationHistory(other.Study.CompanyId, other.Study.Id).Count);
    }

    [TestMethod]
    public async Task RP01_ConflictRequiresConsentSecondaryRoutesAreReusedAndClosedTargetsReject()
    {
        using var file = Baseline();
        var w = await ImportedResources(file);
        var key = file.Allocations.SaveAllocationKey(KeyCommand(w)).After;
        var alternate = file.Allocations.SaveAllocationKey(KeyCommand(w)).After;
        var secondaryKey = file.Allocations.SaveAllocationKey(KeyCommand(w, stage: AllocationStage.Secondary)).After;
        var rule = new PrimaryRule(Id(), w.Study.CompanyId, w.Model.Structure.Id, AccountSelectorKind.Prefix, "6", 1, key.Id, true);
        var route = new SecondaryRule(Id(), w.Study.CompanyId, w.Model.Structure.Id,
            w.Model.Structure.Nodes.First(n => n.Role == NodeRole.Auxiliary).Id, "all", secondaryKey.Id, true);
        file.Allocations.ChangeAllocations(AllocationCommand(w, new([rule], [route], [], [], [])));
        var source = file.Allocations.ReadAllocations(w.Study.CompanyId, w.Study.Id);
        var other = await ImportedResources(file);
        var existingRule = rule with { Id = Id(), KeyVersionId = alternate.Id };
        file.Allocations.ChangeAllocations(AllocationCommand(other, new([existingRule], [], [], [], [])));
        var target = file.Allocations.ReadAllocations(other.Study.CompanyId, other.Study.Id);
        var selection = SelectReuse(source, rule.Id) with { SecondaryRuleIds = [route.Id] };
        Assert.IsTrue(AllocationReuse.Preview(source, target).Items.Single(i => i.RuleId == rule.Id).Conflict);
        Assert.ThrowsExactly<DomainRuleException>(() => ReuseCommand(source, target, selection));
        var hash = Hash(file.Path);
        var approved = ReuseCommand(source, target, selection with { ReplaceConflicts = true });
        // A forged configuration fails before any change is persisted.
        Assert.ThrowsExactly<DomainRuleException>(() => file.Allocations.ChangeAllocations(approved with { Configuration = AllocationConfiguration.Empty }));
        Assert.AreEqual(hash, Hash(file.Path));
        file.Allocations.ChangeAllocations(approved);
        var adopted = file.Allocations.ReadAllocations(other.Study.CompanyId, other.Study.Id);
        Assert.AreEqual(key.Id, adopted.Configuration.PrimaryRules.Single().KeyVersionId);
        Assert.AreEqual(route, adopted.Configuration.SecondaryRules.Single());
        Assert.IsFalse(adopted.Configuration.PrimaryClosed);
        Assert.IsFalse(adopted.Configuration.SecondaryClosed);
        Assert.IsNull(adopted.SecondaryPublication);
        // A stale target preview cannot overwrite a later revision.
        Assert.ThrowsExactly<DomainRuleException>(() => file.Allocations.ChangeAllocations(approved with { RequestId = Id() }));
        file.Allocations.ChangeAllocations(new(other.Study.CompanyId, other.Study.Id, adopted.Resources.Study.Revision,
            Id(), "test", adopted.Configuration.WithPrimaryClosed(true)));
        var closed = file.Allocations.ReadAllocations(other.Study.CompanyId, other.Study.Id);
        Assert.AreEqual("REUSE_TARGET_CLOSED", Assert.ThrowsExactly<DomainRuleException>(() =>
            AllocationReuse.Adopt(source, closed, selection with { ReplaceConflicts = true })).Diagnostic.Code);
    }
}
