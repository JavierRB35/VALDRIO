using Microsoft.Win32;
using System.IO;

namespace Valdrio.Desktop;

public sealed class CompanyFileDialogs : ICompanyFileDialogs
{
    private static string? Save(string extension, string? suggestedName = null)
    {
        var dialog = new SaveFileDialog { DefaultExt = extension, Filter = "VALDRIO|*" + extension,
            AddExtension = true, OverwritePrompt = false, CheckPathExists = true };
        if (suggestedName is not null)
            dialog.FileName = SuggestedFileName(suggestedName);
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
    private static string? Open(string extension)
    {
        var dialog = new OpenFileDialog { Filter = "VALDRIO|*" + extension, CheckFileExists = true };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
    internal static string SuggestedFileName(string companyName)
    {
        // Only suggest a name: the user always chooses the actual path.
        // Windows reserved names and separators must not escape the SaveFileDialog.
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(companyName.Trim().Select(c =>
            char.IsControl(c) || invalid.Contains(c) || c is '/' or '\\' or ':' or '*' or '?' or '\"' or '<' or '>' or '|'
                ? '-' : c).ToArray()).Trim(' ', '.');
        if (clean.Length > 100) clean = clean[..100].Trim(' ', '.');
        if (string.IsNullOrWhiteSpace(clean)) return "Empresa";
        var stem = clean.Split('.')[0];
        if (new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                    "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }
            .Contains(stem, StringComparer.OrdinalIgnoreCase)) clean = "_" + clean;
        return clean;
    }
    public string? SelectNewFile() => Save(".valdrio");
    public string? SelectNewFile(string suggestedCompanyName) => Save(".valdrio", suggestedCompanyName);
    public string? SelectFile() => Open(".valdrio");
    public string? SelectBackupDestination() => Save(".valdrio-backup");
    public string? SelectBackupSource() => Open(".valdrio-backup");
    public string? SelectRestoreDestination() => Save(".valdrio");
}

// CI replaces only native path selection; all buttons, commands and storage remain real.
internal sealed class QueuedCompanyFileDialogs : ICompanyFileDialogs
{
    public Queue<string?> Paths { get; } = new();
    private string? Next() => Paths.Dequeue();
    public string? SelectNewFile() => Next();
    public string? SelectFile() => Next();
    public string? SelectBackupDestination() => Next();
    public string? SelectBackupSource() => Next();
    public string? SelectRestoreDestination() => Next();
}
