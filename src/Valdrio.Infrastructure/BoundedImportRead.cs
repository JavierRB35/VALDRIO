using System.IO.Compression;
using Valdrio.Application;

namespace Valdrio.Infrastructure;

internal static class BoundedImportRead
{
    public static async Task CopyAsync(Stream source, MemoryStream target, ImportReadLimits limits,
        CancellationToken token)
    {
        var block = new byte[81920];
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var count = (int)Math.Min(block.Length, limits.MaxFileBytes - target.Length + 1);
            var read = await source.ReadAsync(block.AsMemory(0, count), token).ConfigureAwait(false);
            if (read == 0) break;
            ImportReadLimits.Require(target.Length + read <= limits.MaxFileBytes, "fileBytes", limits.MaxFileBytes);
            target.Write(block, 0, read);
        }
    }

    public static void ValidatePackage(MemoryStream source, ImportReadLimits limits, CancellationToken token)
    {
        source.Position = 0;
        using (var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true))
        {
            ImportReadLimits.Require(archive.Entries.Count <= limits.MaxPackageEntries,
                "packageEntries", limits.MaxPackageEntries);
            long expanded = 0;
            foreach (var entry in archive.Entries)
            {
                token.ThrowIfCancellationRequested();
                ImportReadLimits.Require(entry.Length <= limits.MaxPartBytes, "partBytes", limits.MaxPartBytes);
                ImportReadLimits.Require(entry.Length <= limits.MaxExpandedBytes - expanded,
                    "expandedBytes", limits.MaxExpandedBytes);
                expanded += entry.Length;
            }
        }
        source.Position = 0;
    }
}
