using System.Security.Cryptography;
using System.Text;
using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Application;

public static class ResourceOrigins
{
    public static EntityId StableId(string scope, string value) =>
        new(new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalSnapshotJson.Serialize(new { scope, value }))).AsSpan(0, 16)));

    // Accounts identify resources. Descriptions never split or classify a resource.
    public static IReadOnlyList<ResourceOrigin> FromImports(StudyDefinition study, IReadOnlyList<ImportedBatch> imports)
    {
        var values = new List<(string Code, ResourceOriginRow Row)>();
        foreach (var batch in ImportConfirmationRules.ActiveBatches(imports))
        {
            if (batch.ConfirmedStudy.Id != study.Id || batch.ConfirmedStudy.CompanyId != study.CompanyId)
                throw ResourceWorkspaceRules.Error("RES_SCOPE_INVALID", "Resource.ScopeInvalid");
            Add(batch, batch.Prepared, false);
            if (batch.Prepared.Profile.Comparison is { } comparison)
            {
                var context = new StudyDefinition(study.Id, study.CompanyId, study.Name,
                    comparison.Profile.PeriodStart, comparison.Profile.PeriodEnd, study.Purpose, study.ScenarioKind,
                    null, study.SelectedModelVersionId, study.Currency, study.CurrencyValidity, study.Revision,
                    StudyState.Open, null, null, study.CreatedAtUtc);
                var earlier = PreparedImport.Create(batch.Id, context, batch.Prepared.PrecisionPolicy,
                    comparison.Source, comparison.Profile);
                if (!earlier.CanConfirm) throw ResourceWorkspaceRules.Error("RES_SOURCE_CHANGED", "Resource.SourceChanged");
                Add(batch, earlier, true);
            }
        }
        return FrozenList.Copy(values.GroupBy(v => v.Code, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new ResourceOrigin(StableId("resource:" + study.Id, g.Key), g.Key,
                FrozenList.Copy(g.Select(v => v.Row).OrderBy(r => r.Id)))));

        void Add(ImportedBatch batch, PreparedImport prepared, bool earlier)
        {
            var targets = prepared.Profile.Grouping?.Records.ToDictionary(r => r.RecordNumber, r => r.Code);
            foreach (var row in prepared.Rows.Where(r => r.Selected))
            {
                var id = StableId("source:" + batch.Id + (earlier ? ":earlier" : ":current"),
                    row.RecordNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));
                var original = row.Original!.Value;
                var conversion = prepared.ConversionBridge?.Rows.Single(r => r.RecordNumber == row.RecordNumber).Application;
                if (earlier) original = new(-original.Amount, original.Currency, original.PrecisionPolicy);
                var applied = new FxConverter().Convert(id.ToString(), original,
                    study.Currency.FunctionalCurrencyCode, conversion is null ? null : ResourceWorkspaceRules.Rate(conversion));
                values.Add((targets?.GetValueOrDefault(row.RecordNumber) ?? row.Account!,
                    new(id, batch.Id, row.RecordNumber, earlier, prepared.Source.FileName, applied,
                        prepared.Profile.Columns.Description is { } column
                            ? prepared.Source.Rows.Single(r => r.RecordNumber == row.RecordNumber).GetCell(column).TextValue : null,
                        prepared.Profile.Columns.Description, prepared.Source.FileHash)));
            }
        }
    }
}
