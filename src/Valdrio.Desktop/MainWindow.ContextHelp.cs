using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;

namespace Valdrio.Desktop;

public partial class MainWindow
{
    private ContextHelpWindow? contextHelpWindow;
    private ContextHelpCatalog? contextHelpCatalog;
    private IInputElement? helpPreviousFocus;
    private string? shownHelpView;
    private string? shownHelpLanguage;

    private void InitializeContextHelp()
    {
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.F1 || e.Handled) return;
            e.Handled = true;
            ShowContextHelp();
        };
    }

    private void OpenContextHelp(object sender, RoutedEventArgs e) => ShowContextHelp();

    private void ReadFieldHelp(object sender, RoutedEventArgs e)
    {
        var previous = Keyboard.FocusedElement;
        var language = System.Globalization.CultureInfo.GetCultureInfo(viewModel?.SelectedUiLanguageTag ?? "es");
        var texts = new Valdrio.Localization.UiTextCatalog();
        var window = new EconomicConfirmationWindow(texts.Get("UX.ReadFull", language), FieldHelpText.Text,
            texts.Get("UX.KeepEditing", language), texts.Get("UX.KeepEditing", language));
        window.AcceptButton.Visibility = Visibility.Collapsed;
        window.ShowDialog();
        if (previous is not null) Keyboard.Focus(previous);
    }

    private void ShowContextHelp()
    {
        helpPreviousFocus = Keyboard.FocusedElement;
        if (contextHelpWindow is { IsVisible: true })
        {
            RefreshContextHelp();
            contextHelpWindow.Activate();
            return;
        }
        contextHelpWindow = new ContextHelpWindow { Owner = this };
        contextHelpWindow.Closed += (_, _) =>
        {
            contextHelpWindow = null;
            shownHelpView = shownHelpLanguage = null;
            if (IsVisible && helpPreviousFocus is not null) Keyboard.Focus(helpPreviousFocus);
        };
        RefreshContextHelp(force: true);
        contextHelpWindow?.Show();
    }

    private void RefreshContextHelp(bool force = false)
    {
        if (contextHelpWindow is null) return;
        var page = workspacePages.LastOrDefault()?.GetType().Name;
        var viewId = ContextHelpCatalog.ViewFor(workflowStep, page);
        var language = viewModel?.SelectedUiLanguageTag ?? "es";
        if (!force && viewId == shownHelpView && language == shownHelpLanguage) return;
        var english = language.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        try
        {
            contextHelpCatalog ??= new ContextHelpCatalog(
                System.IO.Path.Combine(AppContext.BaseDirectory, "help"));
            contextHelpWindow.Present(contextHelpCatalog.Load(viewId, language), english);
            shownHelpView = viewId;
            shownHelpLanguage = language;
        }
        catch (Exception error) when (error is System.IO.IOException or
            UnauthorizedAccessException or System.Text.Json.JsonException or
            KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            var text = english ? "Help is not available in this installation."
                               : "La ayuda no está disponible en esta instalación.";
            System.Diagnostics.Trace.TraceError("Context help unavailable: {0}", error);
            contextHelpWindow.Present(new ContextHelpCatalog.Article(viewId,
                "# " + text + "\n\n" + text + "\n", false), english);
            shownHelpView = viewId;
            shownHelpLanguage = language;
        }
    }
}
