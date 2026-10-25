using System.Windows;
namespace Valdrio.Desktop;

public sealed class ModelEditorDialogs : IModelEditorDialogs
{
    private readonly Func<System.Globalization.CultureInfo> culture;
    public ModelEditorDialogs(Func<System.Globalization.CultureInfo>? culture = null) =>
        this.culture = culture ?? (() => System.Globalization.CultureInfo.CurrentUICulture);
    public bool ConfirmEconomicChange(string title, string body, string accept, string cancel) =>
        new EconomicConfirmationWindow(title, body, accept, cancel).ShowDialog() == true;
    public PendingEditChoice ResolvePending(string title, string body)
    {
        var texts = new Valdrio.Localization.UiTextCatalog();
        string Label(string key) => texts.Get("UX." + key, culture());
        var dialog = new PendingChangesWindow(title, body, Label("SaveContinue"), Label("Discard"), Label("KeepEditing"));
        dialog.ShowDialog();
        return dialog.Choice;
    }
}
internal sealed class QueuedModelEditorDialogs : IModelEditorDialogs
{
    public Func<string, string, string, string, bool>? EconomicConfirmation { get; set; }
    public bool ConfirmEconomicChange(string title, string body, string accept, string cancel) =>
        EconomicConfirmation?.Invoke(title, body, accept, cancel) ?? false;
    public Queue<PendingEditChoice> Choices { get; } = new();
    public PendingEditChoice ResolvePending(string title, string body) => Choices.Dequeue();
}
