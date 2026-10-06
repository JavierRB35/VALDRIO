using System.Globalization;
using Valdrio.Domain;

namespace Valdrio.Application;

// Technical resource limits, independent of accounting policy and source culture.
public sealed record ImportReadLimits
{
    public static ImportReadLimits Default { get; } = new();

    public ImportReadLimits(long maxFileBytes = 32 * 1024 * 1024,
        long maxExpandedBytes = 128 * 1024 * 1024, long maxPartBytes = 32 * 1024 * 1024,
        int maxRows = 100_000, int maxColumns = 16_384, int maxCellCharacters = 262_144,
        int maxSharedStrings = 250_000, int maxPackageEntries = 8192)
    {
        if (maxFileBytes < 1 || maxExpandedBytes < 1 || maxPartBytes < 1 ||
            maxRows < 1 || maxColumns is < 1 or > 16_384 || maxCellCharacters < 1 ||
            maxSharedStrings < 1 || maxPackageEntries < 1)
            throw new ArgumentOutOfRangeException(nameof(maxFileBytes));
        MaxFileBytes = maxFileBytes; MaxExpandedBytes = maxExpandedBytes; MaxPartBytes = maxPartBytes;
        MaxRows = maxRows; MaxColumns = maxColumns; MaxCellCharacters = maxCellCharacters;
        MaxSharedStrings = maxSharedStrings; MaxPackageEntries = maxPackageEntries;
    }

    public long MaxFileBytes { get; }
    public long MaxExpandedBytes { get; }
    public long MaxPartBytes { get; }
    public int MaxRows { get; }
    public int MaxColumns { get; }
    public int MaxCellCharacters { get; }
    public int MaxSharedStrings { get; }
    public int MaxPackageEntries { get; }

    public static void Require(bool withinLimit, string resource, long maximum)
    {
        if (!withinLimit)
            throw ImportRules.Error("IMP_SOURCE_LIMIT", "Import.SourceLimit",
                new DiagnosticArgument("resource", DiagnosticArgumentType.Text, resource),
                new DiagnosticArgument("limit", DiagnosticArgumentType.Integer,
                    maximum.ToString(CultureInfo.InvariantCulture)));
    }
}
