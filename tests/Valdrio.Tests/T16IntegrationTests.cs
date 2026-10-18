using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Infrastructure.CompanyFiles;
namespace Valdrio.Tests;
public sealed partial class T06ImportConfirmationTests
{
    private async Task<AllocationWorkspaceSnapshot> T16Fixture(ICompanyFileSession file)
    {
        var company=file.Read();
        file.AddUnits(DefaultActivityUnitCatalog.Missing(company.Units),company.Revision,"fixture");
        var centers=new[]{("A","Auxiliar A"),("B","Auxiliar B"),("P","Producción")}.Select(x=>file.Models.CreateCenter(new(company.Id,x.Item1,x.Item2,"fixture"))).ToArray();
        var draft=file.Models.CreateDraft(new(company.Id,"T16 ejemplo sintético","fixture"));var m=draft.Structure;
        var nodes=centers.Select(c=>new CalculationNodeDefinition(Id(),m.Id,c.Id,c.Code,c.Code == "P" ? NodeRole.Principal : NodeRole.Auxiliary,["production"],true)).ToArray();
        var unit=file.Models.ReadDriverUnits().First(u=>u.IsActive && u.Definition.Code=="machine_hour").Definition.Id;
        var pools=nodes.Select(n=>new CostPoolDefinition(Id(),m.Id,n.Id,unit,n.Code,CostLayer.Unclassified,ModelPolicyReference.PendingCapacity)).ToArray();
        var saved=file.Models.SaveDraft(new(new(m.Id,m.CompanyId,m.FamilyId,m.VersionNumber,m.ParentVersionId,m.Description,m.PrecisionPolicyVersion,m.MethodPolicy,nodes,pools),draft.Revision,"fixture"));
        file.Models.Publish(new(company.Id,m.Id,saved.Revision,"fixture"));
        var study=file.Studies.Create(new(company.Id,Id(),"T16 · EJEMPLO FICTICIO",BusinessDate.ParseIso("2026-10-01"),BusinessDate.ParseIso("2026-10-31"),StudyPurpose.Management,StudyScenario.Actual,m.Id,new("EUR"),"fixture"));
        var profile=new SourceParseProfileVersion(SourceParseProfileVersion.CurrentSchema,Id(),1,"T16 fixture",ImportSourceKind.Csv,null,1,2,
            new(3,description:2,amount:1),new("utf-8",';','"',CsvBomPolicy.Optional),new(",",".",[3],ImportNegativeSyntax.LeadingMinus),
            "yyyy-MM-dd",ImportAmountMode.PeriodAmount,ImportSignConvention.SignedAmount,study.PeriodStart,study.PeriodEnd,new("EUR"),null,
            ImportDuplicatePolicy.Reject,ImportZeroPolicy.Keep,ImportAccountSelection.All,new(ImportProfileApprovalState.ApprovedBusiness,"Synthetic T16 fixture"),true);
        var prepared=Prepare(file,study,await Source(profile,"Amount;Description;Account\n1000;Fijo A;640000\n600;Fijo B;622000\n0;Fijo P;640001"),profile);
        file.Imports.ConfirmImport(Command(prepared));
        var w=file.Resources.ReadResources(company.Id,study.Id);var assignments=new List<ExplicitPrimaryAssignment>();
        var codes=new[]{"640000","622000","640001"};
        for(int i=0;i<3;i++)
        {
            var index=i;var origin=w.Origins.Single(o=>o.Code==codes[i]);var variable=false;
            var classification=new ResourceClassification(variable?ResourceBehavior.Variable:ResourceBehavior.Fixed,null,ResourceCostLayer.Production,
                variable?ResourceCapacityTreatment.None:ResourceCapacityTreatment.AssessAtPool,
                variable?null:new(pools[index].Id,nodes[index].Id,unit)){VariablePercentage=variable?100:0};
            var part=new ResourcePartDraft(Id(),w.Preparation.Coverage.Single(c=>c.ResourceId==origin.Id).Effective,ResourceRoutingKind.AllocationPool,poolId:pools[index].Id,classification:classification);
            file.Resources.ChangeResources(Mutation(w,ResourceMutationKind.EditResource,origin.Id,[part]));w=file.Resources.ReadResources(company.Id,study.Id);
            var key=file.Allocations.SaveAllocationKey(KeyCommand(w,lines:[new(nodes[index].Id,100m)]) with {Name="Primario "+codes[i]}).After;
            assignments.Add(new(part.Id,key.Id));
        }
        var secondary = file.Allocations.SaveAllocationKey(KeyCommand(w, stage: AllocationStage.Secondary, lines: [new(nodes[2].Id,100m)])).After;
        var rules = nodes.Take(2).Select(n => new SecondaryRule(Id(),company.Id,m.Id,n.Id,"all",secondary.Id,true)).ToArray();
        file.Allocations.ChangeAllocations(AllocationCommand(w,new([],rules,assignments,[],[])));
        w=file.Resources.ReadResources(company.Id,study.Id);
        for(int i=0;i<3;i++) file.PhysicalActivity.Save(new(company.Id,study.Id,w.Study.Revision,nodes[i].Id,pools[i].Id,0,
            PhysicalObservationState.Confirmed,null,null,100, 50,
            new(PhysicalEvidenceQuality.Declared,"Referencia aprobada","Ejemplo"),new(PhysicalEvidenceQuality.Recorded,"Partes de trabajo","Ejemplo"),null,null,null,"fixture"));
        var current=file.Allocations.ReadAllocations(company.Id,study.Id);
        var physical=file.PhysicalActivity.ReadCurrent(company.Id,study.Id);
        var plan=new AuxiliaryCapacityPlan(nodes.Take(2).Select((n,i) => new AuxiliaryCapacityRow(n.Id,pools[i].Id,AuxiliaryCapacityMode.OwnerOnceCapacity,
            physical.Single(o=>o.NodeId==n.Id),[new("all",i==0 ? [new(nodes[1].Id,10),new(nodes[2].Id,40)] : [new(nodes[2].Id,50)])])).ToArray());
        file.Allocations.ChangeAllocations(AllocationCommand(current.Resources,current.Configuration.WithAuxiliaryCapacity(plan)));
        return file.Allocations.ReadAllocations(company.Id,study.Id);
    }

    [TestMethod]
    public async Task T16_C20_PersistsTransmitsOnceAndRestoresHistoricalRates()
    {
        EntityId company, studyId; string exported;
        using(var file=Baseline())
        {
            var w=await T16Fixture(file);var s=w.Resources.Study;company=s.CompanyId;studyId=s.Id;
            var started=file.Runs.PrepareRun(new(company,studyId,s.Revision,Id(),"test"));
            var output=CalculationRunService.Calculate(started);
            Assert.IsNotNull(output.Secondary,string.Join(",",output.Diagnostics.Select(d=>d.Code)));
            Assert.AreEqual(800m,output.Secondary.Facts.Where(f=>!f.IsCapacityVariance).Sum(f=>f.PostedAmount.Amount));
            Assert.AreEqual(800m,output.Secondary.Facts.Where(f=>f.IsCapacityVariance).Sum(f=>f.PostedAmount.Amount));
            Assert.AreEqual(1600m,output.Secondary.Facts.Sum(f=>f.PostedAmount.Amount));
            Assert.IsTrue(output.Secondary.Reconciliations.All(r=>r.Difference.Amount==0));
            Assert.IsTrue(output.Secondary.Facts.Where(f=>!f.IsCapacityVariance).All(f=>f.AssessmentId is not null));
            file.Runs.PublishRun(started.Id,output);
            var report=PrincipalRateReportService.Read(s,w.Resources.Model,file.Runs.ReadRuns(company,studyId),file.PhysicalActivity.ReadCurrent(company,studyId));
            Assert.AreEqual(16m,report.Rows.Single().Result!.CombinedRate);
            Assert.IsTrue(report.Rows.Single().Result!.Components.All(c => c.Source.AssessmentId is not null && c.Source.CapacityOwnerPoolId is not null));
            Assert.AreEqual(0m,report.Rows.Single().Result!.CapacityVariance);
            exported=CanonicalSnapshotJson.Serialize(report);
            if(Environment.GetEnvironmentVariable("VALDRIO_T16_DEMO") is {Length:>0} path) file.Backup(path);
            var row=w.Configuration.AuxiliaryCapacity!.Rows[0];var o=row.Observation!;
            file.PhysicalActivity.Save(new(company,studyId,s.Revision,o.NodeId,o.PoolId,o.Version,PhysicalObservationState.Confirmed,null,null,100,40,o.NormalEvidence,o.RealEvidence,null,null,"Correction","test"));
            Assert.IsNull(file.Runs.ReadRuns(company,studyId).MatchingCompleted(s.Revision));
            Assert.AreEqual("AUX_PHYSICAL_STALE",Assert.ThrowsExactly<DomainRuleException>(()=>file.Runs.PrepareRun(new(company,studyId,s.Revision,Id(),"test"))).Diagnostic.Code);
            Assert.AreEqual(exported,CanonicalSnapshotJson.Serialize(PrincipalRateReportService.RestoreVersion(report,file.Runs,file.PhysicalActivity)));
        }
        using var reopened=Service().Open(FileOf("current.valdrio"));
        Assert.IsNotNull(reopened.Allocations.ReadAllocations(company,studyId).Configuration.AuxiliaryCapacity);
        var restored=CanonicalSnapshotJson.Deserialize<PrincipalRateReport>(exported);
        Assert.AreEqual(exported,CanonicalSnapshotJson.Serialize(PrincipalRateReportService.RestoreVersion(restored,reopened.Runs,reopened.PhysicalActivity)));
    }
    [TestMethod]
    public async Task T16_C29_MissingCoveragePendingAndSelectiveAssessment()
    {
        using var file=Baseline();var w=await T16Fixture(file);
        var plan=w.Configuration.AuxiliaryCapacity!;var a=plan.Rows[0];var b=plan.Rows[1];
        AllocationWorkspaceSnapshot With(AuxiliaryCapacityRow first, AuxiliaryCapacityRow? second=null) =>
            new(w.Resources,w.Keys,w.Configuration.WithAuxiliaryCapacity(new([first,second??b])),w.ConfigurationRevision);
        var p=w.Resources.Model.Structure.Nodes.Single(n=>n.Role==NodeRole.Principal).Id;
        var over=a with {Observation=a.Observation! with {RealQuantity=120},ServiceSets=[new("all",[new(p,120)])]};
        var result=SecondaryAllocationService.Calculate(With(over));
        Assert.AreEqual(-200m,result.Facts.Where(f=>f.TerminalNodeId==a.NodeId && f.IsCapacityVariance).Sum(f=>f.PostedAmount.Amount));
        Assert.AreEqual(1600m,result.Facts.Sum(f=>f.PostedAmount.Amount));
        var bad=a with {ServiceSets=[new("all",[new(p,40)])]};
        Assert.AreEqual("AUX_SERVICE_COVERAGE_MISMATCH",Assert.ThrowsExactly<DomainRuleException>(()=>SecondaryAllocationService.Calculate(With(bad))).Diagnostic.Code);
        Assert.AreEqual("AUX_CAPACITY_PENDING",Assert.ThrowsExactly<DomainRuleException>(()=>SecondaryAllocationService.Calculate(With(a with {Mode=AuxiliaryCapacityMode.Pending}))).Diagnostic.Code);
        var mixed=SecondaryAllocationService.Calculate(With(a,b with {Mode=AuxiliaryCapacityMode.ThroughFlow}));
        Assert.AreEqual(1100m,mixed.Facts.Where(f=>!f.IsCapacityVariance).Sum(f=>f.PostedAmount.Amount));
        Assert.IsTrue(mixed.Facts.Any(f=>f.CapacityNotEvaluated));
        var idle=a with {Observation=a.Observation! with {RealQuantity=0},ServiceSets=[new("all",[])]};
        var idleB=b with {Observation=b.Observation! with {RealQuantity=0},ServiceSets=[new("all",[])]};
        Assert.AreEqual(1600m,SecondaryAllocationService.Calculate(With(idle,idleB)).Facts.Where(f=>f.IsCapacityVariance).Sum(f=>f.PostedAmount.Amount));
        var receiveB=b with {ServiceSets=[new("all",[new(a.NodeId,10),new(p,40)])]};
        Assert.AreEqual("AUX_ZERO_REAL_RECEIVED",Assert.ThrowsExactly<DomainRuleException>(()=>SecondaryAllocationService.Calculate(With(idle,receiveB))).Diagnostic.Code);
        var closedA=a with {ServiceSets=[new("all",[new(b.NodeId,50)])]};
        var closedB=b with {ServiceSets=[new("all",[new(a.NodeId,50)])]};
        Assert.ThrowsExactly<DomainRuleException>(()=>SecondaryAllocationService.Calculate(With(closedA,closedB)));
    }
    [TestMethod]
    public async Task T16_ReciprocalGroupsClosureAndCultureRemainReproducible()
    {
        EntityId company,studyId;string publication;
        using(var file=Baseline())
        {
            var w=await T16Fixture(file);company=w.Resources.Study.CompanyId;studyId=w.Resources.Study.Id;
            var plan=w.Configuration.AuxiliaryCapacity!; var a=plan.Rows[0];var b=plan.Rows[1];
            var p=w.Resources.Model.Structure.Nodes.Single(n=>n.Role==NodeRole.Principal).Id;
            var reciprocal=b with { ServiceSets=[new("all",[new(a.NodeId,10),new(p,40)])] };
            var config=w.Configuration.WithAuxiliaryCapacity(new([a,reciprocal]));
            var candidate=new AllocationWorkspaceSnapshot(w.Resources,w.Keys,config,w.ConfigurationRevision);
            var result=SecondaryAllocationService.Calculate(candidate);
            Assert.AreEqual(800m,result.Facts.Where(f=>!f.IsCapacityVariance).Sum(f=>f.PostedAmount.Amount));
            Assert.AreEqual(2,result.Facts.Where(f=>!f.IsCapacityVariance).Select(f=>f.AssessmentId).Distinct().Count());
            var presented=Valdrio.Reporting.SecondaryContributionProjector.Build(result.Facts,result.ExplanationLinks,w.Resources.Model.Structure.Nodes.ToDictionary(n=>n.Id));
            Assert.AreEqual(1600m,presented.Sum(f=>f.PostedAmount));
            var oldCulture=System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                var before=CanonicalSnapshotJson.Serialize(result.Facts);
                System.Globalization.CultureInfo.CurrentCulture=System.Globalization.CultureInfo.GetCultureInfo("en-US");
                Assert.AreEqual(before,CanonicalSnapshotJson.Serialize(SecondaryAllocationService.Calculate(candidate).Facts));
                System.Globalization.CultureInfo.CurrentCulture=System.Globalization.CultureInfo.GetCultureInfo("es-ES");
                Assert.AreEqual(before,CanonicalSnapshotJson.Serialize(SecondaryAllocationService.Calculate(candidate).Facts));
            } finally { System.Globalization.CultureInfo.CurrentCulture=oldCulture; }
            var secondRules=config.SecondaryRules.Select(r=>r with { Id=Id(),ComponentRoutingGroup="other" }).ToArray();
            var part=w.Resources.Resources.SelectMany(r=>r.Parts).Single(part=>part.Classification!.CapacityOwner!.NodeId==a.NodeId);
            var grouped=new AllocationConfiguration(config.PrimaryRules,config.SecondaryRules.Concat(secondRules).ToArray(),config.ExplicitAssignments,[],[],
                secondaryPartRoutingGroups:[new(part.Id,"other")],auxiliaryCapacity:new([
                    a with {ServiceSets=a.ServiceSets.Append(new AuxiliaryServiceSet("other",[new(p,50)])).ToArray()},
                    reciprocal with {ServiceSets=reciprocal.ServiceSets.Append(new AuxiliaryServiceSet("other",[new(p,50)])).ToArray()}]));
            var groups=SecondaryAllocationService.Calculate(new AllocationWorkspaceSnapshot(w.Resources,w.Keys,grouped,w.ConfigurationRevision));
            Assert.AreEqual(2,groups.GroupCirculations.Count);
            Assert.AreEqual(800m,groups.Facts.Where(f=>!f.IsCapacityVariance).Sum(f=>f.PostedAmount.Amount));
            var groupedInput=new AllocationWorkspaceSnapshot(w.Resources,w.Keys,grouped,w.ConfigurationRevision);
            Assert.AreNotEqual(CanonicalSnapshotJson.Serialize(AuxiliaryCapacityService.Graph(groupedInput,"all").Q),CanonicalSnapshotJson.Serialize(AuxiliaryCapacityService.Graph(groupedInput,"other").Q));
            file.Allocations.ChangeAllocations(AllocationCommand(w.Resources,config.WithPrimaryClosed(true)));
            w=file.Allocations.ReadAllocations(company,studyId);result=SecondaryAllocationService.Calculate(w);
            var closed=file.Allocations.ChangeAllocations(AllocationCommand(w.Resources,w.Configuration.WithSecondaryClosed(true,SecondaryAllocationService.ResultFingerprint(result,w.Configuration))));
            publication=CanonicalSnapshotJson.Serialize(closed.SecondaryPublication!);
        }
        using var reopened=Service().Open(FileOf("current.valdrio"));
        var saved=reopened.Allocations.ReadAllocations(company,studyId);
        Assert.IsTrue(saved.Configuration.SecondaryClosed);
        Assert.AreEqual(publication,CanonicalSnapshotJson.Serialize(saved.SecondaryPublication!));
        reopened.Allocations.ChangeAllocations(AllocationCommand(saved.Resources,saved.Configuration.WithSecondaryClosed(false)));
        Assert.IsNotNull(reopened.Allocations.ReadAllocations(company,studyId).Configuration.AuxiliaryCapacity);
    }

    [TestMethod]
    public async Task T16_UnclassifiedCostCannotPretendCapacityWasAssessed()
    {
        using var file=Baseline();var w=await T16Fixture(file);var a=w.Configuration.AuxiliaryCapacity!.Rows[0];
        var resource=w.Resources.Resources.Single(r=>r.Parts.Any(p=>p.Classification?.CapacityOwner?.NodeId==a.NodeId));
        var part=resource.Parts.Single();
        var pending=new ResourcePartDraft(part.Id,part.Amount,ResourceRoutingKind.AllocationPool,poolId:part.PoolId,classification:ResourceClassification.Unclassified);
        file.Resources.ChangeResources(Mutation(w.Resources,ResourceMutationKind.EditResource,resource.Id,[pending]));
        var updated=file.Allocations.ReadAllocations(w.Resources.Study.CompanyId,w.Resources.Study.Id);
        Assert.AreEqual("AUX_CLASSIFICATION_REQUIRED",Assert.ThrowsExactly<DomainRuleException>(()=>SecondaryAllocationService.Calculate(updated)).Diagnostic.Code);
    }

}
