using System.Windows.Threading;

namespace Valdrio.Desktop;

public partial class ImportColumnMappingWindow
{
    // Regression cases deliberately prepare invalid payloads to verify domain diagnostics.
    // The user-facing wizard checks its required fields before reaching this command.
    private async Task PrepareForSmoke()
    {
        var vm = (ImportColumnMappingViewModel)DataContext;
        if (!vm.PrepareCommand.CanExecute(null)) throw new InvalidOperationException("Preparation unavailable.");
        await vm.PrepareCommand.ExecuteAsync(null);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        UpdateLayout();
    }
}
