using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Application;

public sealed record RevalueStudyItem(
    string ResourceId,
    Money OriginalMoney,
    FxRateId? SelectedRateId);

public sealed record RevalueStudyCommand(
    string StudyId,
    int ExpectedRevision,
    CurrencyCode FunctionalCurrency,
    FxRateSetVersion RateSet,
    IReadOnlyList<RevalueStudyItem> Items);

public sealed record RevaluedStudyItem(
    string ResourceId,
    FxRateApplication RateApplication,
    string FxInputHash);

public sealed record RevalueStudyCandidate(
    string StudyId,
    int ExpectedRevision,
    int NewRevision,
    string RateSetVersionId,
    IReadOnlyList<RevaluedStudyItem> Items);

public sealed record RevalueStudyCommitResult(
    string StudyId,
    int Revision,
    string RateSetVersionId);

public interface IStudyRevaluationValidator
{
    void ValidatePartitionsAndConsumptions(
        RevalueStudyCandidate candidate);
}

public interface IStudyRevaluationRepository
{
    RevalueStudyCommitResult CommitAtomically(
        RevalueStudyCandidate candidate);
}

public sealed class RevalueStudyService
{
    private readonly IStudyRevaluationValidator validator;
    private readonly IStudyRevaluationRepository repository;
    private readonly FxConverter converter = new();

    public RevalueStudyService(
        IStudyRevaluationValidator validator,
        IStudyRevaluationRepository repository)
    {
        this.validator = validator;
        this.repository = repository;
    }

    public RevalueStudyCommitResult RevalueStudy(
        RevalueStudyCommand command)
    {
        if (command.Items.Count == 0)
        {
            throw new ArgumentException(
                "RevalueStudy requires at least one item.",
                nameof(command));
        }

        var items = command.Items
            .Select(item =>
            {
                FxRate? rate = null;

                if (item.OriginalMoney.Currency != command.FunctionalCurrency)
                {
                    if (!item.SelectedRateId.HasValue)
                    {
                        throw new DomainRuleException(
                            new DomainDiagnostic(
                                "FX_RATE_MISSING",
                                "Fx.RateMissing",
                                [
                                    new("resourceId", DiagnosticArgumentType.Identifier, item.ResourceId),
                                    new("sourceCurrency", DiagnosticArgumentType.CurrencyCode, item.OriginalMoney.Currency.Value),
                                    new("targetCurrency", DiagnosticArgumentType.CurrencyCode, command.FunctionalCurrency.Value)
                                ]));
                    }

                    rate = command.RateSet.GetRequired(
                        item.SelectedRateId.Value);
                }

                var application = converter.Convert(
                    $"revalue:{command.StudyId}:{item.ResourceId}:{command.ExpectedRevision + 1}",
                    item.OriginalMoney,
                    command.FunctionalCurrency,
                    rate);

                return new RevaluedStudyItem(
                    item.ResourceId,
                    application,
                    FxConversionHash.Compute(application));
            })
            .ToArray();

        var candidate = new RevalueStudyCandidate(
            command.StudyId,
            command.ExpectedRevision,
            checked(command.ExpectedRevision + 1),
            command.RateSet.VersionId,
            items);

        validator.ValidatePartitionsAndConsumptions(candidate);

        // Atomicity is owned by the repository port: either this candidate is
        // committed as one new revision or no study state changes.
        return repository.CommitAtomically(candidate);
    }
}
