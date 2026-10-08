using System.Text.Json;
using System.Text.Json.Serialization;
using Valdrio.Domain;
namespace Valdrio.Application;

public sealed class ResourceStudySnapshotInput : IStudySnapshotInput
{
    public const string Schema = "valdrio-study-input-v6";
    public ResourceStudySnapshotInput(StudyDefinition studyContext, PrecisionPolicy precisionPolicy,
        PublishedModelStructure model, StudySnapshotProvenance provenance, IReadOnlyList<ImportedBatch> imports,
        IReadOnlyList<ResourceMutationReceipt> history)
    {
        _ = new StudySnapshotInput(StudySnapshotInput.Schema, StudySnapshotInput.PreparationVersion,
            studyContext, precisionPolicy, model, provenance, [], [], [], []);
        if (imports.Count > 0) _ = new ImportedStudySnapshotInput(studyContext, precisionPolicy, model, provenance, imports);
        if (history.Count == 0) throw StudyRules.Error("STUDY_SNAPSHOT_INVALID", "Study.SnapshotInvalid");
        StudyContext = studyContext; PrecisionPolicy = precisionPolicy; Model = model; Provenance = provenance;
        Imports = FrozenList.Copy(imports.OrderBy(b => b.ConfirmedStudy.Revision));
        History = FrozenList.Copy(history.OrderBy(h => h.ConfirmedStudy.Revision));
        var ledger = ResourceHistory.Replay(studyContext, precisionPolicy, model, Imports, History);
        Workspace = new(studyContext, precisionPolicy, model, ResourceOrigins.FromImports(studyContext, Imports), ledger);
    }
    public string SnapshotSchemaVersion => Schema;
    public string InputPreparationVersion => "resource-preparation-t07-v1";
    public StudyDefinition StudyContext { get; }
    public PrecisionPolicy PrecisionPolicy { get; }
    public PublishedModelStructure Model { get; }
    public StudySnapshotProvenance Provenance { get; }
    public IReadOnlyList<ImportedBatch> Imports { get; }
    public IReadOnlyList<ResourceMutationReceipt> History { get; }
    [JsonIgnore] public ResourceWorkspaceSnapshot Workspace { get; }
    // Legacy interface values remain typed originals, one per source/delta; never add unlike currencies.
    [JsonIgnore] public IReadOnlyList<ResourceSnapshotInput> Resources => FrozenList.Copy(Workspace.Resources.SelectMany(r =>
        r.Sources.Select(s => new ResourceSnapshotInput(s.SourceRowId, s.Conversion.OriginalMoney, s.Conversion.ConvertedOriginalMoney, s.Conversion))
        .Concat(r.Adjustments.Select(a => new ResourceSnapshotInput(a.Id, a.Conversion.OriginalMoney, a.Conversion.ConvertedOriginalMoney, a.Conversion)))));
    [JsonIgnore] public IReadOnlyList<FxRateApplication> AppliedFx => FrozenList.Copy(Resources.Select(r => r.Conversion!));
    [JsonIgnore] public IReadOnlyList<ActivitySnapshotInput> Activity => [];
    [JsonIgnore] public IReadOnlyList<ConsumptionSnapshotInput> Consumptions => [];
}

public static class ResourceStudySnapshotJson
{
    public static string Serialize(ResourceStudySnapshotInput input)
    {
        using var doc = JsonDocument.Parse(CanonicalSnapshotJson.Serialize(input));
        return "{\"SchemaVersion\":\"" + ResourceStudySnapshotInput.Schema + "\",\"Value\":" + doc.RootElement.GetProperty("Value").GetRawText() + "}";
    }
    public static ResourceStudySnapshotInput Deserialize(string json)
    {
        using var doc = JsonDocument.Parse(json); var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 2 ||
            root.EnumerateObject().Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != 2 ||
            !root.TryGetProperty("SchemaVersion", out var schema) || schema.GetString() != ResourceStudySnapshotInput.Schema ||
            !root.TryGetProperty("Value", out var value) || value.ValueKind != JsonValueKind.Object)
            throw new JsonException("Unsupported resource snapshot envelope.");
        return CanonicalSnapshotJson.Deserialize<ResourceStudySnapshotInput>("{\"SchemaVersion\":\"" + CanonicalSnapshotJson.SchemaVersion +
            "\",\"Value\":" + value.GetRawText() + "}");
    }
    public static string EconomicJson(ResourceStudySnapshotInput input)
    {
        var context = new StudySnapshotInput(StudySnapshotInput.Schema, StudySnapshotInput.PreparationVersion,
            input.StudyContext, input.PrecisionPolicy, input.Model, input.Provenance, [], [], [], []);
        return CanonicalSnapshotJson.Serialize(new
        {
            SchemaVersion = "valdrio-study-economy-v6", input.InputPreparationVersion,
            ContextEconomy = StudySnapshotJson.EconomicJson(context),
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
