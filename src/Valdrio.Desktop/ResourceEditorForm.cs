using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Valdrio.Domain;
using Valdrio.Localization;
namespace Valdrio.Desktop;

// Text is interpreted with the culture captured when editing begins, never the latest preference.
public sealed partial class ResourceEditorForm : ObservableObject
{
    private readonly Dictionary<string, string> values = new(StringComparer.Ordinal);
    public event Action? Edited;
    public string this[string key]
    {
        get => values.GetValueOrDefault(key, "");
        set { if (this[key] == value) return; values[key] = value; OnPropertyChanged("Item[]"); }
    }
    public DecimalEditSession Numbers { get; set; } = new(CultureInfo.GetCultureInfo("es-ES"));
    [ObservableProperty] private ResourceRoutingKind route;
    [ObservableProperty] private ResourceBehavior behavior;
    [ObservableProperty] private ResourceCostLayer layer;
    [ObservableProperty] private ResourceCapacityTreatment capacity;
    [ObservableProperty] private EntityId? poolId;
    [ObservableProperty] private EntityId? objectId;
    [ObservableProperty] private UnitId? unitId;
    [ObservableProperty] private DateTime? start;
    [ObservableProperty] private DateTime? end;
    [ObservableProperty] private DateTime? rateDate;
    protected override void OnPropertyChanged(PropertyChangedEventArgs e) { base.OnPropertyChanged(e); Edited?.Invoke(); }
}
public sealed record ResourceChoice<T>(T Value, string Label);

// WPF selectors must retain their ItemsSource while a form is being edited.
// Replacing equivalent lists can clear SelectedValue and create a false edit.
internal sealed class WorkspaceChoices
{
    private readonly Dictionary<string, (object Context, object Items)> entries = [];
    public IReadOnlyList<T> Get<T>(object context, Func<IReadOnlyList<T>> build,
        [System.Runtime.CompilerServices.CallerMemberName] string name = "")
    {
        if (entries.TryGetValue(name, out var entry) && Equals(entry.Context, context))
            return (IReadOnlyList<T>)entry.Items;
        var items = build();
        if (entry.Items is IReadOnlyList<T> previous && previous.SequenceEqual(items)) items = previous;
        entries[name] = (context, items);
        return items;
    }
}
public sealed record ResourceGridRow(EntityId Id, string Code, string Originals, string Converted, string Adjustments,
    string Effective, string Pending, string Difference)
{
    public string DisplayName { get; init; } = Code;
    public long Revision { get; init; }
    public string CorrectionNumber { get; init; } = "";
    public string FormatCulture { get; init; } = "es-ES";
    public string AssignmentStatus { get; init; } = "";
}
public sealed record ResourcePartRow(ResourcePartDraft Value, string Amount, string Route, string Target, string Behavior, string Layer);
public sealed record ResourceAdjustmentRow(ResourceAdjustment Value, string Original, string Functional, string Type, string Reason, string State);
public sealed class ResourceRevalueRow(EntityId resourceId, EntityId valueId, string account, FxRateApplication previous) : ObservableObject
{
    public EntityId ResourceId { get; } = resourceId;
    public EntityId ValueId { get; } = valueId;
    public string Account { get; } = account;
    public FxRateApplication Previous { get; } = previous;
    public ResourceEditorForm Form { get; } = new();
}
