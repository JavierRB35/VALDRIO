using System.Text.Json;
using System.Text.Json.Serialization;
using Valdrio.Domain;
namespace Valdrio.Application;

public sealed class AllocationStudySnapshotInput : IStudySnapshotInput
{
    public const string Schema = "valdrio-study-input-v7";
    public AllocationStudySnapshotInput(StudyDefinition studyContext, PrecisionPolicy precisionPolicy,
        PublishedModelStructure model, StudySnapshotProvenance provenance, IReadOnlyList<ImportedBatch> imports,
        IReadOnlyList<ResourceMutationReceipt> history, IReadOnlyList<AllocationChangeReceipt> allocationHistory,
        bool includeUnroutedAccounts = false)
    {
        _ = new StudySnapshotInput(StudySnapshotInput.Schema, StudySnapshotInput.PreparationVersion,
            studyContext, precisionPolicy, model, provenance, [], [], [], []);
        if (imports.Count > 0) _ = new ImportedStudySnapshotInput(studyContext, precisionPolicy, model, provenance, imports);
        if (allocationHistory.Count == 0) throw StudyRules.Error("STUDY_SNAPSHOT_INVALID", "Study.SnapshotInvalid");
        StudyContext = studyContext; PrecisionPolicy = precisionPolicy; Model = model; Provenance = provenance;
        IncludeUnroutedAccounts = includeUnroutedAccounts;
        Imports = FrozenList.Copy(imports.OrderBy(b => b.ConfirmedStudy.Revision));
        History = FrozenList.Copy(history.OrderBy(h => h.ConfirmedStudy.Revision));
        var ledger = ResourceHistory.Replay(studyContext, precisionPolicy, model, Imports, History);
        Workspace = new(studyContext, precisionPolicy, model, ResourceOrigins.FromImports(studyContext, Imports), ledger, ResourceHistory.EconomicRevision(studyContext, Imports, History));
        AllocationHistory = FrozenList.Copy(allocationHistory.OrderBy(h => h.ConfirmedStudy.Revision));
        long revision = -1; var requests = new HashSet<EntityId>();
        foreach (var receipt in AllocationHistory)
        {
            var before = receipt.PreviousStudy; var after = receipt.ConfirmedStudy;
            if (before.Id != studyContext.Id || before.CompanyId != studyContext.CompanyId ||
                before.Revision < revision || after.Revision > studyContext.Revision || !requests.Add(receipt.Request.RequestId) ||
                !ImportConfirmationRules.IsImportRevision(before, after))
                throw AllocationDiagnostics.Error("KEY_HISTORY_INVALID", "Allocation.HistoryInvalid");
            var historicalImports = Imports.Where(b => b.ConfirmedStudy.Revision <= before.Revision).ToArray();
            var previousLedger = History.LastOrDefault(h => h.ConfirmedStudy.Revision <= before.Revision)?.Ledger ?? ResourceLedger.Empty;
            var resources = new ResourceWorkspaceSnapshot(before, precisionPolicy, model, ResourceOrigins.FromImports(before, historicalImports), previousLedger, ResourceHistory.EconomicRevision(before, Imports, History));
            var referenced = AllocationWorkspaceRules.ValidateConfiguration(resources, receipt.ReferencedKeys, receipt.Request);
            if (CanonicalSnapshotJson.Serialize(referenced) != CanonicalSnapshotJson.Serialize(receipt.ReferencedKeys))
                throw AllocationDiagnostics.Error("KEY_HISTORY_INVALID", "Allocation.HistoryInvalid");
            revision = after.Revision;
        }
        var last = AllocationHistory.Last();
        Allocations = new(Workspace, last.ReferencedKeys, last.Request.Configuration, last.ConfirmedStudy.Revision,
            includeUnroutedAccounts, secondaryPublication: last.SecondaryPublication);
    }
    public string SnapshotSchemaVersion => Schema;
    public string InputPreparationVersion => IncludeUnroutedAccounts ? "allocation-preparation-t09-v2" : "allocation-preparation-t08-v1";
    // Omitted in historical v7 snapshots: retain their preparation policy and hashes.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IncludeUnroutedAccounts { get; }
    public StudyDefinition StudyContext { get; }
    public PrecisionPolicy PrecisionPolicy { get; }
    public PublishedModelStructure Model { get; }
    public StudySnapshotProvenance Provenance { get; }
    public IReadOnlyList<ImportedBatch> Imports { get; }
    public IReadOnlyList<ResourceMutationReceipt> History { get; }
    public IReadOnlyList<AllocationChangeReceipt> AllocationHistory { get; }
    [JsonIgnore] public ResourceWorkspaceSnapshot Workspace { get; }
    [JsonIgnore] public AllocationWorkspaceSnapshot Allocations { get; }
    // Materialize the actual version chosen for every covered source, independent of future publications.
    public IReadOnlyList<AppliedAllocationKey> AppliedKeys => FrozenList.Copy(Allocations.Coverage.Where(c => c.KeyId.HasValue)
        .Select(c => new AppliedAllocationKey(c.SourceId, c.ComponentGroup, Allocations.Keys.Single(k => k.Id == c.KeyId))));
    // Legacy interface values remain typed originals, one per source/delta; never add unlike currencies.
    [JsonIgnore] public IReadOnlyList<ResourceSnapshotInput> Resources => FrozenList.Copy(Workspace.Resources.SelectMany(r =>
        r.Sources.Select(s => new ResourceSnapshotInput(s.SourceRowId, s.Conversion.OriginalMoney, s.Conversion.ConvertedOriginalMoney, s.Conversion))
        .Concat(r.Adjustments.Select(a => new ResourceSnapshotInput(a.Id, a.Conversion.OriginalMoney, a.Conversion.ConvertedOriginalMoney, a.Conversion)))));
    [JsonIgnore] public IReadOnlyList<FxRateApplication> AppliedFx => FrozenList.Copy(Resources.Select(r => r.Conversion!));
    [JsonIgnore] public IReadOnlyList<ActivitySnapshotInput> Activity => [];
    [JsonIgnore] public IReadOnlyList<ConsumptionSnapshotInput> Consumptions => [];
}

public static class AllocationStudySnapshotJson
{
    public static string Serialize(AllocationStudySnapshotInput input)
    {
        using var doc = JsonDocument.Parse(CanonicalSnapshotJson.Serialize(input));
        return "{\"SchemaVersion\":\"" + AllocationStudySnapshotInput.Schema + "\",\"Value\":" + doc.RootElement.GetProperty("Value").GetRawText() + "}";
    }
    public static AllocationStudySnapshotInput Deserialize(string json)
    {
        using var doc = JsonDocument.Parse(json); var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 2 ||
            root.EnumerateObject().Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != 2 ||
            !root.TryGetProperty("SchemaVersion", out var schema) || schema.GetString() != AllocationStudySnapshotInput.Schema ||
            !root.TryGetProperty("Value", out var value) || value.ValueKind != JsonValueKind.Object)
            throw new JsonException("Unsupported resource snapshot envelope.");
        return CanonicalSnapshotJson.Deserialize<AllocationStudySnapshotInput>("{\"SchemaVersion\":\"" + CanonicalSnapshotJson.SchemaVersion +
            "\",\"Value\":" + value.GetRawText() + "}");
    }
    public static string EconomicJson(AllocationStudySnapshotInput input)
    {
        var context = new StudySnapshotInput(StudySnapshotInput.Schema, StudySnapshotInput.PreparationVersion,
            input.StudyContext, input.PrecisionPolicy, input.Model, input.Provenance, [], [], [], []);
        return CanonicalSnapshotJson.Serialize(new
        {
            SchemaVersion = "valdrio-study-economy-v7", input.InputPreparationVersion,
            ContextEconomy = StudySnapshotJson.EconomicJson(context),
            Keys = input.AppliedKeys.Select(a => new { a.SourceId, a.ComponentGroup,
                a.Key.Id, a.Key.VersionNumber, a.Key.Stage, a.Key.Mode, a.Key.UnitId, a.Key.SelfAllocation, a.Key.Lines }).ToArray(),
            input.Allocations.Configuration.PrimaryRules, input.Allocations.Configuration.SecondaryRules,
            input.Allocations.Configuration.ExplicitAssignments,
            Absolute = input.Allocations.Configuration.AbsoluteAssignments.Select(a => new { a.PartId, a.SourceAmount, a.Lines }).ToArray(),
            Coverage = input.Allocations.Coverage.Select(c => new { c.SourceId, c.ComponentGroup, c.KeyId, Code = c.Diagnostic?.Code }).ToArray(),
            Objects = input.Workspace.Ledger.Objects.Select(o => new { o.Id, o.Code, o.Kind }).ToArray(),
            Resources = input.Workspace.Resources.OrderBy(r => r.Id).Select(r => new
            {
                r.Id, Code = input.Workspace.Origins.Single(o => o.Id == r.Id).Code, r.Sources,
                Adjustments = r.Adjustments.Select(a => new { a.Id, a.Conversion, a.Type, a.ReversalOfId }).ToArray(),
                Parts = r.Parts.Select(p => new { p.Id, p.Amount, p.Routing, p.DirectAssignments, p.PoolId, p.Classification }).ToArray()
            }).ToArray()
        });
    }
}
