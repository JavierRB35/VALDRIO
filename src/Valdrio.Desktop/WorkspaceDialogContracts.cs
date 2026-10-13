namespace Valdrio.Desktop;

public enum PendingEditChoice { Save, Discard, Cancel }
public interface IModelEditorDialogs
{
    PendingEditChoice ResolvePending(string title, string body);
}
public interface ICompanyFileDialogs
{
    string? SelectNewFile();
    // Optional overload keeps test doubles and older dialog adapters compatible.
    string? SelectNewFile(string suggestedCompanyName) => SelectNewFile();
    string? SelectFile();
    string? SelectBackupDestination();
    string? SelectBackupSource();
    string? SelectRestoreDestination();
}
