using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Infrastructure.CompanyFiles;
namespace Valdrio.Tests;
public sealed partial class T06ImportConfirmationTests
{
    private async Task<AllocationWorkspaceSnapshot> T15Fixture(ICompanyFileSession file)
    {
        var company=file.Read();
        file.AddUnits(DefaultActivityUnitCatalog.Missing(company.Units),company.Revision,"fixture");
        var centers=new[]{("MEC","Mecanizado"),("MON","Montaje")}.Select(x=>file.Models.CreateCenter(new(company.Id,x.Item1,x.Item2,"fixture"))).ToArray();
        var draft=file.Models.CreateDraft(new(company.Id,"T15 ejemplo sintético","fixture"));var m=draft.Structure;
        var nodes=centers.Select(c=>new CalculationNodeDefinition(Id(),m.Id,c.Id,c.Code,NodeRole.Principal,["production"],true)).ToArray();
        var unit=file.Models.ReadDriverUnits().First(u=>u.IsActive && u.Definition.Code=="machine_hour").Definition.Id;
        var pools=nodes.Select(n=>new CostPoolDefinition(Id(),m.Id,n.Id,unit,n.Code,CostLayer.Unclassified,ModelPolicyReference.PendingCapacity)).ToArray();
        var saved=file.Models.SaveDraft(new(new(m.Id,m.CompanyId,m.FamilyId,m.VersionNumber,m.ParentVersionId,m.Description,m.PrecisionPolicyVersion,m.MethodPolicy,nodes,pools),draft.Revision,"fixture"));
        file.Models.Publish(new(company.Id,m.Id,saved.Revision,"fixture"));
        var study=file.Studies.Create(new(company.Id,Id(),"T15 · EJEMPLO FICTICIO",BusinessDate.ParseIso("2026-10-01"),BusinessDate.ParseIso("2026-10-31"),StudyPurpose.Management,StudyScenario.Actual,m.Id,new("EUR"),"fixture"));
        var profile=new SourceParseProfileVersion(SourceParseProfileVersion.CurrentSchema,Id(),1,"T15 fixture",ImportSourceKind.Csv,null,1,2,
            new(3,description:2,amount:1),new("utf-8",';','"',CsvBomPolicy.Optional),new(",",".",[3],ImportNegativeSyntax.LeadingMinus),
            "yyyy-MM-dd",ImportAmountMode.PeriodAmount,ImportSignConvention.SignedAmount,study.PeriodStart,study.PeriodEnd,new("EUR"),null,
            ImportDuplicatePolicy.Reject,ImportZeroPolicy.Keep,ImportAccountSelection.All,new(ImportProfileApprovalState.ApprovedBusiness,"Synthetic T15 fixture"),true);
        var prepared=Prepare(file,study,await Source(profile,"Amount;Description;Account\n1000;Fijo mecanizado;640000\n600;Variable mecanizado;622000\n500;Fijo montaje;640001"),profile);
        file.Imports.ConfirmImport(Command(prepared));
        var w=file.Resources.ReadResources(company.Id,study.Id);var assignments=new List<ExplicitPrimaryAssignment>();
        var codes=new[]{"640000","622000","640001"};
        for(int i=0;i<3;i++)
        {
            var index=i==2?1:0;var origin=w.Origins.Single(o=>o.Code==codes[i]);var variable=i==1;
            var classification=new ResourceClassification(variable?ResourceBehavior.Variable:ResourceBehavior.Fixed,null,ResourceCostLayer.Production,
                variable?ResourceCapacityTreatment.None:ResourceCapacityTreatment.AssessAtPool,
                variable?null:new(pools[index].Id,nodes[index].Id,unit)){VariablePercentage=variable?100:0};
            var part=new ResourcePartDraft(Id(),w.Preparation.Coverage.Single(c=>c.ResourceId==origin.Id).Effective,ResourceRoutingKind.AllocationPool,poolId:pools[index].Id,classification:classification);
            file.Resources.ChangeResources(Mutation(w,ResourceMutationKind.EditResource,origin.Id,[part]));w=file.Resources.ReadResources(company.Id,study.Id);
            var key=file.Allocations.SaveAllocationKey(KeyCommand(w,lines:[new(nodes[index].Id,100m)]) with {Name="Primario "+codes[i]}).After;
            assignments.Add(new(part.Id,key.Id));
        }
        file.Allocations.ChangeAllocations(AllocationCommand(w,new([],[],assignments,[],[])));
        w=file.Resources.ReadResources(company.Id,study.Id);
        for(int i=0;i<2;i++) file.PhysicalActivity.Save(new(company.Id,study.Id,w.Study.Revision,nodes[i].Id,pools[i].Id,0,
            PhysicalObservationState.Confirmed,null,null,100, i==0?60:80,
            new(PhysicalEvidenceQuality.Declared,"Referencia aprobada","Ejemplo"),new(PhysicalEvidenceQuality.Recorded,"Partes de trabajo","Ejemplo"),null,null,null,"fixture"));
        return file.Allocations.ReadAllocations(company.Id,study.Id);
    }
    [TestMethod]
    public async Task T15_FrozenCostsPhysicalVersionsReopeningAndStaleRuns()
    {
        EntityId companyId,studyId;string original;
        using(var file=Baseline())
        {
            var w=await T15Fixture(file);var study=w.Resources.Study;companyId=study.CompanyId;studyId=study.Id;
            var started=file.Runs.PrepareRun(new(companyId,studyId,study.Revision,Id(),"test"));
            var output=CalculationRunService.Calculate(started);Assert.IsNotNull(output.Secondary);
            file.Runs.PublishRun(started.Id,output);
            PrincipalRateReport Read()=>PrincipalRateReportService.Read(study,w.Resources.Model,file.Runs.ReadRuns(companyId,studyId),file.PhysicalActivity.ReadCurrent(companyId,studyId));
            var report=Read(); Assert.AreEqual(2,report.Rows.Count);
            var mec=report.Rows.Single(r=>r.Name.Contains("Mecanizado"));
            Assert.IsNotNull(mec.Result,string.Join(",",mec.Diagnostics));
            Assert.AreEqual(20m,mec.Result.CombinedRate);Assert.AreEqual(400m,mec.Result.CapacityVariance);
            Assert.IsNull(mec.Result.AppliedCost);Assert.AreEqual(1200m,mec.Result.PerformedCost);
            original=CanonicalSnapshotJson.Serialize(report);
            Assert.AreEqual(original,CanonicalSnapshotJson.Serialize(Read()));
            if(Environment.GetEnvironmentVariable("VALDRIO_T15_DEMO") is {Length:>0} demo)file.Backup(demo);
            var obs=mec.Observation!;
            file.PhysicalActivity.Save(new(companyId,studyId,study.Revision,obs.NodeId,obs.PoolId,obs.Version,PhysicalObservationState.Confirmed,
                null,null,100,50,obs.NormalEvidence,obs.RealEvidence,null,null,"Corrección del parte","test"));
            Assert.AreNotEqual(report.Version,Read().Version);Assert.AreEqual(20m,mec.Result.CombinedRate);
            var historical=PrincipalRateReportService.Read(study,w.Resources.Model,file.Runs.ReadRuns(companyId,studyId),
                report.Rows.Select(r=>r.Observation!).ToArray());
            Assert.AreEqual(original,CanonicalSnapshotJson.Serialize(historical));
            var newer=file.Studies.UpdateContext(new(companyId,studyId,study.Revision,"Nuevo",study.PeriodStart,study.PeriodEnd,study.Purpose,study.ScenarioKind,study.SelectedModelVersionId,study.Currency.FunctionalCurrencyCode,"test"));
            var stale=PrincipalRateReportService.Read(newer,w.Resources.Model,file.Runs.ReadRuns(companyId,studyId),file.PhysicalActivity.ReadCurrent(companyId,studyId));
            Assert.IsTrue(stale.Rows.All(r=>r.Result is null && r.Diagnostics.Contains("Rates.MissingCosts")));
        }
        using var reopened=Service().Open(FileOf("current.valdrio"));
        Assert.AreEqual(2,reopened.PhysicalActivity.ReadCurrent(companyId,studyId).Count);
        var restored=CanonicalSnapshotJson.Deserialize<PrincipalRateReport>(original);
        Assert.AreEqual(20m,restored.Rows.Single(r=>r.Name.Contains("Mecanizado")).Result!.CombinedRate);
        Assert.AreEqual(original,CanonicalSnapshotJson.Serialize(PrincipalRateReportService.RestoreVersion(restored,reopened.Runs,reopened.PhysicalActivity)));
    }
}
