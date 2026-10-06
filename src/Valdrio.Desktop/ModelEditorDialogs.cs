using System.Windows;
namespace Valdrio.Desktop;

public sealed class ModelEditorDialogs : IModelEditorDialogs
{
    public PendingEditChoice ResolvePending(string title, string body) => MessageBox.Show(body,title,MessageBoxButton.YesNoCancel,MessageBoxImage.Question) switch
    { MessageBoxResult.Yes => PendingEditChoice.Save, MessageBoxResult.No => PendingEditChoice.Discard, _ => PendingEditChoice.Cancel };
}
internal sealed class QueuedModelEditorDialogs : IModelEditorDialogs
{
    public Queue<PendingEditChoice> Choices { get; } = new();
    public PendingEditChoice ResolvePending(string title, string body) => Choices.Dequeue();
}
