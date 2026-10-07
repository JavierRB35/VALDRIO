using System.Text.Json;
using Valdrio.Domain;

namespace Valdrio.Application;

// v1 remains context-only and byte/hash compatible. v2 adds the actual confirmed import ledger.
public sealed class ImportedStudySnapshotInput : IStudySnapshotInput
{
    public const string Schema = "valdrio-study-input-v2";
    public const string GroupedSchema = "valdrio-study-input-v3";
    public const string PreparationVersion = "period-import-t06-v1";
    public ImportedStudySnapshotInput(StudyDefinition studyContext, PrecisionPolicy precisionPolicy,
        PublishedModelStructure model, StudySnapshotProvenance provenance, IReadOnlyList<ImportedBatch> imports)
    {
        _ = new StudySnapshotInput(StudySnapshotInput.Schema, StudySnapshotInput.PreparationVersion,
            studyContext, precisionPolicy, model, provenance, [], [], [], []);
        if (imports is null || imports.Count == 0 || imports.Select(b => b.Id).Distinct().Count() != imports.Count ||
            imports.Select(b => b.ConfirmedStudy.Revision).Distinct().Count() != imports.Count ||
            imports.Any(b => b.ConfirmedStudy.Id != studyContext.Id || b.ConfirmedStudy.CompanyId != studyContext.CompanyId ||
                b.ConfirmedStudy.Revision > studyContext.Revision || b.Prepared.PrecisionPolicy != precisionPolicy ||
                b.Prepared.Profile.PeriodStart != studyContext.PeriodStart || b.Prepared.Profile.PeriodEnd != studyContext.PeriodEnd ||
                b.ConfirmedStudy.SelectedModelVersionId != studyContext.SelectedModelVersionId ||
                b.Resources.Any(r => r.Functional.Currency != studyContext.Currency.FunctionalCurrencyCode)))
            throw StudyRules.Error("STUDY_SNAPSHOT_INVALID", "Study.SnapshotInvalid", studyContext.Id);
        StudyContext = studyContext; PrecisionPolicy = precisionPolicy; Model = model; Provenance = provenance;
        Imports = FrozenList.Copy(imports.OrderBy(b => b.ConfirmedStudy.Revision));
        Resources = FrozenList.Copy(Imports.SelectMany(b => b.Resources).OrderBy(r => r.Id)
            .Select(r => new ResourceSnapshotInput(r.Id, r.Original, r.Functional, null)));
        if (Resources.Count == 0 || Resources.Select(r => r.Id).Distinct().Count() != Resources.Count)
            throw StudyRules.Error("STUDY_SNAPSHOT_INVALID", "Study.SnapshotInvalid", studyContext.Id);
    }
    public string SnapshotSchemaVersion => Imports.Any(b => b.Prepared.Profile.Grouping is not null) ? GroupedSchema : Schema;
    public string InputPreparationVersion => SnapshotSchemaVersion == GroupedSchema ? "grouped-import-t06-v2" : PreparationVersion;
    public StudyDefinition StudyContext { get; }
    public PrecisionPolicy PrecisionPolicy { get; }
    public PublishedModelStructure Model { get; }
    public StudySnapshotProvenance Provenance { get; }
    public IReadOnlyList<ImportedBatch> Imports { get; }
    public IReadOnlyList<ResourceSnapshotInput> Resources { get; }
    public IReadOnlyList<FxRateApplication> AppliedFx => [];
    public IReadOnlyList<ActivitySnapshotInput> Activity => [];
    public IReadOnlyList<ConsumptionSnapshotInput> Consumptions => [];
}

public static class ImportedStudySnapshotJson
{
    public static string Serialize(ImportedStudySnapshotInput input)
    {
        using var doc = JsonDocument.Parse(CanonicalSnapshotJson.Serialize(input));
        return "{\"SchemaVersion\":\"" + input.SnapshotSchemaVersion + "\",\"Value\":" + doc.RootElement.GetProperty("Value").GetRawText() + "}";
    }
    public static ImportedStudySnapshotInput Deserialize(string json)
    {
        using var doc = JsonDocument.Parse(json); var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 2 ||
            root.EnumerateObject().Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != 2 ||
            !root.TryGetProperty("SchemaVersion", out var schema) || schema.GetString() is not (ImportedStudySnapshotInput.Schema or ImportedStudySnapshotInput.GroupedSchema) ||
            !root.TryGetProperty("Value", out var value) || value.ValueKind != JsonValueKind.Object)
            throw new JsonException("Unsupported or incomplete imported study snapshot envelope.");
        var result = CanonicalSnapshotJson.Deserialize<ImportedStudySnapshotInput>("{\"SchemaVersion\":\"" +
            CanonicalSnapshotJson.SchemaVersion + "\",\"Value\":" + value.GetRawText() + "}");
        if (result.SnapshotSchemaVersion != schema.GetString()) throw new JsonException("Imported snapshot schema differs from its ledger.");
        return result;
    }
    private sealed record ResourceEconomy(EntityId Id, string Account, Money Original, Money Functional,
        BusinessDate PeriodStart, BusinessDate PeriodEnd);
    public static string EconomicJson(ImportedStudySnapshotInput input)
    {
        var context = new StudySnapshotInput(StudySnapshotInput.Schema, StudySnapshotInput.PreparationVersion,
            input.StudyContext, input.PrecisionPolicy, input.Model, input.Provenance, [], [], [], []);
        return CanonicalSnapshotJson.Serialize(new
        {
            SchemaVersion = input.SnapshotSchemaVersion == ImportedStudySnapshotInput.GroupedSchema ? "valdrio-study-economy-v3" : "valdrio-study-economy-v2", input.InputPreparationVersion,
            ContextEconomy = StudySnapshotJson.EconomicJson(context),
            Resources = input.Imports.SelectMany(b => b.Resources).OrderBy(r => r.Id).Select(r =>
                new ResourceEconomy(r.Id, r.Account, r.Original, r.Functional, r.PeriodStart, r.PeriodEnd)).ToArray()
        });
    }
}
