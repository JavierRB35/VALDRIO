using System.Globalization;
using Valdrio.Application;
using Valdrio.Calculation;
using Valdrio.Domain;
using Valdrio.Infrastructure.CompanyFiles;

namespace Valdrio.Tests;

public sealed partial class T06ImportConfirmationTests
{
    private static ResourceMutation Mutation(ResourceWorkspaceSnapshot w, ResourceMutationKind kind,
        EntityId? resource = null, IReadOnlyList<ResourcePartDraft>? parts = null,
        IReadOnlyList<ResourceAdjustmentRequest>? adjustments = null, ResourceCostObject? obj = null,
        FxRateSetVersion? rates = null, IReadOnlyList<ResourceRateChoice>? choices = null,
        IReadOnlyList<ResourcePartReplacement>? replacements = null) =>
        new(kind, w.Study.CompanyId, w.Study.Id, w.Study.Revision, Id(), "test", "Checked business choice", resource,
            parts ?? [], adjustments ?? [], obj, rates, choices ?? [], replacements ?? []);
    private static Money Eur(decimal amount) => new(amount, new("EUR"));
    private static ResourcePartDraft Pending(decimal amount) => new(Id(), Eur(amount), ResourceRoutingKind.Pending);
    private static ResourceAdjustmentRequest Delta(decimal value, string currency = "EUR", FxRate? rate = null) =>
        new(Id(), new Money(value, new(currency)), rate, "Correction", "Checked correction", null, Catalog.GetRequired(new(currency)).Freeze());
    private async Task<ResourceWorkspaceSnapshot> ImportedResources(ICompanyFileSession file, string csv = "Amount;Ignored;Account\n1000;x;6000")
    {
        var study = Study(file); var profile = Profile(study);
        var prepared = Prepare(file, study, await Source(profile, csv), profile);
        var result = file.Imports.ConfirmImport(Command(prepared));
        return file.Resources.ReadResources(study.CompanyId, study.Id);
    }

    [TestMethod]
    public async Task T07_C04_PersistsAdjustmentRoutesAuditSnapshotAndReopens()
    {
        StudySnapshotReceipt photo; EntityId studyId, companyId; ResourceMutation command;
        using (var file = Baseline())
        {
            var w = await ImportedResources(file); studyId = w.Study.Id; companyId = w.Study.CompanyId;
            var obj = new ResourceCostObject(Id(), companyId, studyId, "PRODUCT", "Producto de prueba", ResourceObjectKind.Product);
            file.Resources.ChangeResources(Mutation(w, ResourceMutationKind.PutObject, obj: obj));
            w = file.Resources.ReadResources(companyId, studyId);
            var pool = w.Model.Structure.Pools.First();
            ResourcePartDraft[] parts = [new(Id(), Eur(300), ResourceRoutingKind.DirectObject, [new(obj.Id, Eur(300))]),
                new(Id(), Eur(500), ResourceRoutingKind.AllocationPool, poolId: pool.Id),
                new(Id(), Eur(100), ResourceRoutingKind.Excluded, exclusionReason: "Fuera del estudio")];
            command = Mutation(w, ResourceMutationKind.EditResource, w.Resources.Single().Id, parts, [Delta(-100)]);
            var receipt = file.Resources.ChangeResources(command);
            Assert.AreEqual(CanonicalSnapshotJson.Serialize(receipt), CanonicalSnapshotJson.Serialize(file.Resources.ChangeResources(command)));
            var current = file.Resources.ReadResources(companyId, studyId);
            var coverage = current.Preparation.Coverage.Single();
            Assert.AreEqual(900m, coverage.Effective.Amount); Assert.AreEqual(0m, coverage.Difference.Amount);
            Assert.AreEqual(300m, coverage.Direct.Amount); Assert.AreEqual(500m, coverage.Pools.Amount); Assert.AreEqual(100m, coverage.Excluded.Amount);
            Assert.AreEqual("1", Query(file.Path, "SELECT COUNT(*) FROM AuditEvents WHERE AggregateType='ResourceAdjustment'"));
            photo = file.Studies.Capture(new(companyId, studyId, current.Study.Revision, Id(), "test"));
            Assert.AreEqual(ResourceStudySnapshotInput.Schema, photo.PayloadSchema); photo.EnsureValid();
            Assert.IsFalse(StudyPreparationService.Evaluate(photo.Input).Diagnostics.Any(d => d.Code == "STUDY_PARTITIONS_PENDING"));
            Assert.AreEqual(2, ((ResourceStudySnapshotInput)photo.Input).History.Count);
        }
        using var reopened = Service().Open(FileOf("current.valdrio"));
        Assert.AreEqual(900m, reopened.Resources.ReadResources(companyId, studyId).Preparation.Coverage.Single().Effective.Amount);
        Assert.AreEqual(photo.CanonicalJson, reopened.Studies.ReadSnapshots(companyId, studyId).Single().CanonicalJson);
        Assert.AreEqual(2, reopened.Resources.ReadResourceHistory(companyId, studyId).Count);
        Assert.AreEqual(command.RequestId, reopened.Resources.ChangeResources(command).Request.RequestId);
    }

    [TestMethod]
    public async Task T07_C05_Extra100AndMissingExclusionReasonNeverWrite()
    {
        using var file = Baseline(); var w = await ImportedResources(file); var before = Hash(file.Path);
        foreach (var part in new[] { Pending(1100), new ResourcePartDraft(Id(), Eur(1000), ResourceRoutingKind.Excluded, exclusionReason: " ") })
        {
            Assert.ThrowsExactly<DomainRuleException>(() => file.Resources.ChangeResources(Mutation(w, ResourceMutationKind.EditResource, w.Resources.Single().Id, [part])));
            Assert.AreEqual(before, Hash(file.Path));
        }
    }

    [TestMethod]
    public async Task T07_C30_TransactionalRevalueRejectsOppositePerAccountErrors()
    {
        using var file = Baseline(); var w = await ImportedResources(file, "Amount;Ignored;Account\n100;x;6000\n100;y;6001");
        var rates = new FxRateSetVersion("explicit", BusinessDate.ParseIso("2026-10-31"), "test",
            [new(new("unused"), new("USD"), new("EUR"), 1m, BusinessDate.ParseIso("2026-10-31"), "test", "explicit")]);
        var before = Hash(file.Path);
        var command = Mutation(w, ResourceMutationKind.Revalue, rates: rates,
            replacements: [new(w.Resources[0].Id, [Pending(90)]), new(w.Resources[1].Id, [Pending(110)])]);
        var error = Assert.ThrowsExactly<DomainRuleException>(() => file.Resources.ChangeResources(command));
        Assert.AreEqual("RES_PARTITION_MISMATCH", error.Diagnostic.Code); Assert.AreEqual(before, Hash(file.Path));
    }

    [TestMethod]
    public async Task T07_AccountAggregationIgnoresDescriptionsAndPreservesSourceRows()
    {
        using var file = Baseline(); var study = Study(file);
        var profile = GroupProfile(study, new(2, "6005"), new(3, "6005"));
        var prepared = Prepare(file, study, await Source(profile, "Amount;Ignored;Account\n280;x;6005\n78,35;another description;6005"), profile);
        file.Imports.ConfirmImport(Command(prepared));
        var w = file.Resources.ReadResources(study.CompanyId, study.Id);
        Assert.AreEqual("6005", w.Origins.Single().Code); Assert.AreEqual(2, w.Resources.Single().Sources.Count);
        Assert.AreEqual(358.35m, w.Preparation.Coverage.Single().Effective.Amount);
    }

    [TestMethod]
    public async Task T07_AdjustmentReversalUsesItsOwnHistoricFxAndCannotBeRepeated()
    {
        using var file = Baseline(); var w = await ImportedResources(file);
        var rate = new FxRate(new("usd-eur-1"), new("USD"), new("EUR"), 2m, BusinessDate.ParseIso("2026-10-31"), "manual", "v1");
        var delta = Delta(-50m, "USD", rate); var id = w.Resources.Single().Id;
        file.Resources.ChangeResources(Mutation(w, ResourceMutationKind.EditResource, id, [Pending(900)], [delta]));
        w = file.Resources.ReadResources(w.Study.CompanyId, w.Study.Id);
        var reversal = new ResourceAdjustmentRequest(Id(), null, null, "", "Revert correction", delta.Id, null);
        file.Resources.ChangeResources(Mutation(w, ResourceMutationKind.EditResource, id, [Pending(1000)], [reversal]));
        w = file.Resources.ReadResources(w.Study.CompanyId, w.Study.Id);
        Assert.AreEqual(0m, w.Preparation.Coverage.Single().AdjustmentTotal.Amount);
        Assert.AreEqual(2m, w.Resources.Single().Adjustments.Single(a => a.Id == reversal.Id).Conversion.Quote);
        Assert.AreEqual(2, w.Resources.Single().Adjustments.Count);
        var before = Hash(file.Path);
        Assert.ThrowsExactly<DomainRuleException>(() => file.Resources.ChangeResources(Mutation(w, ResourceMutationKind.EditResource, id, [Pending(1100)], [reversal with { Id = Id() }])));
        Assert.AreEqual(before, Hash(file.Path));
    }

    [TestMethod]
    public async Task T07_RevaluationRequiresReconciledPartsAndPreservesOldSnapshotAndOriginalMoney()
    {
        using var file = Baseline(); var w = await ImportedResources(file); var id = w.Resources.Single().Id;
        var rate = new FxRate(new("usd-eur-1"), new("USD"), new("EUR"), 2m, BusinessDate.ParseIso("2026-10-31"), "manual", "v1");
        var delta = Delta(-50m, "USD", rate);
        file.Resources.ChangeResources(Mutation(w, ResourceMutationKind.EditResource, id, [Pending(900)], [delta]));
        w = file.Resources.ReadResources(w.Study.CompanyId, w.Study.Id);
        var oldPhoto = file.Studies.Capture(new(w.Study.CompanyId, w.Study.Id, w.Study.Revision, Id(), "test"));
        var next = new FxRate(new("usd-eur-2"), new("USD"), new("EUR"), 3m, BusinessDate.ParseIso("2026-10-31"), "manual", "v2");
        var rates = new FxRateSetVersion("v2", BusinessDate.ParseIso("2026-10-31"), "test", [next]);
        var invalid = Mutation(w, ResourceMutationKind.Revalue, rates: rates, choices: [new(delta.Id, next.Id)]);
        var hash = Hash(file.Path);
        Assert.ThrowsExactly<DomainRuleException>(() => file.Resources.ChangeResources(invalid)); Assert.AreEqual(hash, Hash(file.Path));
        file.Resources.ChangeResources(Mutation(w, ResourceMutationKind.Revalue, rates: rates, choices: [new(delta.Id, next.Id)],
            replacements: [new(id, [Pending(850)])]));
        var current = file.Resources.ReadResources(w.Study.CompanyId, w.Study.Id);
        Assert.AreEqual(850m, current.Preparation.Coverage.Single().Effective.Amount);
        Assert.AreEqual(-50m, current.Resources.Single().Adjustments.Single().Conversion.OriginalMoney.Amount);
        Assert.AreEqual(oldPhoto.CanonicalJson, file.Studies.ReadSnapshots(w.Study.CompanyId, w.Study.Id).Single().CanonicalJson);
        var newPhoto = file.Studies.Capture(new(w.Study.CompanyId, w.Study.Id, current.Study.Revision, Id(), "test"));
        Assert.AreNotEqual(oldPhoto.EconomicInputHash, newPhoto.EconomicInputHash);
        foreach (var culture in new[] { "es-ES", "en-US", "de-DE" })
        {
            var saved = CultureInfo.CurrentCulture;
            try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture); newPhoto.EnsureValid(); }
            finally { CultureInfo.CurrentCulture = saved; }
        }
    }

    [TestMethod]
    public async Task T07_ResourceTransactionRollsBackAfterFirstWriteAndCancellation()
    {
        var path = FileOf("current.valdrio"); ResourceWorkspaceSnapshot w;
        using (var file = Baseline()) w = await ImportedResources(file);
        var command = Mutation(w, ResourceMutationKind.EditResource, w.Resources.Single().Id, [Pending(900)], [Delta(-100)]);
        var hash = Hash(path);
        using (var failed = Service(c => { if (c == CompanyWriteCheckpoint.AfterFirstWrite) throw new IOException("failure"); }).Open(path))
            Assert.ThrowsExactly<CompanyFileException>(() => failed.Resources.ChangeResources(command));
        Assert.AreEqual(hash, Hash(path));
        using var cts = new CancellationTokenSource();
        using (var cancelled = Service(c => { if (c == CompanyWriteCheckpoint.BeforeCommit) cts.Cancel(); }).Open(path))
            Assert.ThrowsExactly<OperationCanceledException>(() => cancelled.Resources.ChangeResources(command, cts.Token));
        Assert.AreEqual(hash, Hash(path));
        using var reopened = Service().Open(path);
        Assert.AreEqual(0, reopened.Resources.ReadResourceHistory(w.Study.CompanyId, w.Study.Id).Count);
    }

    [TestMethod]
    public async Task T07_ChangedSourcesRequireExplicitResetWithoutDeletingHistory()
    {
        using var file = Baseline(); var w = await ImportedResources(file);
        file.Resources.ChangeResources(Mutation(w, ResourceMutationKind.EditResource, w.Resources.Single().Id, [Pending(900)], [Delta(-100)]));
        w = file.Resources.ReadResources(w.Study.CompanyId, w.Study.Id);
        var source = await Source(Profile(w.Study), "Amount;Ignored;Account\n2000;x;6000");
        var prepared = Prepare(file, w.Study, source, Profile(w.Study)); var hash = Hash(file.Path);
        var error = Assert.ThrowsExactly<DomainRuleException>(() => file.Imports.ConfirmImport(Command(prepared)));
        Assert.AreEqual("RES_SOURCE_CHANGED", error.Diagnostic.Code); Assert.AreEqual(hash, Hash(file.Path));
        var reset = file.Resources.ChangeResources(Mutation(w, ResourceMutationKind.ResetPreparation));
        prepared = Prepare(file, reset.ConfirmedStudy, source, Profile(reset.ConfirmedStudy));
        file.Imports.ConfirmImport(Command(prepared));
        var current = file.Resources.ReadResources(w.Study.CompanyId, w.Study.Id);
        Assert.AreEqual(3000m, current.Preparation.Coverage.Single().Effective.Amount);
        Assert.AreEqual(2, file.Resources.ReadResourceHistory(w.Study.CompanyId, w.Study.Id).Count);
    }

    [TestMethod]
    public async Task T07_ForeignTargetsStaleRevisionReadOnlyAndClosedStudyAreBlocked()
    {
        using var file = Baseline(); var w = await ImportedResources(file); var id = w.Resources.Single().Id; var hash = Hash(file.Path);
        foreach (var part in new ResourcePartDraft[] { new(Id(), Eur(1000), ResourceRoutingKind.DirectObject, [new(Id(), Eur(1000))]),
            new(Id(), Eur(1000), ResourceRoutingKind.AllocationPool, poolId: Id()) })
            Assert.ThrowsExactly<DomainRuleException>(() => file.Resources.ChangeResources(Mutation(w, ResourceMutationKind.EditResource, id, [part])));
        Assert.AreEqual(hash, Hash(file.Path));
        var first = file.Resources.ChangeResources(Mutation(w, ResourceMutationKind.EditResource, id, [Pending(1000)]));
        Assert.ThrowsExactly<DomainRuleException>(() => file.Resources.ChangeResources(Mutation(w, ResourceMutationKind.EditResource, id, [Pending(1000)])));
        w = file.Resources.ReadResources(w.Study.CompanyId, w.Study.Id);
        using (var readOnly = Service().Open(file.Path, CompanyFileOpenMode.ReadOnly))
            Assert.ThrowsExactly<CompanyFileException>(() => readOnly.Resources.ChangeResources(Mutation(w, ResourceMutationKind.EditResource, id, [Pending(1000)])));
        file.Studies.ChangeState(new(w.Study.CompanyId, w.Study.Id, w.Study.Revision, StudyState.Closed, "test"));
        w = file.Resources.ReadResources(w.Study.CompanyId, w.Study.Id);
        Assert.ThrowsExactly<DomainRuleException>(() => file.Resources.ChangeResources(Mutation(w, ResourceMutationKind.EditResource, id, [Pending(1000)])));
    }

    [TestMethod]
    public async Task T07_TamperedLedgerAndMissingAdjustmentAuditAreRejected()
    {
        string path = FileOf("current.valdrio");
        using (var file = Baseline())
        {
            var w = await ImportedResources(file);
            file.Resources.ChangeResources(Mutation(w, ResourceMutationKind.EditResource, w.Resources.Single().Id, [Pending(900)], [Delta(-100)]));
        }
        Query(path, "DELETE FROM AuditEvents WHERE AggregateType='ResourceAdjustment'; SELECT 1;");
        Assert.ThrowsExactly<CompanyFileException>(() => Service().Open(path));
    }
}
