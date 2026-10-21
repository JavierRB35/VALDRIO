using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Valdrio.Desktop;

/// <summary>Non-modal, read-only help surface; no data bindings or economic commands.</summary>
internal sealed class ContextHelpWindow : Window
{
    private readonly TextBlock heading;
    private readonly TextBlock languageNotice;
    private readonly FlowDocumentScrollViewer reader;
    private readonly Button closeButton;

    public ContextHelpWindow()
    {
        Width = 920;
        Height = 730;
        MinWidth = 560;
        MinHeight = 440;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush("Surface.Canvas", Brushes.White);

        var root = new DockPanel { LastChildFill = true, Margin = new Thickness(18) };
        var header = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 12) };
        closeButton = new Button { MinWidth = 92, Padding = new Thickness(12, 7, 12, 7) };
        closeButton.Click += (_, _) => Close();
        DockPanel.SetDock(closeButton, Dock.Right);
        header.Children.Add(closeButton);

        var titles = new StackPanel { Orientation = Orientation.Vertical };
        heading = new TextBlock
        {
            FontSize = 22, FontWeight = FontWeights.SemiBold,
            Foreground = Brush("Brand.DeepGreen", Brushes.DarkGreen),
            TextWrapping = TextWrapping.Wrap
        };
        languageNotice = new TextBlock
        {
            FontSize = 12, Foreground = Brush("Text.Secondary", Brushes.DimGray),
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 0)
        };
        titles.Children.Add(heading);
        titles.Children.Add(languageNotice);
        header.Children.Add(titles);
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        reader = new FlowDocumentScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            IsToolBarVisible = false,
            IsSelectionEnabled = true,
            Background = Brush("Brand.White", Brushes.White),
            BorderBrush = Brush("Border.Soft", Brushes.LightGray),
            BorderThickness = new Thickness(1)
        };
        AutomationProperties.SetName(reader, "Contenido de la ayuda contextual / Contextual help content");
        root.Children.Add(reader);
        Content = root;
        PreviewKeyDown += (_, args) =>
        {
            if (args.Key != Key.Escape) return;
            args.Handled = true;
            Close();
        };
    }

    public void Present(ContextHelpCatalog.Article article, bool english)
    {
        Title = english ? "VALDRIO · Contextual help" : "VALDRIO · Ayuda contextual";
        closeButton.Content = english ? "Close" : "Cerrar";
        var titleLine = article.Markdown.Split('\n').FirstOrDefault()?.Trim() ?? "";
        heading.Text = titleLine.TrimStart('#', ' ');
        languageNotice.Text = article.SpanishFallback
            ? "An English translation is not yet available for this screen. The Spanish text is shown."
            : english ? "Help for the current screen · F1" : "Ayuda correspondiente a la pantalla actual · F1";
        reader.Document = ContextHelpMarkdown.Render(article.Markdown);
    }

    private static Brush Brush(string key, Brush fallback) =>
        Application.Current?.TryFindResource(key) as Brush ?? fallback;
}

/// <summary>Restricted Markdown renderer: headings, text, emphasis, lists and tables; no HTML or external execution.</summary>
internal static class ContextHelpMarkdown
{
    private static readonly Regex InlineTokens = new(
        @"(\*\*[^*]+\*\*|`[^`]+`|\[[^\]]+\]\([^)]+\))", RegexOptions.Compiled);

    internal static FlowDocument Render(string markdown)
    {
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(22),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
            ColumnWidth = 1100,
            Foreground = Resource("Text.Primary", Brushes.Black),
            Background = Resource("Brand.White", Brushes.White)
        };
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("### Referencias al código", StringComparison.OrdinalIgnoreCase))
                break; // Implementation paths are not business-user help.
            if (line.Length == 0 || line.StartsWith("**ID Vista UI:**", StringComparison.Ordinal) ||
                line.StartsWith("**Última actualización:**", StringComparison.Ordinal) ||
                line.StartsWith("**Estado:**", StringComparison.Ordinal))
                continue;
            if (line.StartsWith('|'))
            {
                var rows = new List<string[]>();
                while (i < lines.Length && lines[i].TrimStart().StartsWith('|'))
                {
                    var cells = lines[i].Trim().Trim('|').Split('|').Select(c => c.Trim()).ToArray();
                    if (!cells.All(c => Regex.IsMatch(c, @"^:?-{3,}:?$")))
                        rows.Add(cells);
                    i++;
                }
                i--;
                if (rows.Count > 0) doc.Blocks.Add(MakeTable(rows));
                continue;
            }
            var hashes = line.TakeWhile(c => c == '#').Count();
            if (hashes is >= 1 and <= 4 && line.Length > hashes && line[hashes] == ' ')
            {
                var paragraph = new Paragraph { FontWeight = FontWeights.SemiBold,
                    FontSize = hashes == 1 ? 22 : hashes == 2 ? 17 : 14,
                    Foreground = Resource("Brand.DeepGreen", Brushes.DarkGreen),
                    Margin = new Thickness(0, hashes == 1 ? 2 : 16, 0, 9) };
                AddInlines(paragraph.Inlines, line[(hashes + 1)..]);
                doc.Blocks.Add(paragraph);
                continue;
            }
            var bullet = line.StartsWith("- ", StringComparison.Ordinal) ||
                         Regex.IsMatch(line, @"^\d+\.\s");
            var paragraphText = bullet ? (line.StartsWith("- ", StringComparison.Ordinal)
                ? "• " + line[2..] : line) : line.TrimStart('>', ' ');
            var body = new Paragraph { Margin = bullet ? new Thickness(14, 3, 0, 4)
                                                      : new Thickness(0, 5, 0, 8),
                                       LineHeight = 20 };
            AddInlines(body.Inlines, paragraphText);
            doc.Blocks.Add(body);
        }
        return doc;
    }

    private static Table MakeTable(List<string[]> rows)
    {
        var count = rows.Max(r => r.Length);
        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 6, 0, 14),
            BorderBrush = Resource("Border.Soft", Brushes.LightGray),
            BorderThickness = new Thickness(1) };
        for (var c = 0; c < count; c++) table.Columns.Add(new TableColumn
            { Width = new GridLength(1, GridUnitType.Star) });
        var group = new TableRowGroup();
        for (var index = 0; index < rows.Count; index++)
        {
            var row = new TableRow();
            if (index == 0) row.Background = Resource("Surface.Subtle", Brushes.Gainsboro);
            for (var c = 0; c < count; c++)
            {
                var paragraph = new Paragraph { Margin = new Thickness(0) };
                AddInlines(paragraph.Inlines, c < rows[index].Length ? rows[index][c] : "");
                if (index == 0) paragraph.FontWeight = FontWeights.SemiBold;
                row.Cells.Add(new TableCell(paragraph) {
                    Padding = new Thickness(7),
                    BorderBrush = Resource("Border.Soft", Brushes.LightGray),
                    BorderThickness = new Thickness(0, 0, 1, 1)
                });
            }
            group.Rows.Add(row);
        }
        table.RowGroups.Add(group);
        return table;
    }

    private static void AddInlines(InlineCollection collection, string text)
    {
        var position = 0;
        foreach (Match token in InlineTokens.Matches(text))
        {
            if (token.Index > position)
                collection.Add(new Run(text[position..token.Index]));
            var value = token.Value;
            if (value.StartsWith("**", StringComparison.Ordinal))
                collection.Add(new Bold(new Run(value[2..^2])));
            else if (value.StartsWith('`'))
                collection.Add(new Run(value[1..^1]) { FontFamily = new FontFamily("Consolas") });
            else
            {
                var end = value.IndexOf(']');
                collection.Add(new Run(value[1..end])); // No links leave the sandbox.
            }
            position = token.Index + token.Length;
        }
        if (position < text.Length) collection.Add(new Run(text[position..]));
    }

    private static Brush Resource(string key, Brush fallback) =>
        Application.Current?.TryFindResource(key) as Brush ?? fallback;
}
