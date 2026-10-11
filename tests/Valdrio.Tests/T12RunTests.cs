using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Infrastructure.CompanyFiles;
namespace Valdrio.Tests;
public sealed partial class T06ImportConfirmationTests
{
    private async Task<AllocationWorkspaceSnapshot> T12Fixture(ICompanyFileSession file, bool closedLoop=false)
    {
        var company=file.Read();
        var centers=new[]{("P12","Producción"),("M12","Mantenimiento"),("A12","Administración")}
            .Select(x=>file.Models.CreateCenter(new(company.Id,x.Item1,x.Item2,"fixture"))).ToArray();
        var draft=file.Models.CreateDraft(new(company.Id,"T12 — ejemplo sintético 6500 EUR","fixture")); var m=draft.Structure;
        var nodes=centers.Select((c,i)=>new CalculationNodeDefinition(Id(),m.Id,c.Id,c.Name,i==0?NodeRole.Principal:NodeRole.Auxiliary,["production"],true)).ToArray();
        var unit=file.Models.ReadDriverUnits().First(u=>u.IsActive).Definition.Id;
        var pools=nodes.Select(n=>new CostPoolDefinition(Id(),m.Id,n.Id,unit,n.Code,CostLayer.Unclassified,ModelPolicyReference.PendingCapacity)).ToArray();
        var saved=file.Models.SaveDraft(new(new(m.Id,m.CompanyId,m.FamilyId,m.VersionNumber,m.ParentVersionId,m.Description,m.PrecisionPolicyVersion,m.MethodPolicy,nodes,pools),draft.Revision,"fixture"));
        file.Models.Publish(new(company.Id,m.Id,saved.Revision,"fixture"));
        var study=file.Studies.Create(new(company.Id,Id(),"T12 DEMO — FICTICIA",BusinessDate.ParseIso("2026-10-01"),BusinessDate.ParseIso("2026-10-31"),StudyPurpose.Management,StudyScenario.Actual,m.Id,new("EUR"),"fixture"));
        var profile=new SourceParseProfileVersion(SourceParseProfileVersion.CurrentSchema,Id(),1,"T12 fixture",ImportSourceKind.Csv,null,1,2,
            new(3,description:2,amount:1),new("utf-8",';','"',CsvBomPolicy.Optional),new(",",".",[3],ImportNegativeSyntax.LeadingMinus),
            "yyyy-MM-dd",ImportAmountMode.PeriodAmount,ImportSignConvention.SignedAmount,study.PeriodStart,study.PeriodEnd,new("EUR"),null,
            ImportDuplicatePolicy.Reject,ImportZeroPolicy.Keep,ImportAccountSelection.All,new(ImportProfileApprovalState.ApprovedBusiness,"Synthetic T12 fixture"),true);var prepared=Prepare(file,study,await Source(profile,"Amount;Ignored;Account\n2660;Gastos de personal;640000\n2304;Reparaciones;622000\n1536;Servicios informáticos;629000"),profile);
        file.Imports.ConfirmImport(Command(prepared));
        var w=file.Resources.ReadResources(company.Id,study.Id);
        var codes=new[]{"640000","622000","629000"}; var assignments=new List<ExplicitPrimaryAssignment>();
        for(var i=0;i<3;i++)
        {
            var origin=w.Origins.Single(o=>o.Code==codes[i]);var r=w.Resources.Single(r=>r.Id==origin.Id);
            var part=new ResourcePartDraft(Id(),w.Preparation.Coverage.Single(c=>c.ResourceId==origin.Id).Effective,ResourceRoutingKind.AllocationPool,poolId:pools[i].Id,
                classification:new(ResourceBehavior.Fixed,null,ResourceCostLayer.Production,ResourceCapacityTreatment.None,null){VariablePercentage=0});
            file.Resources.ChangeResources(Mutation(w,ResourceMutationKind.EditResource,origin.Id,[part]));
            w=file.Resources.ReadResources(company.Id,study.Id);
            var key=file.Allocations.SaveAllocationKey(KeyCommand(w,lines:[new(nodes[i].Id,100m)]) with {Name="Primario "+nodes[i].Code}).After;
            assignments.Add(new(part.Id,key.Id));
        }
        var rules=new List<SecondaryRule>();
        for(var i=1;i<3;i++)
        {
            AllocationKeyLine[] lines=closedLoop?[new(nodes[3-i].Id,100m)]:[new(nodes[0].Id,80m),new(nodes[3-i].Id,20m)];
            var key=file.Allocations.SaveAllocationKey(KeyCommand(w,stage:AllocationStage.Secondary,lines:lines) with {Name="Secundario "+nodes[i].Code}).After;
            rules.Add(new(Id(),company.Id,m.Id,nodes[i].Id,"all",key.Id,true));
        }
        file.Allocations.ChangeAllocations(AllocationCommand(w,new([],rules,assignments,[],[new("640000","Gastos de personal"),new("622000","Reparaciones"),new("629000","Servicios informáticos")])));
        return file.Allocations.ReadAllocations(company.Id,study.Id);
    }
    [TestMethod]
    public async Task T12_6500FlowsPersistAndHistoricalReadDoesNotRecalculate()
    {
        EntityId company,study,runId;string json;
        using(var file=Baseline())
        {
            var w=await T12Fixture(file);company=w.Resources.Study.CompanyId;study=w.Resources.Study.Id;
            var run=file.Runs.PrepareRun(new(company,study,w.Resources.Study.Revision,Id(),"fixture"));runId=run.Id;
            var output=CalculationRunService.Calculate(run);
            Assert.IsNotNull(output.Secondary);Assert.AreEqual(6500m,output.Secondary.Facts.Sum(f=>f.PostedAmount.Amount));
            CollectionAssert.AreEquivalent(new[]{2176m,544m,1664m,416m},output.Transfers.Select(t=>t.Amount).ToArray());
            Assert.AreEqual(960m,output.Transfers.Where(t=>t.Internal).Sum(t=>t.Amount));
            Assert.IsTrue(output.Secondary.ResourceBridges.All(b=>b.Difference.Amount==0));
            var completed=file.Runs.PublishRun(run.Id,output);json=CanonicalSnapshotJson.Serialize(completed);
            Assert.AreEqual(run.Id,file.Runs.ReadRuns(company,study).CurrentRunId);
            // Preserve a user-reviewable fixture with a completed current run.
            if(Environment.GetEnvironmentVariable("VALDRIO_T12_DEMO") is {Length:>0} demo) file.Backup(demo);
            var current=file.Studies.ReadStudies().Single(s=>s.Id==study);
            file.Studies.UpdateContext(new(company,study,current.Revision,"Revisión posterior",current.PeriodStart,current.PeriodEnd,current.Purpose,current.ScenarioKind,current.SelectedModelVersionId,current.Currency.FunctionalCurrencyCode,"test"));
            Assert.IsNull(file.Runs.ReadRuns(company,study).CurrentRunId);
            Assert.AreEqual(json,CanonicalSnapshotJson.Serialize(file.Runs.ReadRuns(company,study).Runs.Single()));
        }
        using var reopened=Service().Open(FileOf("current.valdrio"));
        Assert.AreEqual(json,CanonicalSnapshotJson.Serialize(reopened.Runs.ReadRuns(company,study).Runs.Single(r=>r.Id==runId)));
    }
    [TestMethod]
    public async Task T12_RevisionChangeWhileCalculatingPublishesHistoryOnly()
    {
        using var file=Baseline();var w=await T12Fixture(file);var s=w.Resources.Study;
        var run=file.Runs.PrepareRun(new(s.CompanyId,s.Id,s.Revision,Id(),"test"));
        var output=CalculationRunService.Calculate(run);
        file.Studies.UpdateContext(new(s.CompanyId,s.Id,s.Revision,"Changed",s.PeriodStart,s.PeriodEnd,s.Purpose,s.ScenarioKind,s.SelectedModelVersionId,s.Currency.FunctionalCurrencyCode,"test"));
        file.Runs.PublishRun(run.Id,output);Assert.IsNull(file.Runs.ReadRuns(s.CompanyId,s.Id).CurrentRunId);
    }
    [TestMethod]
    public async Task T12_ClosedLoopIsPersistedAsFailureWithoutInventedFinalFacts()
    {
        using var file=Baseline();var w=await T12Fixture(file,true);var s=w.Resources.Study;
        var run=file.Runs.PrepareRun(new(s.CompanyId,s.Id,s.Revision,Id(),"test"));var output=CalculationRunService.Calculate(run);
        Assert.IsNull(output.Secondary);Assert.AreEqual(6500m,output.Primary!.Facts.Sum(f=>f.PostedAmount.Amount));
        Assert.IsTrue(output.Diagnostics.Count>0);Assert.AreEqual(CalculationRunStatus.Failed,file.Runs.PublishRun(run.Id,output).Status);
        Assert.IsNull(file.Runs.ReadRuns(s.CompanyId,s.Id).CurrentRunId);
    }
    [TestMethod]
    public async Task T12_CancellationBeforePublishLeavesNoPartialResultsAndPriorRunSurvives()
    {
        using var file=Baseline();var w=await T12Fixture(file);var s=w.Resources.Study;
        var first=file.Runs.PrepareRun(new(s.CompanyId,s.Id,s.Revision,Id(),"test"));file.Runs.PublishRun(first.Id,CalculationRunService.Calculate(first));
        var run=file.Runs.PrepareRun(new(s.CompanyId,s.Id,s.Revision,Id(),"test"));var output=CalculationRunService.Calculate(run);
        using var cancel=new CancellationTokenSource();cancel.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(()=>file.Runs.PublishRun(run.Id,output,cancel.Token));
        file.Runs.CancelRun(run.Id);var history=file.Runs.ReadRuns(s.CompanyId,s.Id);
        Assert.AreEqual(first.Id,history.CurrentRunId);Assert.IsNull(history.Runs.Single(r=>r.Id==run.Id).Output);
    }
    [TestMethod]
    [DataRow(CompanyWriteCheckpoint.AfterFirstWrite)]
    [DataRow(CompanyWriteCheckpoint.BeforeCommit)]
    public async Task T12_PublishFailureRollsBackPersistedOutput(CompanyWriteCheckpoint checkpoint)
    {
        CalculationRun run;RunOutput output;
        using(var file=Baseline())
        {
            var w=await T12Fixture(file);var s=w.Resources.Study;
            run=file.Runs.PrepareRun(new(s.CompanyId,s.Id,s.Revision,Id(),"test"));output=CalculationRunService.Calculate(run);
        }
        using(var failing=Service(p=>{if(p==checkpoint)throw new InvalidOperationException("Injected publication failure");}).Open(FileOf("current.valdrio")))
            Assert.ThrowsExactly<InvalidOperationException>(()=>failing.Runs.PublishRun(run.Id,output));
        using var reopened=Service().Open(FileOf("current.valdrio"));
        var saved=reopened.Runs.ReadRuns(run.Snapshot.CompanyId,run.Snapshot.StudyId).Runs.Single();
        Assert.AreEqual(CalculationRunStatus.Started,saved.Status);Assert.IsNull(saved.Output);
    }
    [TestMethod]
    public async Task T12_CultureDoesNotChangeHashesOrLedgerAndFinalRunsAreImmutable()
    {
        using var file=Baseline();var w=await T12Fixture(file);var s=w.Resources.Study;
        var run=file.Runs.PrepareRun(new(s.CompanyId,s.Id,s.Revision,Id(),"test"));
        var originalCulture=System.Globalization.CultureInfo.CurrentCulture;string? expected=null;
        var economicHash=run.EconomicInputHash;var fullHash=run.FullSnapshotHash;
        try
        {
            foreach(var culture in new[]{"es-ES","en-US","tr-TR"})
            {
                System.Globalization.CultureInfo.CurrentCulture=System.Globalization.CultureInfo.GetCultureInfo(culture);
                var json=CanonicalSnapshotJson.Serialize(CalculationRunService.Calculate(run));expected??=json;Assert.AreEqual(expected,json);
                Assert.AreEqual(economicHash,run.EconomicInputHash);Assert.AreEqual(fullHash,run.FullSnapshotHash);
            }
        }
        finally {System.Globalization.CultureInfo.CurrentCulture=originalCulture;}
        var output=CalculationRunService.Calculate(run);file.Runs.PublishRun(run.Id,output);
        Assert.ThrowsExactly<InvalidOperationException>(()=>file.Runs.PublishRun(run.Id,output));
        Assert.ThrowsExactly<Microsoft.Data.Sqlite.SqliteException>(()=>Query(file.Path,"DELETE FROM CalculationRuns"));
    }

}

