using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Valdrio.Localization;

namespace Valdrio.Tests;

[TestClass]
public sealed class T01AInternationalizationTests
{
    [TestMethod]
    public void I01_UiLanguageAndSpanishFormat_AreIndependentFromUsd()
    {
        var languages = new LanguageCatalog();
        var preferences = new PresentationPreferencesService(
            languages,
            UserPresentationPreferences.Default);
        var formatter = new PresentationFormatter(new DisplayTimeZoneResolver());

        var before = formatter.FormatCurrency(
            1234.50m,
            "USD",
            CultureInfo.GetCultureInfo(preferences.Current.FormatCultureTag),
            preferences.Current.NumberDisplayOptions);

        preferences.Update(preferences.Current with { UiLanguageTag = "es" });

        var after = formatter.FormatCurrency(
            1234.50m,
            "USD",
            CultureInfo.GetCultureInfo(preferences.Current.FormatCultureTag),
            preferences.Current.NumberDisplayOptions);

        Assert.AreEqual("1.234,50 USD", before);
        Assert.AreEqual(before, after);
        Assert.AreEqual("USD", before[^3..]);
    }

    [TestMethod]
    public void I02_UsFormat_DoesNotIntroduceUsdForEur()
    {
        var formatter = new PresentationFormatter(new DisplayTimeZoneResolver());
        var value = formatter.FormatCurrency(
            1234.50m,
            "EUR",
            CultureInfo.GetCultureInfo("en-US"),
            new NumberDisplayOptions());

        Assert.AreEqual("1,234.50 EUR", value);
        Assert.IsFalse(value.Contains("USD", StringComparison.Ordinal));
    }

    [TestMethod]
    public void DisplayInstant_ContainsLocalizedDateTimeAndExplicitOffset()
    {
        var formatter = new PresentationFormatter(new DisplayTimeZoneResolver());
        var value = formatter.FormatInstant(
            new DateTimeOffset(2026, 10, 4, 0, 30, 0, TimeSpan.Zero),
            "UTC",
            CultureInfo.GetCultureInfo("en-US"));

        Assert.IsTrue(value.Contains("2026", StringComparison.Ordinal));
        Assert.IsTrue(value.Contains("12:30", StringComparison.Ordinal));
        Assert.IsTrue(value.Contains("+00:00", StringComparison.Ordinal));
        Assert.IsFalse(value.Contains("AD +00:00", StringComparison.Ordinal));
    }

    [TestMethod]
    public void I05_PresentationPreferences_AreExcludedFromSyntheticEconomicHash()
    {
        const string canonicalEconomicFixture =
            "C09|source=100|A=40|B=60|policy=fixture-v1";

        var before = Hash(canonicalEconomicFixture);

        var preferences = new PresentationPreferencesService(
            new LanguageCatalog(),
            UserPresentationPreferences.Default);

        preferences.Update(preferences.Current with
        {
            UiLanguageTag = "es",
            FormatCultureTag = "en-US",
            DisplayTimeZoneId = "UTC"
        });

        var after = Hash(canonicalEconomicFixture);

        Assert.AreEqual(before, after);
    }

    [TestMethod]
    public void I10_UnsupportedRuntimeLanguage_FallsBackToEnglishAndLogsKey()
    {
        var sink = new RecordingFallbackSink();
        var catalog = new UiTextCatalog(sink);

        var value = catalog.Get(
            "WindowTitle",
            CultureInfo.GetCultureInfo("fr-FR"));

        Assert.AreEqual("VALDRIO - presentation preferences", value);
        CollectionAssert.AreEqual(
            new[] { "fr-FR|WindowTitle" },
            sink.Entries.ToArray());
    }

    [TestMethod]
    public void I11_Pseudolocalization_ExpandsAtLeastFortyPercent_AndPreservesIdentifiers()
    {
        const string source = "Account ACC-001 formula A/B and {0}";
        var expanded = PseudoLocalization.Expand(source);

        Assert.IsTrue(expanded.Length >= Math.Ceiling(source.Length * 1.40));
        Assert.IsTrue(expanded.Contains("ACC-001", StringComparison.Ordinal));
        Assert.IsTrue(expanded.Contains("A/B", StringComparison.Ordinal));
        Assert.IsTrue(expanded.Contains("{0}", StringComparison.Ordinal));
    }

    [TestMethod]
    public void RegionChange_DoesNotReinterpretPendingEdit()
    {
        var preferences = new PresentationPreferencesService(
            new LanguageCatalog(),
            UserPresentationPreferences.Default with { FormatCultureTag = "es-ES" });

        var edit = new DecimalEditSession(
            CultureInfo.GetCultureInfo(preferences.Current.FormatCultureTag));

        preferences.Update(preferences.Current with { FormatCultureTag = "en-US" });

        var committed = edit.TryCommit("1.234,50", out var value);

        Assert.IsTrue(committed);
        Assert.AreEqual(1234.50m, value);
        Assert.AreEqual("es-ES", edit.CapturedCulture.Name);
        Assert.AreEqual("en-US", preferences.Current.FormatCultureTag);
    }

    [TestMethod]
    public void LanguageFormatAndZone_UpdateIndependently()
    {
        var service = new PresentationPreferencesService(
            new LanguageCatalog(),
            UserPresentationPreferences.Default);

        var original = service.Current;

        service.Update(original with { UiLanguageTag = "es" });
        Assert.AreEqual("es", service.Current.UiLanguageTag);
        Assert.AreEqual(original.FormatCultureTag, service.Current.FormatCultureTag);
        Assert.AreEqual(original.DisplayTimeZoneId, service.Current.DisplayTimeZoneId);

        service.Update(service.Current with { FormatCultureTag = "en-US" });
        Assert.AreEqual("es", service.Current.UiLanguageTag);
        Assert.AreEqual("en-US", service.Current.FormatCultureTag);
        Assert.AreEqual(original.DisplayTimeZoneId, service.Current.DisplayTimeZoneId);

        service.Update(service.Current with { DisplayTimeZoneId = "UTC" });
        Assert.AreEqual("es", service.Current.UiLanguageTag);
        Assert.AreEqual("en-US", service.Current.FormatCultureTag);
        Assert.AreEqual("UTC", service.Current.DisplayTimeZoneId);
    }

    [TestMethod]
    public void UserPresentationPreferences_RoundTripOutsideEconomicData()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "valdrio-t01a-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "presentation.json");

        try
        {
            var expected = UserPresentationPreferences.Default with
            {
                UiLanguageTag = "es",
                FormatCultureTag = "en-US",
                DisplayTimeZoneId = "UTC",
                DateDisplayPreset = DateDisplayPreset.Iso,
                NumberDisplayOptions = new NumberDisplayOptions(3, false)
            };

            var store = new JsonUserPresentationPreferencesStore(path);
            store.Save(expected);

            var loaded = store.TryLoad(out var actual);

            Assert.IsTrue(loaded);
            Assert.AreEqual(expected, actual);
            Assert.IsTrue(path.Contains("presentation", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void PluralRules_AreRegisteredForPublishedLanguages()
    {
        var registry = new PluralRuleRegistry();

        Assert.AreEqual(PluralCategory.One, registry.GetRequired("en").Select(1));
        Assert.AreEqual(PluralCategory.Other, registry.GetRequired("en").Select(2));
        Assert.AreEqual(PluralCategory.One, registry.GetRequired("es").Select(1));
        Assert.AreEqual(PluralCategory.Other, registry.GetRequired("es").Select(0));
    }

    [TestMethod]
    public void MessageDescriptor_KeepsCodeKeyAndTypedArgumentsSeparate()
    {
        var descriptor = new MessageDescriptor(
            "IMP_EXAMPLE",
            "Import.InvalidAmount",
            [
                new("amount", MessageArgumentType.Decimal, 1234.50m),
                new("currency", MessageArgumentType.CurrencyCode, "USD")
            ]);

        Assert.AreEqual("IMP_EXAMPLE", descriptor.Code);
        Assert.AreEqual("Import.InvalidAmount", descriptor.MessageKey);
        Assert.AreEqual(MessageArgumentType.Decimal, descriptor.Arguments[0].Type);
        Assert.AreEqual(MessageArgumentType.CurrencyCode, descriptor.Arguments[1].Type);
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed class RecordingFallbackSink : ILocalizationFallbackSink
    {
        public List<string> Entries { get; } = [];

        public void MissingKey(string requestedLanguageTag, string key) =>
            Entries.Add($"{requestedLanguageTag}|{key}");
    }
}
