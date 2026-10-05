using Microsoft.Win32;

namespace Valdrio.Desktop;

public interface ICompanyFileDialogs
{
    string? SelectNewFile();
    string? SelectFile();
    string? SelectBackupDestination();
    string? SelectBackupSource();
    string? SelectRestoreDestination();
}

public sealed class CompanyFileDialogs : ICompanyFileDialogs
{
    private static string? Save(string extension)
    {
        var dialog = new SaveFileDialog { DefaultExt = extension, Filter = "VALDRIO|*" + extension,
            AddExtension = true, OverwritePrompt = false, CheckPathExists = true };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
    private static string? Open(string extension)
    {
        var dialog = new OpenFileDialog { Filter = "VALDRIO|*" + extension, CheckFileExists = true };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
    public string? SelectNewFile() => Save(".valdrio");
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
