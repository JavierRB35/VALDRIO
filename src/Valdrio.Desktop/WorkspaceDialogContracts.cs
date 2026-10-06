namespace Valdrio.Desktop;

public enum PendingEditChoice { Save, Discard, Cancel }
public interface IModelEditorDialogs
{
    PendingEditChoice ResolvePending(string title, string body);
}
public interface ICompanyFileDialogs
{
    string? SelectNewFile();
    string? SelectFile();
    string? SelectBackupDestination();
    string? SelectBackupSource();
    string? SelectRestoreDestination();
}
