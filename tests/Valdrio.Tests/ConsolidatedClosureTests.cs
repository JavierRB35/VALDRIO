using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Desktop;
using Valdrio.Localization;
using Valdrio.Infrastructure.CompanyFiles;
namespace Valdrio.Tests;
public sealed partial class T06ImportConfirmationTests
{
    private async Task<AllocationWorkspaceSnapshot> ClosureFixture(ICompanyFileSession file)
    {
        var w = await T12Fixture(file);
        if (!w.Configuration.PrimaryClosed)
            file.Allocations.ChangeAllocations(AllocationCommand(w.Resources, w.Configuration.WithPrimaryClosed(true)));
        return file.Allocations.ReadAllocations(w.Resources.Study.CompanyId, w.Resources.Study.Id);
    }
    [TestMethod]
    public async Task DuplicateProjectCommandRejectsOverwriteAndOffersIndependentCopy()
    {
        using(var file=Baseline()) { }
        var source=FileOf("current.valdrio"); var target=FileOf("scenario.valdrio");
        var dialogs=new QueuedCompanyFileDialogs();
        var preferences=new PresentationPreferencesService(new LanguageCatalog(),UserPresentationPreferences.Default);
        using var vm=new CompanyFileViewModel(Service(),dialogs,Catalog,new UiTextCatalog(),preferences,new PresentationFormatter(new DisplayTimeZoneResolver()));
        await vm.OpenPath(source);
        var offered=false;
        vm.ConfirmFileOperation=(_,original,copy)=>{ offered=true;Assert.AreEqual(source,original);Assert.AreEqual(target,copy);return false; };
        dialogs.Paths.Enqueue(target); await vm.DuplicateCommand.ExecuteAsync(null);
        Assert.IsTrue(offered);Assert.AreEqual(source,vm.ActivePath);Assert.IsTrue(vm.Status.Contains(target));
        using(var copy=Service().Open(target)) Assert.AreEqual(vm.Snapshot!.Id,copy.Read().Id);
        dialogs.Paths.Enqueue(target); await vm.DuplicateCommand.ExecuteAsync(null);
        Assert.AreEqual("FILE_ALREADY_EXISTS",vm.DiagnosticCode);Assert.AreEqual(source,vm.ActivePath);
        var another=FileOf("another-scenario.valdrio");
        dialogs.Paths.Enqueue(another);vm.ConfirmFileOperation=(_,_,_)=>true;
        await vm.DuplicateCommand.ExecuteAsync(null);
        Assert.AreEqual(another,vm.ActivePath);Assert.IsNull(vm.DiagnosticCode);
    }
    [TestMethod]
    public async Task ConsolidatedClosurePersistsCurrentRunAndIndependentCopyWithoutReopening()
    {
        EntityId company, study, runId;
        using (var file = Baseline())
        {
            var w = await ClosureFixture(file); company = w.Resources.Study.CompanyId; study = w.Resources.Study.Id;
            var result = SecondaryAllocationService.Calculate(w);
            var command = AllocationCommand(w.Resources, w.Configuration.WithSecondaryClosed(true, SecondaryAllocationService.ResultFingerprint(result, w.Configuration)));
            var receipt = file.Allocations.ChangeAllocations(command);
            var history = file.Runs.ReadRuns(company, study);
            var run = history.MatchingCompleted(receipt.ConfirmedStudy.Revision);
            Assert.IsNotNull(run); runId = run.Id;
            Assert.AreEqual(6500m, run.Output!.Secondary!.Facts.Sum(f => f.PostedAmount.Amount));
            Assert.IsTrue(run.Output.Secondary.ResourceBridges.All(b => b.Difference.Amount == 0));
            file.Allocations.ChangeAllocations(command);
            Assert.AreEqual(1, file.Runs.ReadRuns(company, study).Runs.Count);
            file.Backup(FileOf("hypothesis.valdrio"));
            Assert.ThrowsExactly<CompanyFileException>(() => file.Backup(FileOf("hypothesis.valdrio")));
            Assert.IsTrue(file.Allocations.ReadAllocations(company, study).Configuration.SecondaryClosed);
        }
        using var original = Service().Open(FileOf("current.valdrio"));
        using var copy = Service().Open(FileOf("hypothesis.valdrio"));
        Assert.AreEqual(runId, copy.Runs.ReadRuns(company, study).CurrentRunId);
        var copied = copy.Allocations.ReadAllocations(company, study);
        copy.Allocations.ChangeAllocations(AllocationCommand(copied.Resources, copied.Configuration.WithSecondaryClosed(false)));
        Assert.IsNull(copy.Runs.ReadRuns(company, study).CurrentRunId);
        Assert.AreEqual(runId, original.Runs.ReadRuns(company, study).CurrentRunId);
        Assert.IsTrue(original.Allocations.ReadAllocations(company, study).Configuration.SecondaryClosed);
    }
    [TestMethod]
    [DataRow(CompanyWriteCheckpoint.AfterFirstWrite)]
    [DataRow(CompanyWriteCheckpoint.BeforeCommit)]
    public async Task ConsolidatedClosureFailureRollsBackClosureSnapshotAndRun(CompanyWriteCheckpoint checkpoint)
    {
        AllocationWorkspaceSnapshot w;
        using (var file = Baseline()) w = await ClosureFixture(file);
        var result = SecondaryAllocationService.Calculate(w);
        var command = AllocationCommand(w.Resources, w.Configuration.WithSecondaryClosed(true, SecondaryAllocationService.ResultFingerprint(result, w.Configuration)));
        using (var failing = Service(p => { if (p == checkpoint) throw new InvalidOperationException("Injected closure failure"); }).Open(FileOf("current.valdrio")))
            Assert.ThrowsExactly<InvalidOperationException>(() => failing.Allocations.ChangeAllocations(command));
        using var reopened = Service().Open(FileOf("current.valdrio"));
        var current = reopened.Allocations.ReadAllocations(w.Resources.Study.CompanyId, w.Resources.Study.Id);
        Assert.IsFalse(current.Configuration.SecondaryClosed);
        Assert.AreEqual(w.Resources.Study.Revision, current.Resources.Study.Revision);
        Assert.AreEqual(0, reopened.Runs.ReadRuns(w.Resources.Study.CompanyId, w.Resources.Study.Id).Runs.Count);
        Assert.AreEqual(0, reopened.Studies.ReadSnapshots(w.Resources.Study.CompanyId, w.Resources.Study.Id).Count);
    }
    [TestMethod]
    public async Task ConsolidatedRatesReadClosedCostsAndRefreshPhysicalWithoutReopening()
    {
        using var file=Baseline();var w=await T15Fixture(file);var study=w.Resources.Study;
        file.Allocations.ChangeAllocations(AllocationCommand(w.Resources,w.Configuration.WithPrimaryClosed(true)));
        w=file.Allocations.ReadAllocations(study.CompanyId,study.Id);
        var secondary=SecondaryAllocationService.Calculate(w);
        var receipt=file.Allocations.ChangeAllocations(AllocationCommand(w.Resources,w.Configuration.WithSecondaryClosed(true,SecondaryAllocationService.ResultFingerprint(secondary,w.Configuration))));
        study=receipt.ConfirmedStudy;
        PrincipalRateReport Report()=>PrincipalRateReportService.Read(study,w.Resources.Model,file.Runs.ReadRuns(study.CompanyId,study.Id),file.PhysicalActivity.ReadCurrent(study.CompanyId,study.Id));
        var before=Report();var row=before.Rows.Single(r=>r.Name.Contains("Mecanizado"));
        Assert.AreEqual(20m,row.Result!.CombinedRate);var observation=row.Observation!;
        file.PhysicalActivity.Save(new(study.CompanyId,study.Id,study.Revision,observation.NodeId,observation.PoolId,observation.Version,
            PhysicalObservationState.Confirmed,null,null,100,50,observation.NormalEvidence,observation.RealEvidence,null,null,"Corrected actual activity","test"));
        var after=Report();Assert.AreEqual(before.EconomicRunId,after.EconomicRunId);
        Assert.AreEqual(22m,after.Rows.Single(r=>r.PoolId==row.PoolId).Result!.CombinedRate);
        Assert.IsTrue(file.Allocations.ReadAllocations(study.CompanyId,study.Id).Configuration.SecondaryClosed);
    }
    [TestMethod]
    public async Task AuxiliaryReportDoesNotCountEconomicGroupsTwiceOrGuessPercentageHours()
    {
        using var file=Baseline();var w=await T16Fixture(file);var study=w.Resources.Study;
        var physical=file.PhysicalActivity.ReadCurrent(study.CompanyId,study.Id);
        var report=AuxiliaryServiceReport.Read(w,physical);
        Assert.IsTrue(report.All(r=>r.Distributed==50 && r.Unassigned==0 && r.Idle==50));
        var plan=w.Configuration.AuxiliaryCapacity!;
        var duplicate=plan with { Rows=plan.Rows.Select(r=>r with { ServiceSets=r.ServiceSets.Concat(r.ServiceSets.Select(s=>s with {ComponentRoutingGroup="second"})).ToArray() }).ToArray() };
        var projected=new AllocationWorkspaceSnapshot(w.Resources,w.Keys,w.Configuration.WithAuxiliaryCapacity(duplicate),w.ConfigurationRevision);
        Assert.IsTrue(AuxiliaryServiceReport.Read(projected,physical).All(r=>r.Distributed==50));
        var through=plan with {Rows=plan.Rows.Select(r=>r with { Mode=AuxiliaryCapacityMode.ThroughFlow }).ToArray()};
        projected=new(w.Resources,w.Keys,w.Configuration.WithAuxiliaryCapacity(through),w.ConfigurationRevision);
        Assert.IsTrue(AuxiliaryServiceReport.Read(projected,physical).All(r=>r.Distributed is null && r.Reason=="UnknownBasis"));
        var unit=w.Resources.Model.Structure.Pools.First().DriverUnitId;
        var destinations=w.Resources.Model.Structure.Nodes.Take(2).Select((n,i)=>new AllocationKeyLine(n.Id,i+1)).ToArray();
        var keys=w.Keys.Select(k=>k.Stage!=AllocationStage.Secondary?k:new AllocationKeyVersion(k.Id,k.CompanyId,k.ModelVersionId,k.FamilyId,k.VersionNumber,k.ParentVersionId,k.Revision,k.Name,k.Stage,
            AllocationKeyMode.DriverUnits,unit,SelfAllocationPolicy.Allowed,k.Status,k.PublishedAtUtc,destinations)).ToArray();
        projected=new(w.Resources,keys,projected.Configuration,w.ConfigurationRevision);
        Assert.IsTrue(AuxiliaryServiceReport.Read(projected,physical).All(r=>r.Distributed==50 && r.Unassigned==0));
        var wrongModel=physical.Select(o=>o with {ModelVersionId=new(Guid.NewGuid())}).ToArray();
        Assert.IsTrue(AuxiliaryServiceReport.Read(projected,wrongModel).All(r=>r.Real is null && r.Distributed is null));
    }
    [TestMethod]
    public async Task LegacyClosureMaterializesRunOnceWithoutChangingPublishedAmounts()
    {
        EntityId company,study;string original;
        using(var file=Baseline())
        {
            var w=await ClosureFixture(file);company=w.Resources.Study.CompanyId;study=w.Resources.Study.Id;
            var secondary=SecondaryAllocationService.Calculate(w);
            var closed=file.Allocations.ChangeAllocations(AllocationCommand(w.Resources,w.Configuration.WithSecondaryClosed(true,SecondaryAllocationService.ResultFingerprint(secondary,w.Configuration))));
            original=CanonicalSnapshotJson.Serialize(closed.SecondaryPublication);
        }
        // Reproduce the old file shape: closed publication but no consolidated run.
        using(var db=new Microsoft.Data.Sqlite.SqliteConnection("Pooling=False;Data Source="+FileOf("current.valdrio")))
        {db.Open();using var command=db.CreateCommand();command.CommandText="DROP TRIGGER CalculationRunsDelete; DELETE FROM CalculationRuns; CREATE TRIGGER CalculationRunsDelete BEFORE DELETE ON CalculationRuns BEGIN SELECT RAISE(ABORT,'Run history is immutable'); END;";command.ExecuteNonQuery();}
        using(var file=Service().Open(FileOf("current.valdrio")))
        {
            Assert.AreEqual(1,file.Runs.ReadRuns(company,study).Runs.Count);
            Assert.AreEqual(original,CanonicalSnapshotJson.Serialize(file.Allocations.ReadAllocations(company,study).SecondaryPublication));
        }
        using var reopened=Service().Open(FileOf("current.valdrio"));
        Assert.AreEqual(1,reopened.Runs.ReadRuns(company,study).Runs.Count);
    }

}
