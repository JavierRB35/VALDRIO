using Valdrio.Infrastructure.CompanyFiles;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Valdrio.Application;
using Valdrio.Calculation;
using Valdrio.Domain;
namespace Valdrio.Tests;
[TestClass]
public sealed class T17CompositionTests
{
    private static EntityId Id() => new(Guid.NewGuid());
    private static (CompositionCatalog Catalog, UnitDefinition[] Units, CompositionPool[] Pools) Example()
    {
        var piece = new UnitDefinition(new(Id()), "piece", "count", "ud", 0, true);
        var kg = new UnitDefinition(new(Id()), "kg", "mass", "kg", 12, false);
        var hour = new UnitDefinition(new(Id()), "hour", "time", "h", 12, false);
        var product = new CompositionItem(Id(), "P", "Product", piece.Id, CompositionItemKind.Product, true);
        var material = new CompositionItem(Id(), "M", "Material", kg.Id, CompositionItemKind.Material, true);
        var model = Id(); var pool = new CompositionPool(Id(), model, "Setup", hour.Id, true);
        var recipe = new CompositionVersion(Id(), product.Id, 1, model, 50, CompositionState.Published, "", [
            new(Id(),material.Id,null,null,2,kg.Id,1,ConsumptionBasis.PerUnit,CompositionLossKind.ExtraInput,10),
            new(Id(),null,null,pool.Id,2,hour.Id,1,ConsumptionBasis.PerStartedBatch,CompositionLossKind.None,0),
            new(Id(),null,null,pool.Id,1,hour.Id,1,ConsumptionBasis.PerOrder,CompositionLossKind.None,0)]);
        return (new(Id(), 0, [product,material], [recipe]), [piece,kg,hour], [pool]);
    }
    [TestMethod] public void T17_StartedBatchesExtraInputAndOrderAreDistinct()
    {
        var (c,u,p)=Example(); var r=CompositionCalculator.Preview(c,c.Versions[0].Id,120,u,p);
        Assert.AreEqual(3m,r.StartedBatches); Assert.AreEqual(264m,r.Lines[0].RequiredQuantity);
        Assert.AreEqual(6m,r.Lines[1].RequiredQuantity); Assert.AreEqual(1m,r.Lines[2].RequiredQuantity);
        Assert.AreEqual(7m,r.Totals.Single(t=>t.Activity).Quantity);
        Assert.AreEqual(264m,r.Totals.Single(t=>!t.Activity).Quantity);
        Assert.AreEqual(2m,CompositionCalculator.Preview(c,c.Versions[0].Id,1,u,p).Lines[1].RequiredQuantity);
    }
    [TestMethod] public void T17_YieldIsNotExtraInputAndConvertsBeforeDiscreteRounding()
    {
        var (c,u,p)=Example(); var recipe=c.Versions[0];
        c=c with { Versions=[recipe with { Lines=[recipe.Lines[0] with { LossKind=CompositionLossKind.Yield,LossPercent=80 }] }] };
        Assert.AreEqual(300m,CompositionCalculator.Preview(c,recipe.Id,120,u,p).Lines[0].RequiredQuantity);
        c=c with { Items=[c.Items[0],c.Items[1] with { UnitId=u[0].Id }],Versions=[recipe with { Lines=[recipe.Lines[0] with { InputUnitId=u[0].Id,Quantity=1,LossPercent=10 }] }] };
        Assert.AreEqual(2m,CompositionCalculator.Preview(c,recipe.Id,1,u,p).Lines[0].RequiredQuantity);
    }
    [TestMethod] public void T17_PinnedSemifinishedExpandsWithoutDoubleCountingAndChecksNestedInactive()
    {
        var (c,u,p)=Example(); var root=c.Versions[0]; var semi=new CompositionItem(Id(),"S","Semi",u[0].Id,CompositionItemKind.SemiFinished,true);
        var child=root with { Id=Id(),ProductId=semi.Id,Lines=[root.Lines[0]] };
        var parent=root with { Lines=[new(Id(),null,child.Id,null,1,u[0].Id,1,ConsumptionBasis.PerUnit,CompositionLossKind.None,0)] };
        c=c with { Items=c.Items.Append(semi).ToArray(),Versions=[child,parent] };
        var r=CompositionCalculator.Preview(c,parent.Id,10,u,p);
        Assert.AreEqual(2,r.Lines.Count); Assert.IsTrue(r.Lines[0].Expanded); Assert.AreEqual(22m,r.Lines.Single(l=>!l.Expanded).RequiredQuantity);
        c=c with { Items=c.Items.Select(i=>i.Kind==CompositionItemKind.Material?i with {Active=false}:i).ToArray() };
        Assert.AreEqual("COMPOSITION_INACTIVE",Assert.ThrowsExactly<DomainRuleException>(()=>CompositionCalculator.Validate(c,u,p,true,parent.Id)).Diagnostic.Code);
        Assert.AreEqual(22m,CompositionCalculator.Preview(c,parent.Id,10,u,p).Lines.Last().RequiredQuantity);
    }
    [TestMethod] public void T17_MinutesToHoursRemainContinuousAndInactivePoolPreservesHistory()
    {
        var(c,u,p)=Example();var minute=new UnitDefinition(new(Id()),"minute","time","min",12,false);u=[..u,minute];
        var v=c.Versions[0]; c=c with {Versions=[v with {Lines=[v.Lines[1] with {Quantity=30,InputUnitId=minute.Id,ConversionFactor=1m/60m}]}]};
        var r=CompositionCalculator.Preview(c,v.Id,120,u,p);
        Assert.AreEqual(90m,r.Lines[0].BaseQuantity);Assert.AreEqual(minute.Id,r.Lines[0].InputUnitId);
        Assert.AreEqual(1.5m,r.Lines[0].RequiredQuantity,0.000000000001m);
        var inactive=p.Select(x=>x with {Active=false}).ToArray();
        Assert.ThrowsExactly<DomainRuleException>(()=>CompositionCalculator.Validate(c,u,inactive,true,v.Id));
        Assert.AreEqual(r.Lines[0].RequiredQuantity,CompositionCalculator.Preview(c,v.Id,120,u,inactive).Lines[0].RequiredQuantity);
    }
    [TestMethod] public void T17_NestedMissingTargetAndTinyYieldReturnDomainDiagnostics()
    {
        var(c,u,p)=Example();var v=c.Versions[0];
        var tiny=c with {Versions=[v with {Lines=[v.Lines[0] with {LossKind=CompositionLossKind.Yield,LossPercent=0.0000000000000000000000000001m}]}]};
        Assert.AreEqual("COMPOSITION_OVERFLOW",Assert.ThrowsExactly<DomainRuleException>(()=>CompositionCalculator.Preview(tiny,v.Id,1,u,p)).Diagnostic.Code);
        Assert.AreEqual("COMPOSITION_INVALIDVERSION",Assert.ThrowsExactly<DomainRuleException>(()=>CompositionCalculator.Preview(c,Id(),1,u,p)).Diagnostic.Code);
    }
    [TestMethod] public void T17_InvalidDimensionsCyclesEmptyAndOverflowAreRejected()
    {
        var(c,u,p)=Example(); var v=c.Versions[0];
        Assert.ThrowsExactly<DomainRuleException>(()=>CompositionCalculator.Validate(c with {Versions=[v with { Lines=[v.Lines[0] with {InputUnitId=u[2].Id}]}]},u,p));
        Assert.ThrowsExactly<DomainRuleException>(()=>CompositionCalculator.Validate(c with {Versions=[v with { Lines=[] }]},u,p));
        Assert.ThrowsExactly<DomainRuleException>(()=>CompositionCalculator.Preview(c,v.Id,1.5m,u,p));
        Assert.AreEqual("COMPOSITION_OVERFLOW",Assert.ThrowsExactly<DomainRuleException>(()=>CompositionCalculator.Preview(c,v.Id,decimal.MaxValue,u,p)).Diagnostic.Code);
        var semi=c.Items[0] with {Kind=CompositionItemKind.SemiFinished};
        var self=v with {Lines=[new(Id(),null,v.Id,null,1,u[0].Id,1,ConsumptionBasis.PerUnit,CompositionLossKind.None,0)]};
        Assert.AreEqual("COMPOSITION_CYCLE",Assert.ThrowsExactly<DomainRuleException>(()=>CompositionCalculator.Validate(c with {Items=[semi,c.Items[1]],Versions=[self]},u,p)).Diagnostic.Code);
    }
}
public sealed partial class T14PhysicalActivityTests
{
    [TestMethod] public void T17_PersistencePublicationConflictAndReopen()
    {
        var(file,study,node,pool)=Fixture(); var path=file.Path; CompositionCatalog saved;
        using(file)
        {
            var c=file.Compositions.ReadCompositions();
            var product=new CompositionItem(Id(),"SVC","Service",pool.DriverUnitId,CompositionItemKind.Service,true);
            var version=new CompositionVersion(Id(),product.Id,1,study.SelectedModelVersionId,1,CompositionState.Draft,"",[new(Id(),null,null,pool.Id,2,pool.DriverUnitId,1,ConsumptionBasis.PerOrder,CompositionLossKind.None,0)]);
            saved=file.Compositions.SaveCompositions(c with {Items=[product],Versions=[version]},0,"test");
            Assert.AreEqual(1L,saved.Revision);
            Assert.AreEqual("COMPOSITION_CONFLICT",Assert.ThrowsExactly<DomainRuleException>(()=>file.Compositions.SaveCompositions(saved,0,"test")).Diagnostic.Code);
            saved=file.Compositions.SaveCompositions(saved with {Versions=[version with {State=CompositionState.Published}]},1,"test");
            Assert.AreEqual("COMPOSITION_IMMUTABLE",Assert.ThrowsExactly<DomainRuleException>(()=>file.Compositions.SaveCompositions(saved with {Versions=[saved.Versions[0] with {BatchSize=10}]},2,"test")).Diagnostic.Code);
            Assert.AreEqual(2L,file.Compositions.ReadCompositions().Revision);
            Assert.AreEqual(study.Revision,file.Studies.ReadStudies().Single().Revision);
        }
        using(var reopened=Service().Open(path)) Assert.AreEqual(CanonicalSnapshotJson.Serialize(saved),CanonicalSnapshotJson.Serialize(reopened.Compositions.ReadCompositions()));
        using var readOnly=Service().Open(path,CompanyFileOpenMode.ReadOnly);
        Assert.AreEqual("FILE_READ_ONLY",Assert.ThrowsExactly<CompanyFileException>(()=>readOnly.Compositions.SaveCompositions(saved,2,"test")).Diagnostic.Code);
    }
    [TestMethod] public void T17_CatalogTamperingIsRejectedOnReopen()
    {
        var(file,_,_,pool)=Fixture();var path=file.Path;
        using(file)
        {
            var c=file.Compositions.ReadCompositions();
            file.Compositions.SaveCompositions(c with {Items=[new(Id(),"M","Material",pool.DriverUnitId,CompositionItemKind.Material,true)]},0,"test");
        }
        using(var connection=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+path+";Pooling=False"))
        {
            connection.Open();using var command=connection.CreateCommand();command.CommandText="UPDATE CompositionCatalogs SET Revision=2;";command.ExecuteNonQuery();
        }
        Assert.ThrowsExactly<CompanyFileException>(()=>Service().Open(path));
    }
    [TestMethod] public void T17_CancellationRollsBackCatalogAndAudit()
    {
        var(file,_,_,pool)=Fixture();var path=file.Path;file.Dispose(); using var cancel=new CancellationTokenSource();
        using(var reopened=Service(p=>{if(p==CompanyWriteCheckpoint.BeforeCommit)cancel.Cancel();}).Open(path))
        {
            var c=reopened.Compositions.ReadCompositions();var item=new CompositionItem(Id(),"M","Material",pool.DriverUnitId,CompositionItemKind.Material,true);
            Assert.ThrowsExactly<OperationCanceledException>(()=>reopened.Compositions.SaveCompositions(c with {Items=[item]},0,"test",cancel.Token));
        }
        using var verified=Service().Open(path); Assert.AreEqual(0L,verified.Compositions.ReadCompositions().Revision);
    }
}
