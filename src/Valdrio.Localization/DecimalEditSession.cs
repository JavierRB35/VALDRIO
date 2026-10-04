using System.Globalization;

namespace Valdrio.Localization;

public sealed class DecimalEditSession
{
    private const NumberStyles AcceptedStyles =
        NumberStyles.AllowLeadingSign |
        NumberStyles.AllowDecimalPoint |
        NumberStyles.AllowThousands;

    public DecimalEditSession(CultureInfo capturedCulture)
    {
        CapturedCulture = CultureInfo.ReadOnly(
            (CultureInfo)capturedCulture.Clone());
    }

    public CultureInfo CapturedCulture { get; }

    public bool TryCommit(string text, out decimal value)
    {
        value = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var candidate = text.Trim();
        var numberFormat = CapturedCulture.NumberFormat;

        if (!HasValidStructure(candidate, numberFormat))
        {
            return false;
        }

        return decimal.TryParse(
            candidate,
            AcceptedStyles,
            CapturedCulture,
            out value);
    }

    private static bool HasValidStructure(
        string text,
        NumberFormatInfo numberFormat)
    {
        var unsigned = RemoveLeadingSign(
            text,
            numberFormat);

        if (unsigned.Length == 0)
        {
            return false;
        }

        var decimalSeparator =
            numberFormat.NumberDecimalSeparator;

        var decimalIndex = unsigned.IndexOf(
            decimalSeparator,
            StringComparison.Ordinal);

        if (decimalIndex >= 0 &&
            decimalIndex != unsigned.LastIndexOf(
                decimalSeparator,
                StringComparison.Ordinal))
        {
            return false;
        }

        var integerPart = decimalIndex < 0
            ? unsigned
            : unsigned[..decimalIndex];

        var fractionPart = decimalIndex < 0
            ? null
            : unsigned[(decimalIndex + decimalSeparator.Length)..];

        if (fractionPart is not null &&
            (fractionPart.Length == 0 ||
             !fractionPart.All(char.IsDigit)))
        {
            return false;
        }

        if (integerPart.Length == 0)
        {
            return fractionPart is not null;
        }

        return HasValidIntegerGrouping(
            integerPart,
            numberFormat);
    }

    private static string RemoveLeadingSign(
        string text,
        NumberFormatInfo numberFormat)
    {
        foreach (var sign in new[]
                 {
                     numberFormat.NegativeSign,
                     numberFormat.PositiveSign
                 })
        {
            if (!string.IsNullOrEmpty(sign) &&
                text.StartsWith(
                    sign,
                    StringComparison.Ordinal))
            {
                return text[sign.Length..];
            }
        }

        return text;
    }

    private static bool HasValidIntegerGrouping(
        string integerPart,
        NumberFormatInfo numberFormat)
    {
        var groupSeparator =
            numberFormat.NumberGroupSeparator;

        if (string.IsNullOrEmpty(groupSeparator) ||
            !integerPart.Contains(
                groupSeparator,
                StringComparison.Ordinal))
        {
            return integerPart.All(char.IsDigit);
        }

        var groups = integerPart.Split(
            [groupSeparator],
            StringSplitOptions.None);

        if (groups.Any(group =>
                group.Length == 0 ||
                !group.All(char.IsDigit)))
        {
            return false;
        }

        return GroupSizesAreValid(
            groups,
            numberFormat.NumberGroupSizes);
    }

    private static bool GroupSizesAreValid(
        IReadOnlyList<string> groups,
        IReadOnlyList<int> groupSizes)
    {
        if (groups.Count < 2 ||
            groupSizes.Count == 0 ||
            groupSizes[0] == 0)
        {
            return false;
        }

        var sizeIndex = 0;

        for (var index = groups.Count - 1;
             index >= 1;
             index--)
        {
            var expected = groupSizes[
                Math.Min(
                    sizeIndex,
                    groupSizes.Count - 1)];

            if (expected == 0 ||
                groups[index].Length != expected)
            {
                return false;
            }

            if (sizeIndex < groupSizes.Count - 1)
            {
                sizeIndex++;
            }
        }

        var leftExpected = groupSizes[
            Math.Min(
                sizeIndex,
                groupSizes.Count - 1)];

        return leftExpected == 0
            ? groups[0].Length >= 1
            : groups[0].Length >= 1 &&
              groups[0].Length <= leftExpected;
    }
}
