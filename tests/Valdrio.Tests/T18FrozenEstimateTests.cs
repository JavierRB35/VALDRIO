using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Valdrio.Application;
using Valdrio.Calculation;
using Valdrio.Domain;
using Valdrio.Infrastructure.CompanyFiles;
namespace Valdrio.Tests;

[TestClass]
public sealed class T18EstimateCalculatorTests
{
    private static EntityId Id() => new(Guid.NewGuid());
    [TestMethod]
    public void C22_PartialBatchCosts420AndUsesFullPrecision()
    {
        var unit = new UnitDefinition(new(Id()),"hour","time","h",12,false);
        var product = new CompositionItem(Id(),"P","Product",unit.Id,CompositionItemKind.Product,true);
        var model=Id(); var setup=new CompositionPool(Id(),model,"Setup",unit.Id,true);
        var process=new CompositionPool(Id(),model,"Process",unit.Id,true);
        var recipe=new CompositionVersion(Id(),product.Id,1,model,50,CompositionState.Published,"",[
            new(Id(),null,null,setup.Id,2,unit.Id,1,ConsumptionBasis.PerStartedBatch,CompositionLossKind.None,0),
            new(Id(),null,null,process.Id,.1m,unit.Id,1,ConsumptionBasis.PerUnit,CompositionLossKind.None,0)]);
        var catalog=new CompositionCatalog(Id(),0,[product],[recipe]);
        var quantities=CompositionCalculator.Preview(catalog,recipe.Id,120,[unit],[setup,process]);
        EstimatePrice[] prices=[new(setup.Id,true,unit.Id,30,"setup"),new(process.Id,true,unit.Id,20,"process")];
        var result=EstimateCalculator.Calculate(quantities,prices,PrecisionPolicy.Pdec1Proposed);
        Assert.AreEqual(3m,result.StartedBatches); Assert.AreEqual(180m,result.Lines[0].Amount);
        Assert.AreEqual(240m,result.Lines[1].Amount); Assert.AreEqual(420m,result.Total); Assert.AreEqual(3.5m,result.UnitCost);
        prices[1]=prices[1] with { UnitCost=20.123456789123m };
        result=EstimateCalculator.Calculate(quantities,prices,PrecisionPolicy.Pdec1Proposed);
        Assert.AreEqual(241.481481469476m,result.Lines[1].RawAmount);
        Assert.AreEqual(result.Total,result.Lines.Sum(l=>l.Amount));
        Assert.AreNotEqual(180m+12m*20.12m,result.Total);
        prices[1]=prices[1] with { UnitCost=null };
        Assert.AreEqual("ESTIMATE_MISSINGRATE",Assert.ThrowsExactly<DomainRuleException>(()=>EstimateCalculator.Calculate(quantities,prices,PrecisionPolicy.Pdec1Proposed)).Diagnostic.Code);
        prices[1]=prices[1] with { UnitCost=0 };
        Assert.AreEqual(180m,EstimateCalculator.Calculate(quantities,prices,PrecisionPolicy.Pdec1Proposed).Total);
    }
}

public sealed partial class T06ImportConfirmationTests
{
    [TestMethod]
    public void T18_UpgradeSchema12CopiesSourceAndPreservesCatalog()
    {
        var source=FileOf("schema12.valdrio");EntityId company;
        using(var file=Service().Create(new(source,"Upgrade",new("EUR"),PrecisionPolicy.Pdec1Proposed))) company=file.Read().Id;
        // Restore the exact empty schema-12 shape, before any estimate or upgrade audit exists.
        using(var connection=new SqliteConnection("Data Source="+source+";Pooling=False"))
        {
            connection.Open();using var command=connection.CreateCommand();
            command.CommandText="DROP TABLE Estimates; DELETE FROM __EFMigrationsHistory WHERE MigrationId='202610250013_Estimates'; UPDATE FileMetadata SET SchemaVersion=12;";
            command.ExecuteNonQuery();
        }
        var hash=Hash(source);Assert.IsTrue(Service().RequiresUpgrade(source));
        using var upgraded=Service().Upgrade(source,FileOf("upgraded.valdrio"),"test");
        Assert.AreEqual(hash,Hash(source));Assert.AreEqual(13,upgraded.Read().SchemaVersion);
        Assert.AreEqual(company,upgraded.Compositions.ReadCompositions().CompanyId);
        Assert.AreEqual(0,upgraded.Estimates.ReadEstimates(Id()).Count);
    }
    [TestMethod]
    public async Task T18_AcceptRollbackAndAuditTampering()
    {
        EstimateRequest request;EstimatePreview preview;
        using(var file=Service().Create(new(FileOf("current.valdrio"),"Rollback",new("EUR"),PrecisionPolicy.Pdec1Proposed)))
        {
            (request,_)=await T18Fixture(file);preview=file.Estimates.PreviewEstimate(request);
        }
        var original=Hash(FileOf("current.valdrio"));
        using(var broken=Service(point=>{if(point==CompanyWriteCheckpoint.BeforeCommit)throw new OperationCanceledException();}).Open(FileOf("current.valdrio")))
        {
            Assert.ThrowsExactly<OperationCanceledException>(()=>broken.Estimates.AcceptEstimate(request,preview.InputHash,"test"));
            Assert.AreEqual(0,broken.Estimates.ReadEstimates(request.StudyId).Count);
        }
        Assert.AreEqual(original,Hash(FileOf("current.valdrio")));
        using(var file=Service().Open(FileOf("current.valdrio")))file.Estimates.AcceptEstimate(request,preview.InputHash,"test");
        using(var connection=new SqliteConnection("Data Source="+FileOf("current.valdrio")+";Pooling=False"))
        {
            connection.Open();using var command=connection.CreateCommand();
            command.CommandText="UPDATE AuditEvents SET Actor='tampered' WHERE AggregateType='Estimate'";command.ExecuteNonQuery();
        }
        Assert.ThrowsExactly<CompanyFileException>(()=>Service().Open(FileOf("current.valdrio")));
    }
    [TestMethod]
    public void T18_MappingAndMigrationSnapshotHaveNoPendingChanges()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open(); using var context = new EstimateDbContext(connection);
        var snapshot = context.GetService<IMigrationsAssembly>().ModelSnapshot!.Model;
        var initialized = context.GetService<IModelRuntimeInitializer>().Initialize(snapshot, designTime: true);
        var changes = context.GetService<IMigrationsModelDiffer>().GetDifferences(initialized.GetRelationalModel(),
            context.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model.GetRelationalModel());
        Assert.AreEqual(0, changes.Count, string.Join("\n", changes.Select(c => c switch
        {
            AlterColumnOperation a => a.Table + "." + a.Name + " nullable " + a.OldColumn.IsNullable + "→" + a.IsNullable,
            AddColumnOperation a => "add " + a.Table + "." + a.Name,
            CreateIndexOperation a => "index " + a.Table + "." + a.Name,
            _ => c.GetType().Name
        })));
        Assert.IsFalse(context.Database.HasPendingModelChanges());
    }
    private async Task<(EstimateRequest Request, CompositionCatalog Catalog)> T18Fixture(ICompanyFileSession file)
    {
        var w=await T15Fixture(file);var study=w.Resources.Study;
        var run=file.Runs.PrepareRun(new(study.CompanyId,study.Id,study.Revision,Id(),"test"));
        file.Runs.PublishRun(run.Id,CalculationRunService.Calculate(run));
        var pool=w.Resources.Model.Structure.Pools[0];
        var product=new CompositionItem(Id(),"T18-P","Producto estimado",pool.DriverUnitId,CompositionItemKind.Product,true);
        var material=new CompositionItem(Id(),"T18-M","Material",pool.DriverUnitId,CompositionItemKind.Material,true)
            {ReferenceUnitCost=5,ReferenceCostSource="fixture EUR"};
        var recipe=new CompositionVersion(Id(),product.Id,1,study.SelectedModelVersionId,50,CompositionState.Published,"",[
            new(Id(),null,null,pool.Id,2,pool.DriverUnitId,1,ConsumptionBasis.PerStartedBatch,CompositionLossKind.None,0),
            new(Id(),material.Id,null,null,1,pool.DriverUnitId,1,ConsumptionBasis.PerUnit,CompositionLossKind.None,0)]);
        var c=file.Compositions.ReadCompositions();
        c=file.Compositions.SaveCompositions(c with {Items=[product,material],Versions=[recipe]},c.Revision,"test");
        return(new(study.CompanyId,study.Id,Id(),"ORDER-001",recipe.Id,run.Id,120),c);
    }
    [TestMethod]
    public async Task T18_FrozenHistorySurvivesPricesPhysicalRecipeChangesAndReopen()
    {
        string frozen; EstimateRequest request; EntityId acceptedId;
        using(var file=Service().Create(new(FileOf("current.valdrio"),"T18 demo",new("EUR"),PrecisionPolicy.Pdec1Proposed)))
        {
            var fixture=await T18Fixture(file); request=fixture.Request;var c=fixture.Catalog;
            var before=CanonicalSnapshotJson.Serialize(file.Resources.ReadResources(request.CompanyId,request.StudyId));
            var preview=file.Estimates.PreviewEstimate(request);
            var accepted=file.Estimates.AcceptEstimate(request,preview.InputHash,"test");acceptedId=accepted.Id;
            frozen=CanonicalSnapshotJson.Serialize(accepted);
            Assert.AreEqual("Estimate",accepted.FactKind);Assert.AreEqual(1,accepted.Version);
            Assert.AreEqual(before,CanonicalSnapshotJson.Serialize(file.Resources.ReadResources(request.CompanyId,request.StudyId)));
            var revision=request with {PreviousEstimateId=accepted.Id};
            var stale=file.Estimates.PreviewEstimate(revision);
            var recipe=c.Versions[0];
            c=file.Compositions.SaveCompositions(c with {Items=c.Items.Select(i=>i.Kind==CompositionItemKind.Material?i with {ReferenceUnitCost=8}:i).ToArray(),
                Versions=[recipe,recipe with {Id=Id(),Number=2,BatchSize=25}]},c.Revision,"test");
            Assert.AreEqual("ESTIMATE_CHANGED",Assert.ThrowsExactly<DomainRuleException>(()=>file.Estimates.AcceptEstimate(revision,stale.InputHash,"test")).Diagnostic.Code);
            var obs=file.PhysicalActivity.ReadCurrent(request.CompanyId,request.StudyId).First();
            var study=file.Studies.ReadStudies().Single(x=>x.Id==request.StudyId);
            file.PhysicalActivity.Save(new(study.CompanyId,study.Id,study.Revision,obs.NodeId,obs.PoolId,obs.Version,PhysicalObservationState.Confirmed,
                null,null,100,50,obs.NormalEvidence,obs.RealEvidence,null,null,"Revisión","test"));
            Assert.AreEqual(frozen,CanonicalSnapshotJson.Serialize(file.Estimates.ReadEstimates(request.StudyId).Single()));
            revision=revision with {RecipeVersionId=c.Versions[1].Id};
            var newer=file.Estimates.PreviewEstimate(revision);var second=file.Estimates.AcceptEstimate(revision,newer.InputHash,"test");
            Assert.AreEqual(2,second.Version);Assert.AreNotEqual(accepted.Preview.Cost.Total,second.Preview.Cost.Total);
            Assert.AreEqual("ESTIMATE_CHANGED",Assert.ThrowsExactly<DomainRuleException>(()=>file.Estimates.AcceptEstimate(revision,newer.InputHash,"test")).Diagnostic.Code);
            var duplicate=request with {OrderId=Id()};var dupPreview=file.Estimates.PreviewEstimate(duplicate);
            Assert.AreEqual("ESTIMATE_DUPLICATEORDER",Assert.ThrowsExactly<DomainRuleException>(()=>file.Estimates.AcceptEstimate(duplicate,dupPreview.InputHash,"test")).Diagnostic.Code);
            Assert.AreEqual(before,CanonicalSnapshotJson.Serialize(file.Resources.ReadResources(request.CompanyId,request.StudyId)));
            if(Environment.GetEnvironmentVariable("VALDRIO_T18_DEMO") is {Length:>0} demo)file.Backup(demo);
        }
        using(var reopened=Service().Open(FileOf("current.valdrio")))
        {
            var history=reopened.Estimates.ReadEstimates(request.StudyId);
            Assert.AreEqual(2,history.Count);Assert.AreEqual(frozen,CanonicalSnapshotJson.Serialize(history.Single(e=>e.Id==acceptedId)));
        }
        using var readOnly=Service().Open(FileOf("current.valdrio"),CompanyFileOpenMode.ReadOnly);
        Assert.AreEqual(2,readOnly.Estimates.ReadEstimates(request.StudyId).Count);
        Assert.AreEqual("FILE_READ_ONLY",Assert.ThrowsExactly<CompanyFileException>(()=>readOnly.Estimates.AcceptEstimate(request,"hash","test")).Diagnostic.Code);
    }
    [TestMethod]
    public async Task T18_MissingPriceCurrencyAndCancellationDoNotWrite()
    {
        using var file=Service().Create(new(FileOf("current.valdrio"),"T18 demo",new("EUR"),PrecisionPolicy.Pdec1Proposed));var (request,c)=await T18Fixture(file);
        var p=file.Estimates.PreviewEstimate(request);var hash=Hash(file.Path);
        using var cancel=new CancellationTokenSource();cancel.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(()=>file.Estimates.AcceptEstimate(request,p.InputHash,"test",cancel.Token));
        Assert.AreEqual(hash,Hash(file.Path));
        Assert.AreEqual("ESTIMATE_CURRENCYMISMATCH",Assert.ThrowsExactly<DomainRuleException>(()=>EstimateService.Calculate(p.Input with {ReferenceCurrency="USD"})).Diagnostic.Code);
        c=file.Compositions.SaveCompositions(c with {Items=c.Items.Select(i=>i.Kind==CompositionItemKind.Material?i with {ReferenceUnitCost=null,ReferenceCostSource=null}:i).ToArray()},c.Revision,"test");
        Assert.AreEqual("ESTIMATE_MISSINGPRICE",Assert.ThrowsExactly<DomainRuleException>(()=>file.Estimates.PreviewEstimate(request)).Diagnostic.Code);
        Assert.AreEqual(0,file.Estimates.ReadEstimates(request.StudyId).Count);
        var study=file.Studies.ReadStudies().Single(s=>s.Id==request.StudyId);
        file.Studies.ChangeState(new(study.CompanyId,study.Id,study.Revision,StudyState.Closed,"test"));
        Assert.AreEqual("ESTIMATE_READONLY",Assert.ThrowsExactly<DomainRuleException>(()=>file.Estimates.AcceptEstimate(request,p.InputHash,"test")).Diagnostic.Code);
    }
}
