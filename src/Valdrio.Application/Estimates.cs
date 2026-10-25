using Valdrio.Calculation;
using Valdrio.Domain;
namespace Valdrio.Application;

public sealed record EstimateRequest(EntityId CompanyId, EntityId StudyId, EntityId OrderId, string OrderCode,
    EntityId RecipeVersionId, EntityId RunId, decimal Quantity, EntityId? PreviousEstimateId = null);
public sealed record EstimateInput(EstimateRequest Request, CompositionCatalog Catalog, IReadOnlyList<UnitDefinition> Units,
    IReadOnlyList<CompositionPool> Pools, PrincipalRateReport Rates, string ReferenceCurrency, PrecisionPolicy Precision);
public sealed record EstimatePreview(EstimateInput Input, EstimatedCost Cost, string InputHash);
public sealed record AcceptedEstimate(EntityId Id, int Version, string FactKind, string Algorithm,
    UtcInstant AcceptedAtUtc, string Actor, EstimatePreview Preview);

public interface IEstimateWorkspace
{
    EstimatePreview PreviewEstimate(EstimateRequest request, CancellationToken token = default);
    AcceptedEstimate AcceptEstimate(EstimateRequest request, string expectedInputHash, string actor, CancellationToken token = default);
    IReadOnlyList<AcceptedEstimate> ReadEstimates(EntityId studyId, CancellationToken token = default);
}

public static class EstimateService
{
    public static EstimatePreview Calculate(EstimateInput input)
    {
        var request = input.Request;
        input.Precision.ValidateQuantity(request.Quantity, "OutputQuantity");
        var recipe = input.Catalog.Versions.SingleOrDefault(x => x.Id == request.RecipeVersionId);
        if (!request.OrderId.IsValid || string.IsNullOrWhiteSpace(request.OrderCode) || request.OrderCode.Length > 120 ||
            recipe?.State != CompositionState.Published || input.Catalog.CompanyId != request.CompanyId ||
            input.Rates.Study.CompanyId != request.CompanyId || input.Rates.Study.Id != request.StudyId ||
            recipe.ModelVersionId != input.Rates.Study.SelectedModelVersionId || input.Rates.EconomicRunId != request.RunId ||
            input.Rates.Algorithm != PrincipalRateCalculator.Version)
            throw EstimateCalculator.Error("InvalidInput");
        var quantities = CompositionCalculator.Preview(input.Catalog, recipe.Id, request.Quantity, input.Units, input.Pools);
        var prices = new List<EstimatePrice>();
        foreach (var total in quantities.Totals)
        {
            if (total.Activity)
            {
                var row = input.Rates.Rows.SingleOrDefault(x => x.PoolId == total.TargetId);
                prices.Add(new(total.TargetId, true, row?.UnitId ?? total.UnitId,
                    row is { Diagnostics.Count: 0 } ? row.Result?.CombinedRate : null, input.Rates.Version));
            }
            else
            {
                if (input.ReferenceCurrency != input.Rates.Study.Currency.FunctionalCurrencyCode.ToString())
                    throw EstimateCalculator.Error("CurrencyMismatch");
                var item = input.Catalog.Items.Single(x => x.Id == total.TargetId);
                prices.Add(new(item.Id, false, item.UnitId, item.ReferenceUnitCost, item.ReferenceCostSource ?? ""));
            }
        }
        var cost = EstimateCalculator.Calculate(quantities, prices, input.Precision);
        return new(input, cost, PublishedModelStructure.Hash(CanonicalSnapshotJson.Serialize(input)));
    }

    public static void Validate(AcceptedEstimate estimate)
    {
        if (!estimate.Id.IsValid || estimate.Version < 1 || estimate.FactKind != "Estimate" ||
            estimate.Algorithm != EstimateCalculator.Version || string.IsNullOrWhiteSpace(estimate.Actor))
            throw EstimateCalculator.Error("InvalidInput");
        var replay = Calculate(estimate.Preview.Input);
        if (CanonicalSnapshotJson.Serialize(replay) != CanonicalSnapshotJson.Serialize(estimate.Preview))
            throw EstimateCalculator.Error("InvalidInput");
    }
}

