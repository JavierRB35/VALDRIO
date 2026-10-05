using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Valdrio.Infrastructure.CompanyFiles;

// Resolve Windows short/case aliases and reject hard links before selecting the lock path.
internal static class WindowsFileIdentity
{
    public static string Normalize(string path)
    {
        if (!OperatingSystem.IsWindows()) return path;
        if (new DriveInfo(System.IO.Path.GetPathRoot(path)!).DriveType == DriveType.Network)
            throw new Application.CompanyFileException("FILE_PATH_UNSUPPORTED", "File.PathUnsupported");
        var exists = File.Exists(path);
        var name = exists ? path : System.IO.Path.GetDirectoryName(path)!;
        if (!exists && !Directory.Exists(name)) return path;
        using var handle = CreateFile(name, 0, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero);
        if (handle.IsInvalid) throw new IOException("Cannot open physical file path", new Win32Exception());
        if (exists && (!GetFileInformationByHandle(handle, out var information) || information.NumberOfLinks != 1))
            throw new Application.CompanyFileException("FILE_PATH_UNSUPPORTED", "File.PathUnsupported");
        var text = new StringBuilder(32768);
        var length = GetFinalPathNameByHandle(handle, text, (uint)text.Capacity, 0);
        if (length == 0 || length >= text.Capacity) throw new IOException("Cannot resolve physical file path", new Win32Exception());
        var full = text.ToString();
        if (full.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
            throw new Application.CompanyFileException("FILE_PATH_UNSUPPORTED", "File.PathUnsupported");
        if (full.StartsWith(@"\\?\", StringComparison.Ordinal)) full = full[4..];
        return exists ? full : System.IO.Path.Combine(full, System.IO.Path.GetFileName(path));
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME Creation, Access, Write;
        public uint VolumeSerial, SizeHigh, SizeLow, NumberOfLinks, IndexHigh, IndexLow;
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, StringBuilder path, uint length, uint flags);
}
