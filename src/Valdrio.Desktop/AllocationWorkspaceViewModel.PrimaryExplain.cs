using System.Collections.ObjectModel;
using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Desktop;

// A read-only projection of the primary ledger, using exactly the same
// per-fact currency display amounts as the existing primary results table.
public sealed record PrimaryDestinationDetail(
    string Destination, string NodeType, string Criterion,
    string Driver, string Share, string Amount, decimal NumericAmount);

public sealed record PrimaryAccountResult(
    EntityId ResourceId, string Account, string Repartible,
    string Auxiliary, string Principal, string Assigned, string Pending,
    decimal PresentedBase, decimal PresentedAssigned, decimal PresentedPending,
    decimal AllocationPercent, IReadOnlyList<PrimaryDestinationDetail> Destinations, string AllocationLabel = "");

public sealed partial class AllocationWorkspaceViewModel
{
    public ObservableCollection<PrimaryAccountResult> PrimaryAccounts { get; } = [];
    private PrimaryAccountResult? selectedPrimaryAccount;
    public PrimaryAccountResult? SelectedPrimaryAccount
    {
        get => selectedPrimaryAccount;
        set
        {
            if (SetProperty(ref selectedPrimaryAccount, value))
                PrimaryAccountChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public event EventHandler? PrimaryAccountChanged;
    public string PrimaryKpiEffective { get; private set; } = "";
    public string PrimaryKpiRepartible { get; private set; } = "";
    public string PrimaryKpiAssigned { get; private set; } = "";
    public string PrimaryKpiPending { get; private set; } = "";
    public string PrimaryKpiExcluded { get; private set; } = "";
    public string PrimaryKpiDirectObject { get; private set; } = "";
    public string PrimaryAccountTotal => PrimaryAccounts.Count == 0 ? "" :
        StudyMoney(PrimaryAccounts.Sum(row => row.PresentedAssigned));

    private void ClearPrimaryAccountOverview()
    {
        SelectedPrimaryAccount = null;
        PrimaryAccounts.Clear();
        PrimaryKpiEffective = PrimaryKpiRepartible = PrimaryKpiAssigned =
            PrimaryKpiPending = PrimaryKpiExcluded = PrimaryKpiDirectObject = "";
        PrimaryAccountChanged?.Invoke(this, EventArgs.Empty);
    }

    private void BuildPrimaryAccountOverview(
        PrimaryAllocationResult primary,
        IReadOnlyList<(PrimaryAllocationFact Fact, decimal Amount)> displayed)
    {
        if (workspace is null) return;
        var currency = workspace.Resources.Study.Currency.FunctionalCurrencyCode;
        var nodes = workspace.Resources.Model.Structure.Nodes.ToDictionary(n => n.Id);
        var origins = workspace.Resources.Origins.ToDictionary(o => o.Id);
        var previousId = SelectedPrimaryAccount?.ResourceId;
        SelectedPrimaryAccount = null;
        PrimaryAccounts.Clear();
        var inputAmounts = primary.Parts
            .GroupBy(p => p.ResourceId).ToDictionary(g => g.Key, g => g.Sum(p => p.Input.Amount));
        var byResource = displayed.ToLookup(p => p.Fact.ResourceId);
        foreach (var id in inputAmounts.Keys.Union(displayed.Select(x => x.Fact.ResourceId))
            .OrderBy(id => origins.TryGetValue(id, out var origin) ? origin.Code : id.ToString(),
                StringComparer.Ordinal))
        {
            var inputs = inputAmounts.GetValueOrDefault(id);
            var rows = byResource[id].OrderBy(x => nodes[x.Fact.NodeId].Code, StringComparer.Ordinal)
                .ThenBy(x => x.Fact.PartId).ThenBy(x => x.Fact.Behavior).ToArray();
            var aux = rows.Where(x => nodes[x.Fact.NodeId].Role == NodeRole.Auxiliary).Sum(x => x.Amount);
            var main = rows.Where(x => nodes[x.Fact.NodeId].Role == NodeRole.Principal).Sum(x => x.Amount);
            var assigned = checked(aux + main);
            // Project the unposted ledger gap, then add it to already-projected
            // facts. Independently rounding the source would create fake 0.01
            // discrepancies in a fully covered account.
            var rawPosted = rows.Sum(x => x.Fact.PostedAmount.Amount);
            var rawGap = checked(inputs - rawPosted);
            var pending = AllocationCurrencyDisplay.Project([rawGap],
                DisplayCurrencyQuantum, workspace.Resources.Precision).Single();
            var baseShown = checked(assigned + pending);
            var details = new List<PrimaryDestinationDetail>();
            foreach (var row in rows)
            {
                var fact = row.Fact;
                var node = nodes[fact.NodeId];
                var key = fact.KeyVersionId is { } keyId
                    ? workspace.Keys.FirstOrDefault(k => k.Id == keyId) : null;
                var criterion = fact.Selection switch
                {
                    "absolute" => this["PrimaryAbsolute"],
                    "manual" => this["PrimaryManual"],
                    _ => this["PrimaryRuleSource"]
                };
                if (key is not null) criterion += ": " + key.Name;
                var driver = key is null ? "—" : this[key.Mode.ToString()];
                var share = fact.Selection == "absolute" ? "—" :
                    UiCompactNumbers.Percent(fact.Share * 100m, Format);
                var weight = fact.Selection == "absolute" ? "—" :
                    fact.Weight.ToString("0.####", Format) +
                    (key is { Mode: AllocationKeyMode.DriverUnits } ? " " +
                        workspace.Resources.Model.DriverUnits.FirstOrDefault(u => u.Id == key.UnitId)?.Symbol : "");
                details.Add(new(NodeLabel(node), this[node.Role == NodeRole.Principal ? "PrimaryNodeKindPrincipal" : "PrimaryNodeKindAuxiliary"], criterion,
                    driver + " · " + weight, share, DisplayMoney(row.Amount, currency), row.Amount));
            }
            var ratio = baseShown == 0m ? 0m : assigned / baseShown * 100m;
            PrimaryAccounts.Add(new(id,
                origins.TryGetValue(id, out var origin) ? AccountLabel(origin) : id.ToString(),
                DisplayMoney(baseShown, currency), DisplayMoney(aux, currency),
                DisplayMoney(main, currency), DisplayMoney(assigned, currency),
                DisplayMoney(pending, currency), baseShown, assigned, pending,
                Math.Clamp(ratio, 0m, 100m), details, baseShown == 0m
                    ? this[primary.Parts.Where(p => p.ResourceId == id).All(p => p.Covered) ? "ZeroAssigned" : "ZeroPending"]
                    : ratio.ToString("0.#", Format) + " %"));
        }
        PrimaryKpiEffective = StudyMoney(primary.Resources.Sum(r => r.Effective.Amount));
        PrimaryKpiRepartible = StudyMoney(PrimaryAccounts.Sum(row => row.PresentedBase));
        PrimaryKpiAssigned = StudyMoney(PrimaryAccounts.Sum(row => row.PresentedAssigned));
        PrimaryKpiPending = StudyMoney(primary.Resources.Sum(r => r.Pending.Amount));
        PrimaryKpiExcluded = StudyMoney(primary.Resources.Sum(r => r.Excluded.Amount));
        PrimaryKpiDirectObject = StudyMoney(primary.Resources.Sum(r => r.Direct.Amount));
        SelectedPrimaryAccount = PrimaryAccounts.FirstOrDefault(p => p.ResourceId == previousId)
            ?? PrimaryAccounts.FirstOrDefault();
        PrimaryAccountChanged?.Invoke(this, EventArgs.Empty);
    }
}
