using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Infrastructure.CompanyFiles;

namespace Valdrio.Tests;

public sealed partial class T06ImportConfirmationTests
{
    [TestMethod]
    public async Task ReusableAdjustment_ModalAcceptAndCancelProtectBothOrders()
    {
        using var ui = await ResourceUiReady("Amount;Ignored;Account\n1000;x;681001");
        var vm = ui.Resources;
        Assert.IsTrue(await vm.SaveCorrectionAsync(vm.Rows.Single(), "20", System.Globalization.CultureInfo.InvariantCulture));
        vm.AddRuleCommand.Execute(null);
        var card = vm.RuleCards.Single();
        card.Name = "Technical"; card.Pattern = "681*"; card.Value = "500"; card.Reason = "Estimate";
        await card.SaveCommand.ExecuteAsync(null); card = vm.RuleCards.Single();
        ui.Dialogs.ConfirmEconomic = false;
        await card.ApplyCommand.ExecuteAsync(null);
        Assert.AreEqual(1, vm.Rows.Count);
        StringAssert.Contains(ui.Dialogs.EconomicBody, "1020 EUR");
        Assert.IsFalse(card.HasPreview);
        ui.Dialogs.ConfirmEconomic = true;
        await card.ApplyCommand.ExecuteAsync(null);
        Assert.AreEqual("0 EUR", vm.Rows.Single(r => r.Code == "681001").Effective);
        ui.Dialogs.ConfirmEconomic = false;
        Assert.IsFalse(await vm.SaveCorrectionAsync(vm.Rows.Single(r => r.Code == "681001"), "50", System.Globalization.CultureInfo.InvariantCulture));
        Assert.AreEqual("0 EUR", vm.Rows.Single(r => r.Code == "681001").Effective);
        StringAssert.Contains(ui.Dialogs.EconomicBody, "30 EUR");
        ui.Dialogs.ConfirmEconomic = true;
        Assert.IsTrue(await vm.SaveCorrectionAsync(vm.Rows.Single(r => r.Code == "681001"), "50", System.Globalization.CultureInfo.InvariantCulture));
        Assert.AreEqual("30 EUR", vm.Rows.Single(r => r.Code == "681001").Effective);
        card.UndoCommand.Execute(null); await card.ConfirmCommand.ExecuteAsync(null);
        Assert.AreEqual("1050 EUR", vm.Rows.Single(r => r.Code == "681001").Effective);
    }
    [TestMethod]
    public async Task ReusableAdjustment_SuggestsCodeButPreservesManualAndSavedCodes()
    {
        using var ui = await ResourceUiReady();
        ui.Resources.AddRuleCommand.Execute(null);
        var card = ui.Resources.RuleCards.Single();
        card.Name = "Amortización técnica";
        Assert.AreEqual("AJ-AMORTIZACION-TECNICA", card.Code);
        card.Pattern = "0681*";
        Assert.AreEqual("AJ-0681-AMORTIZACION-TECNICA", card.Code);
        card.Pattern = "";
        Assert.AreEqual("AJ-AMORTIZACION-TECNICA", card.Code);
        card.Code = "MI-CODIGO";
        card.Name = "Otro nombre"; card.Pattern = "681*";
        Assert.AreEqual("MI-CODIGO", card.Code);
        card.Reason = "Estimate";
        await card.SaveCommand.ExecuteAsync(null);
        card = ui.Resources.RuleCards.Single();
        card.Name = "Nombre posterior";
        Assert.AreEqual("MI-CODIGO", card.Code);
    }
    [TestMethod]
    public async Task ReusableAdjustment_SelectionShowsImportedBalanceBeforeSaving()
    {
        using var ui = await ResourceUiReady("Amount;Ignored;Account\n600;x;0681001\n-100;x;0681002\n200;x;681001");
        var vm = ui.Resources;
        vm.AddRuleCommand.Execute(null);
        var card = vm.RuleCards.Single();
        var empty = card.SelectionSummary;
        card.Pattern = "0681*";
        StringAssert.Contains(card.SelectionSummary, "500 EUR");
        StringAssert.Contains(card.SelectionSummary, "2 ");
        card.Pattern = "681001";
        StringAssert.Contains(card.SelectionSummary, "200 EUR");
        card.Pattern = "*";
        Assert.AreNotEqual(empty, card.SelectionSummary);
        card.Pattern = "999*";
        Assert.AreEqual(empty, card.SelectionSummary);
        Assert.IsFalse(card.SelectionSummary.Contains("EUR"));
        Assert.IsNull(card.Saved);
        Assert.AreEqual(3, vm.Rows.Count);
    }
    [TestMethod]
    [DataRow("")]
    [DataRow("777*")]
    public async Task ReusableAdjustment_WithoutSourceCreatesAdditionalRowWithFeedback(string pattern)
    {
        using var ui = await ResourceUiReady("Amount;Ignored;Account\n1000;x;681001");
        var vm = ui.Resources;
        vm.AddRuleCommand.Execute(null);
        var card = vm.RuleCards.Single();
        card.Name = "Additional cost"; card.Pattern = pattern; card.Code = "AJ-EXTRA";
        card.Value = "500"; card.Reason = "Analytical estimate";
        Assert.IsFalse(string.IsNullOrWhiteSpace(card.Feedback));
        await card.SaveCommand.ExecuteAsync(null);
        card = vm.RuleCards.Single();
        vm.PrepareRule(card, false);
        Assert.IsTrue(card.HasPreview, vm.Status);
        Assert.IsFalse(string.IsNullOrWhiteSpace(card.Feedback));
        StringAssert.Contains(card.PreviewText, "500 EUR");
        Assert.AreEqual(1, vm.Rows.Count);
        await card.ConfirmCommand.ExecuteAsync(null);
        Assert.IsFalse(card.HasPreview);
        Assert.IsFalse(string.IsNullOrWhiteSpace(card.Feedback));
        Assert.AreEqual("1000 EUR", vm.Rows.Single(r => r.Code == "681001").Effective);
        Assert.AreEqual("500 EUR", vm.Rows.Single(r => r.Code == "AJ-EXTRA").Effective);
        card.UndoCommand.Execute(null); await card.ConfirmCommand.ExecuteAsync(null);
        Assert.AreEqual("0 EUR", vm.Rows.Single(r => r.Code == "AJ-EXTRA").Effective);
        Assert.AreEqual("1000 EUR", vm.Rows.Single(r => r.Code == "681001").Effective);
        Assert.IsFalse(string.IsNullOrWhiteSpace(card.Feedback));
    }
    [TestMethod]
    public async Task ReusableAdjustment_AnalyticalCostUsesExistingAllocationAndUndoAfterCalculation()
    {
        using var file = Baseline(); var w = await ImportedResources(file, "Amount;Ignored;Account\n1000;x;681001");
        var rule = Rule(w); w = RunRule(file, w, rule, AdjustmentRuleAction.Save);
        w = RunRule(file, w, rule, AdjustmentRuleAction.Apply);
        var key = file.Allocations.SaveAllocationKey(KeyCommand(w)).After;
        var secondary = file.Allocations.SaveAllocationKey(KeyCommand(w, stage: AllocationStage.Secondary)).After;
        var config = new AllocationConfiguration([
            new(Id(), w.Study.CompanyId, w.Model.Structure.Id, AccountSelectorKind.Exact, rule.AnalyticalCode, 0, key.Id, true),
            new(Id(), w.Study.CompanyId, w.Model.Structure.Id, AccountSelectorKind.Prefix, "681", 0, key.Id, true)],
            w.Model.Structure.Nodes.Where(n => n.Role == NodeRole.Auxiliary).Select(n => new SecondaryRule(Id(), w.Study.CompanyId, w.Model.Structure.Id, n.Id, "all", secondary.Id, true)).ToArray(), [], [], []);
        file.Allocations.ChangeAllocations(AllocationCommand(w, config));
        var allocations = file.Allocations.ReadAllocations(w.Study.CompanyId, w.Study.Id);
        Assert.IsTrue(allocations.Coverage.All(c => c.Diagnostic is null));
        var calculated = PrimaryAllocationService.Calculate(allocations);
        Assert.AreEqual(500m, calculated.Facts.Sum(f => f.PostedAmount.Amount));
        Assert.IsTrue(calculated.Facts.All(f => f.Account == rule.AnalyticalCode));
        w = allocations.Resources;
        w = RunRule(file, w, rule, AdjustmentRuleAction.Undo, w.Ledger.AdjustmentApplications!.Single().Id);
        allocations = file.Allocations.ReadAllocations(w.Study.CompanyId, w.Study.Id);
        Assert.IsTrue(allocations.Coverage.All(c => c.Diagnostic is null));
        Assert.AreEqual(1000m, PrimaryAllocationService.Calculate(allocations).Facts.Sum(f => f.PostedAmount.Amount));
        Assert.AreEqual(500m, calculated.Facts.Sum(f => f.PostedAmount.Amount));
    }
    [TestMethod]
    public async Task ReusableAdjustment_CardsSavePreviewCancelApplyArchiveAndUndo()
    {
        using var ui = await ResourceUiReady("Amount;Ignored;Account\n1000;x;681001");
        var vm = ui.Resources;
        vm.AddRuleCommand.Execute(null);
        var card = vm.RuleCards.Single();
        card.Name = "Amortización técnica"; card.Pattern = "681*"; card.Code = "AJ-AMORT-TEC";
        card.Value = "500"; card.Reason = "Technical estimate";
        Assert.IsTrue(vm.IsDirty);
        await card.SaveCommand.ExecuteAsync(null);
        Assert.IsFalse(vm.IsDirty, vm.Status);
        card = vm.RuleCards.Single(); vm.PrepareRule(card, false);
        Assert.IsTrue(card.HasPreview, vm.Status);
        Assert.AreEqual(1, vm.Rows.Count);
        card.CancelCommand.Execute(null); Assert.IsFalse(card.HasPreview);
        vm.PrepareRule(card, false); await card.ConfirmCommand.ExecuteAsync(null);
        Assert.AreEqual(2, vm.Rows.Count, vm.Status);
        Assert.AreEqual("0 EUR", vm.Rows.Single(r => r.Code == "681001").Effective);
        Assert.AreEqual("0 EUR", vm.Rows.Single(r => r.Code == "681001").Adjustments);
        Assert.AreEqual("-1000 EUR", vm.Rows.Single(r => r.Code == "681001").RuleAdjustments);
        StringAssert.Contains(card.SelectionSummary, "1000 EUR");
        StringAssert.Contains(card.SelectionSummary, ": 0 EUR");
        Assert.IsFalse(card.ApplyCommand.CanExecute(null));
        await card.DeleteCommand.ExecuteAsync(null);
        card = vm.RuleCards.Single(); Assert.IsTrue(card.Saved!.Archived);
        card.UndoCommand.Execute(null); await card.ConfirmCommand.ExecuteAsync(null);
        Assert.AreEqual("1000 EUR", vm.Rows.Single(r => r.Code == "681001").Effective, vm.Status);
    }
    [TestMethod]
    public async Task ReusableAdjustment_ReopensPreservesSnapshotAndDoesNotApplyInAnotherAnalysis()
    {
        EntityId companyId, studyId; StudySnapshotReceipt snapshot;
        using (var file = Baseline())
        {
            var w = await ImportedResources(file, "Amount;Ignored;Account\n1000;x;681001");
            companyId = w.Study.CompanyId; studyId = w.Study.Id;
            var rule = Rule(w); w = RunRule(file, w, rule, AdjustmentRuleAction.Save);
            w = RunRule(file, w, rule, AdjustmentRuleAction.Apply);
            snapshot = file.Studies.Capture(new(companyId, studyId, w.Study.Revision, Id(), "test"));
            w = RunRule(file, w, rule, AdjustmentRuleAction.Undo, w.Ledger.AdjustmentApplications!.Single().Id);
            var other = await ImportedResources(file, "Amount;Ignored;Account\n1200;x;681001");
            Assert.AreEqual(1, file.Resources.ReadAdjustmentRules(companyId).Count);
            Assert.AreEqual(1200m, other.Preparation.Coverage.Single().Effective.Amount);
            Assert.IsNull(other.Ledger.AdjustmentApplications);
        }
        using var reopened = Service().Open(FileOf("current.valdrio"));
        Assert.AreEqual(1000m, reopened.Resources.ReadResources(companyId, studyId).Preparation.Coverage.Sum(c => c.Effective.Amount));
        Assert.AreEqual(snapshot.CanonicalJson, reopened.Studies.ReadSnapshots(companyId, studyId).Single().CanonicalJson);
        Assert.AreEqual(1, reopened.Resources.ReadAdjustmentRules(companyId).Count);
    }
    [TestMethod]
    public async Task ReusableAdjustment_RollsBackWholeApplicationAndBlocksClosedAnalysis()
    {
        ResourceWorkspaceSnapshot w; ReusableAdjustmentRule rule;
        using (var file = Baseline())
        {
            w = await ImportedResources(file, "Amount;Ignored;Account\n600;x;681001\n400;x;681002");
            rule = Rule(w); w = RunRule(file, w, rule, AdjustmentRuleAction.Save);
        }
        var path = FileOf("current.valdrio"); var hash = Hash(path);
        var command = RuleMutation(w, rule, AdjustmentRuleAction.Apply);
        using (var failed = Service(c => { if (c == CompanyWriteCheckpoint.BeforeCommit) throw new IOException("rollback"); }).Open(path))
            Assert.ThrowsExactly<CompanyFileException>(() => failed.Resources.ChangeResources(command));
        Assert.AreEqual(hash, Hash(path));
        using var file2 = Service().Open(path);
        w = RunRule(file2, w, rule, AdjustmentRuleAction.Apply);
        var app = w.Ledger.AdjustmentApplications!.Single().Id;
        file2.Studies.ChangeState(new(w.Study.CompanyId, w.Study.Id, w.Study.Revision, StudyState.Closed, "test"));
        w = file2.Resources.ReadResources(w.Study.CompanyId, w.Study.Id);
        Assert.ThrowsExactly<DomainRuleException>(() => RunRule(file2, w, rule, AdjustmentRuleAction.Undo, app));
        file2.Studies.ChangeState(new(w.Study.CompanyId, w.Study.Id, w.Study.Revision, StudyState.Open, "test"));
        w = file2.Resources.ReadResources(w.Study.CompanyId, w.Study.Id);
        w = RunRule(file2, w, rule, AdjustmentRuleAction.Undo, app);
        Assert.AreEqual(1000m, w.Preparation.Coverage.Sum(c => c.Effective.Amount));
    }
    private static ReusableAdjustmentRule Rule(ResourceWorkspaceSnapshot w, string pattern = "681*", decimal value = 500m,
        AdjustmentValueMode mode = AdjustmentValueMode.Fixed) => new(Id(), w.Study.CompanyId, 1, "Amortización técnica", pattern,
            "AJ-AMORT-TEC", mode, value, w.Study.Currency.FunctionalCurrencyCode, "Technical depreciation estimate");
    private static ResourceMutation RuleMutation(ResourceWorkspaceSnapshot w, ReusableAdjustmentRule rule,
        AdjustmentRuleAction action, EntityId? application = null) =>
        new(ResourceMutationKind.AdjustmentRule, w.Study.CompanyId, w.Study.Id, w.Study.Revision, Id(), "test", rule.Reason,
            null, [], [], null, null, [], [])
        { AdjustmentRule = new(action, rule, Catalog.GetRequired(w.Study.Currency.FunctionalCurrencyCode).Freeze(), application) };
    private static ResourceWorkspaceSnapshot RunRule(ICompanyFileSession file, ResourceWorkspaceSnapshot w,
        ReusableAdjustmentRule rule, AdjustmentRuleAction action, EntityId? application = null)
    {
        file.Resources.ChangeResources(RuleMutation(w, rule, action, application));
        return file.Resources.ReadResources(w.Study.CompanyId, w.Study.Id);
    }
    [TestMethod]
    public async Task ReusableAdjustment_SubstitutesOffsetsUndoesAndReappliesWithoutFakeImport()
    {
        using var file = Baseline();
        var w = await ImportedResources(file, "Amount;Ignored;Account\n600;x;681001\n400;x;681002");
        var rule = Rule(w);
        w = RunRule(file, w, rule, AdjustmentRuleAction.Save);
        Assert.AreEqual(1000m, w.Preparation.Coverage.Sum(c => c.Effective.Amount));
        w = RunRule(file, w, rule, AdjustmentRuleAction.Apply);
        Assert.AreEqual(500m, w.Preparation.Coverage.Sum(c => c.Effective.Amount));
        Assert.IsTrue(w.Preparation.Coverage.All(c => c.Difference.Amount == 0));
        var generated = w.Resources.Single(r => r.AnalyticalOrigin is not null);
        Assert.AreEqual(0, generated.Sources.Count);
        Assert.AreEqual("Amortización técnica", w.Origins.Single(o => o.Id == generated.Id).Descriptions.Single());
        Assert.AreEqual(1000m, w.Resources.SelectMany(r => r.Sources).Sum(s => s.Conversion.OriginalMoney.Amount));
        var applied = w.Ledger.AdjustmentApplications!.Single();
        w = RunRule(file, w, rule, AdjustmentRuleAction.Undo, applied.Id);
        Assert.AreEqual(1000m, w.Preparation.Coverage.Sum(c => c.Effective.Amount));
        Assert.AreEqual(0m, w.Preparation.Coverage.Single(c => c.ResourceId == generated.Id).Effective.Amount);
        w = RunRule(file, w, rule, AdjustmentRuleAction.Apply);
        Assert.AreEqual(500m, w.Preparation.Coverage.Sum(c => c.Effective.Amount));
        Assert.AreEqual(generated.Id, w.Resources.Single(r => r.AnalyticalOrigin is not null).Id);
        Assert.AreEqual(2, w.Ledger.AdjustmentApplications!.Count);
    }
    [TestMethod]
    public async Task ReusableAdjustment_RejectsOverlapCodeCollisionAndStalePreviewAtomically()
    {
        using var file = Baseline(); var w = await ImportedResources(file, "Amount;Ignored;Account\n1000;x;681001");
        var rule = Rule(w); w = RunRule(file, w, rule, AdjustmentRuleAction.Save);
        var command = RuleMutation(w, rule, AdjustmentRuleAction.Apply);
        var receipt = file.Resources.ChangeResources(command);
        Assert.AreEqual(receipt.Request.RequestId, file.Resources.ChangeResources(command).Request.RequestId);
        w = file.Resources.ReadResources(w.Study.CompanyId, w.Study.Id);
        var hash = Hash(file.Path);
        Assert.ThrowsExactly<DomainRuleException>(() => AdjustmentRuleEngine.Preview(w, rule));
        Assert.ThrowsExactly<DomainRuleException>(() => AdjustmentRuleEngine.Preview(w, rule with { Id = Id(), AccountPattern = "681001", AnalyticalCode = "OTHER" }));
        Assert.ThrowsExactly<DomainRuleException>(() => AdjustmentRuleEngine.Preview(w, rule with { Id = Id(), AccountPattern = "777*" }));
        Assert.ThrowsExactly<DomainRuleException>(() => file.Resources.ChangeResources(withRequestId()));
        Assert.AreEqual(hash, Hash(file.Path));
        ResourceMutation withRequestId() => new(command.Kind, command.CompanyId, command.StudyId, command.ExpectedRevision, Id(), command.Actor,
            command.Reason, null, [], [], null, null, [], []) { AdjustmentRule = command.AdjustmentRule };
    }
    [TestMethod]
    public async Task ReusableAdjustment_PercentageAdditionAndNegativeBalancesAreDeterministic()
    {
        using var file = Baseline(); var w = await ImportedResources(file, "Amount;Ignored;Account\n1200;x;681001\n-200;x;681002");
        Assert.AreEqual(500m, AdjustmentRuleEngine.Preview(w, Rule(w, value: 50m, mode: AdjustmentValueMode.Percentage)).Target.Amount);
        Assert.AreEqual(800m, AdjustmentRuleEngine.Preview(w, Rule(w, value: -200m, mode: AdjustmentValueMode.AddAmount)).Target.Amount);
        Assert.AreEqual(-100m, AdjustmentRuleEngine.Preview(w, Rule(w, "681002", 50m, AdjustmentValueMode.Percentage)).Target.Amount);
        var swapped = new ResourceWorkspaceSnapshot(w.Study, w.Precision, w.Model, w.Origins.Reverse().ToArray(), w.Ledger);
        var rule = Rule(w); var command = RuleMutation(w, rule, AdjustmentRuleAction.Apply);
        Assert.AreEqual(CanonicalSnapshotJson.Serialize(ResourceWorkspaceRules.Apply(w, command)),
            CanonicalSnapshotJson.Serialize(ResourceWorkspaceRules.Apply(swapped, command)));
    }
    [TestMethod]
    public async Task ReusableAdjustment_ManualCorrectionRemainsIndependentAndSurvivesUndo()
    {
        using var file = Baseline(); var w = await ImportedResources(file, "Amount;Ignored;Account\n1000;x;681001");
        var originalId = w.Resources.Single().Id;
        var rule = Rule(w); w = RunRule(file, w, rule, AdjustmentRuleAction.Save);
        w = RunRule(file, w, rule, AdjustmentRuleAction.Apply);
        var manual = ResourceCorrectionCommand.Prepare(w, originalId, w.Study.Revision, new(20m, new("EUR")),
            Catalog.GetRequired(new("EUR")).Freeze(), Id(), Id(), Id(), "test")!;
        Assert.AreEqual(20m, manual.Adjustments.Single().OriginalDelta!.Value.Amount);
        file.Resources.ChangeResources(manual); w = file.Resources.ReadResources(w.Study.CompanyId, w.Study.Id);
        Assert.AreEqual(520m, w.Preparation.Coverage.Sum(c => c.Effective.Amount));
        w = RunRule(file, w, rule, AdjustmentRuleAction.Undo, w.Ledger.AdjustmentApplications!.Single().Id);
        Assert.AreEqual(1020m, w.Preparation.Coverage.Sum(c => c.Effective.Amount));
        Assert.AreEqual(0m, AdjustmentRuleEngine.RuleTotal(w, originalId).Amount);
    }
    [TestMethod]
    public async Task ReusableAdjustment_VersionsArchiveAndReplay()
    {
        using var file = Baseline(); var w = await ImportedResources(file, "Amount;Ignored;Account\n1000;x;681001");
        var rule = Rule(w); w = RunRule(file, w, rule, AdjustmentRuleAction.Save);
        w = RunRule(file, w, rule, AdjustmentRuleAction.Apply);
        var next = rule with { Version = 2, Value = 700m, Archived = true };
        w = RunRule(file, w, next, AdjustmentRuleAction.Save);
        Assert.AreEqual(next, file.Resources.ReadAdjustmentRules(w.Study.CompanyId).Single());
        Assert.AreEqual(rule, w.Ledger.AdjustmentApplications!.Single().Rule);
        Assert.ThrowsExactly<DomainRuleException>(() => RunRule(file, w, rule, AdjustmentRuleAction.Save));
        w = RunRule(file, w, rule, AdjustmentRuleAction.Undo, w.Ledger.AdjustmentApplications!.Single().Id);
        Assert.AreEqual(1000m, w.Preparation.Coverage.Sum(c => c.Effective.Amount));
        var history = file.Resources.ReadResourceHistory(w.Study.CompanyId, w.Study.Id);
        Assert.AreEqual(CanonicalSnapshotJson.Serialize(w.Ledger), CanonicalSnapshotJson.Serialize(ResourceHistory.Replay(w.Study, w.Precision, w.Model,
            file.Imports.ReadImports(w.Study.CompanyId, w.Study.Id), history)));
    }
}
