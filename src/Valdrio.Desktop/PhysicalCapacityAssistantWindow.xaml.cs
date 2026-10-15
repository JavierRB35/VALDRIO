using System.Windows;

namespace Valdrio.Desktop;

public partial class PhysicalCapacityAssistantWindow : Window
{
    private readonly PhysicalCapacityAssistantEditor editor;
    public bool Adopted => editor.Adopted;
    public PhysicalCapacityAssistantWindow(PhysicalActivityEditor host)
    {
        InitializeComponent();
        editor = new PhysicalCapacityAssistantEditor(host);
        DataContext = editor;
    }
    private void Preview(object sender, RoutedEventArgs e) => editor.Preview();
    private void Adopt(object sender, RoutedEventArgs e)
    {
        editor.Adopt();
        if (editor.Adopted) { DialogResult = true; Close(); }
    }
    private void Cancel(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
}
