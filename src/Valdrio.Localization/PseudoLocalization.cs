namespace Valdrio.Localization;

public static class PseudoLocalization
{
    public static string Expand(string text, decimal minimumExpansion = 0.40m)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length == 0)
        {
            return text;
        }

        var targetLength = (int)Math.Ceiling(text.Length * (1m + minimumExpansion));
        var result = $"⟦{text}⟧";

        if (result.Length >= targetLength)
        {
            return result;
        }

        return result + new string('·', targetLength - result.Length);
    }
}
