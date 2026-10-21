namespace Valdrio.Desktop;

/// <summary>Pure row-major cell order. No UI state or economic state is changed.</summary>
internal static class EditableGridTabOrder
{
    public static IEnumerable<(int Row, int Column)> After(
        int rowCount, int columnCount, int currentRow, int currentColumn, bool backwards)
    {
        if (rowCount <= 0 || columnCount <= 0 ||
            currentRow < 0 || currentRow >= rowCount ||
            currentColumn < 0 || currentColumn >= columnCount)
            yield break;

        var current = currentRow * columnCount + currentColumn;
        var direction = backwards ? -1 : 1;
        for (var index = current + direction;
             index >= 0 && index < rowCount * columnCount;
             index += direction)
            yield return (index / columnCount, index % columnCount);
    }
}
