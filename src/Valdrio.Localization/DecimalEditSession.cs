using System.Globalization;

namespace Valdrio.Localization;

public sealed class DecimalEditSession
{
    public DecimalEditSession(CultureInfo capturedCulture)
    {
        CapturedCulture = CultureInfo.ReadOnly(
            (CultureInfo)capturedCulture.Clone());
    }

    public CultureInfo CapturedCulture { get; }

    public bool TryCommit(string text, out decimal value) =>
        decimal.TryParse(
            text,
            NumberStyles.Number,
            CapturedCulture,
            out value);
}
