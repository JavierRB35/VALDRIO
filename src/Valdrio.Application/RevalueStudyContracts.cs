using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Application;

public sealed record RevalueStudyItem(
    string ResourceId,
    Money OriginalMoney,
    FxRateId? SelectedRateId);

public sealed record RevalueStudyCommand(
    string StudyId,
    long ExpectedRevision,
    CurrencyCode FunctionalCurrency,
    FxRateSetVersion RateSet,
    IReadOnlyList<RevalueStudyItem> Items);

public sealed record RevaluedStudyItem(
    string ResourceId,
    FxRateApplication RateApplication,
    string FxInputHash);

public sealed record RevalueStudyCandidate
{
    public RevalueStudyCandidate(string StudyId, long ExpectedRevision, long NewRevision,
        string RateSetVersionId, IReadOnlyList<RevaluedStudyItem> Items)
    {
        this.StudyId = StudyId;
        this.ExpectedRevision = ExpectedRevision;
        this.NewRevision = NewRevision;
        this.RateSetVersionId = RateSetVersionId;
        this.Items = FrozenList.Copy(Items);
    }
    public string StudyId { get; }
    public long ExpectedRevision { get; }
    public long NewRevision { get; }
    public string RateSetVersionId { get; }
    public IReadOnlyList<RevaluedStudyItem> Items { get; }
}

public sealed record RevalueStudyCommitResult(
    string StudyId,
    long Revision,
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
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.StudyId);
        ArgumentNullException.ThrowIfNull(command.Items);
        ArgumentNullException.ThrowIfNull(command.RateSet);
        if (command.ExpectedRevision < 0) throw new ArgumentOutOfRangeException(nameof(command));
        if (!command.FunctionalCurrency.IsValid) throw new ArgumentException("FX_CURRENCY_INVALID");
        var newRevision = checked(command.ExpectedRevision + 1);
        var inputItems = command.Items.ToArray();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in inputItems)
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentException.ThrowIfNullOrWhiteSpace(item.ResourceId);
            item.OriginalMoney.EnsureValid(item.ResourceId);
            if (!ids.Add(item.ResourceId)) throw new ArgumentException("RESOURCE_ID_DUPLICATE");
        }
        if (inputItems.Length == 0)
        {
            throw new ArgumentException(
                "RevalueStudy requires at least one item.",
                nameof(command));
        }

        var items = inputItems
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
                    $"revalue:{command.StudyId}:{item.ResourceId}:{newRevision}",
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
            newRevision,
            command.RateSet.VersionId,
            items);

        validator.ValidatePartitionsAndConsumptions(candidate);

        // Atomicity is owned by the repository port: either this candidate is
        // committed as one new revision or no study state changes.
        return repository.CommitAtomically(candidate);
    }
}
