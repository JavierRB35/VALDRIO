using Microsoft.VisualStudio.TestTools.UnitTesting;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Infrastructure;
using Valdrio.Infrastructure.CompanyFiles;

namespace Valdrio.Tests;

/// <summary>
/// COR-09: deleting a draft must leave an auditable terminal event;
/// archiving a publication must leave its immutable SQLite row untouched.
/// </summary>
[TestClass]
public sealed class ModelArchiveDraftTests
{
    private string directory = null!;
    private string FilePath => Path.Combine(directory, "archive.valdrio");
    private static EntityId Id() => new(Guid.NewGuid());

    private sealed class Clock : IClock
    {
        public UtcInstant GetUtcNow() => UtcInstant.ParseIso("2026-11-05T10:00:00.0000000Z");
    }

    private static SqliteCompanyFileService Service() => new(
        new JsonCurrencyCatalog(Path.Combine(AppContext.BaseDirectory,
            "reference", "currency-catalog-example.json")), new Clock());

    [TestInitialize]
    public void Initialize()
    {
        directory = Path.Combine(Path.GetTempPath(), "valdrio-archive-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    [TestMethod]
    public void PublishedArchiveAndRestoreKeepStructuralHashAndAuditChain()
    {
        using var file = Service().Create(new(FilePath, "Model archive smoke",
            new("EUR"), PrecisionPolicy.Pdec1Proposed));
        var company = file.Read();
        var unit = new UnitDefinition(new UnitId(Id()), "hour", "time", "h", 12, false);
        file.AddUnits([unit], company.Revision, "test");
        var center = file.Models.CreateCenter(new(company.Id, "CUT", "Cutting", "test"));
        var draft = file.Models.CreateDraft(new(company.Id, "INDUSTRIAL", "test"));
        var node = new CalculationNodeDefinition(Id(), draft.Structure.Id,
            center.Id, "CUT", NodeRole.Principal,
            NodeCostClassification.WithClassification([],
                NodeEconomicFunction.Production, NodeTariffPurpose.General), true);
        var pool = new CostPoolDefinition(Id(), draft.Structure.Id, node.Id,
            unit.Id, "Cutting activity", CostLayer.Unclassified,
            ModelPolicyReference.PendingCapacity);
        var structure = new ModelStructure(draft.Structure.Id, draft.Structure.CompanyId,
            draft.Structure.FamilyId, draft.Structure.VersionNumber,
            draft.Structure.ParentVersionId, draft.Structure.Description,
            draft.Structure.PrecisionPolicyVersion, draft.Structure.MethodPolicy,
            [node], [pool]);
        var edited = file.Models.SaveDraft(new(structure, draft.Revision, "test"));
        var published = file.Models.Publish(new(company.Id, draft.Structure.Id,
            edited.Revision, "test"));
        var hash = published.StructuralHash;
        var canonical = published.PublishedCanonicalJson;
        Assert.IsFalse(published.IsArchived);

        var archived = file.Models.ChangeArchive(new(company.Id,
            published.Structure.Id, published.Revision, true, "test"));
        Assert.IsTrue(archived.IsArchived);
        Assert.AreEqual(published.Revision, archived.Revision);
        Assert.AreEqual(hash, archived.StructuralHash);
        Assert.AreEqual(canonical, archived.PublishedCanonicalJson);
        Assert.IsTrue(file.Models.ReadModels().Single().IsArchived,
            "Fresh read must validate the independent archive audit stream.");

        var restored = file.Models.ChangeArchive(new(company.Id,
            published.Structure.Id, archived.Revision, false, "test"));
        Assert.IsFalse(restored.IsArchived);
        var reopened = file.Models.ReadModels().Single();
        Assert.IsFalse(reopened.IsArchived);
        Assert.AreEqual(canonical, reopened.PublishedCanonicalJson);
    }

    [TestMethod]
    public void DeletingUnreferencedDraftLeavesAValidAuditTerminal()
    {
        using var file = Service().Create(new(FilePath, "Model delete smoke",
            new("EUR"), PrecisionPolicy.Pdec1Proposed));
        var company = file.Read();
        var draft = file.Models.CreateDraft(new(company.Id, "Disposable", "test"));
        file.Models.DeleteDraft(new(company.Id, draft.Structure.Id,
            draft.Revision, "test"));
        Assert.AreEqual(0, file.Models.ReadModels().Count);
        // Every subsequent write/read validates the terminal deletion audit.
        var next = file.Models.CreateDraft(new(company.Id, "Replacement", "test"));
        Assert.AreEqual(next.Structure.Id, file.Models.ReadModels().Single().Structure.Id);
    }
}
