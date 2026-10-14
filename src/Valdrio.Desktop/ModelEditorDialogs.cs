using System.Windows;
namespace Valdrio.Desktop;

public sealed class ModelEditorDialogs : IModelEditorDialogs
{
    public bool ConfirmEconomicChange(string title, string body, string accept, string cancel) =>
        new EconomicConfirmationWindow(title, body, accept, cancel).ShowDialog() == true;
    public PendingEditChoice ResolvePending(string title, string body) => MessageBox.Show(body,title,MessageBoxButton.YesNoCancel,MessageBoxImage.Question) switch
    { MessageBoxResult.Yes => PendingEditChoice.Save, MessageBoxResult.No => PendingEditChoice.Discard, _ => PendingEditChoice.Cancel };
}
internal sealed class QueuedModelEditorDialogs : IModelEditorDialogs
{
    public Func<string, string, string, string, bool>? EconomicConfirmation { get; set; }
    public bool ConfirmEconomicChange(string title, string body, string accept, string cancel) =>
        EconomicConfirmation?.Invoke(title, body, accept, cancel) ?? false;
    public Queue<PendingEditChoice> Choices { get; } = new();
    public PendingEditChoice ResolvePending(string title, string body) => Choices.Dequeue();
}
