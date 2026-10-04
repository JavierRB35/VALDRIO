using System.Globalization;
using System.Text;
using Valdrio.Domain;
using Valdrio.Localization;

namespace Valdrio.Tests;

[TestClass]
public sealed class RepositoryReviewLocalizationTests
{
    [TestMethod]
    public void R05_DiagnosticFormatter_UsesUiLanguageAndRegionalArgumentsIndependently()
    {
        var diagnostic = new DomainDiagnostic(
            "NUM_CONSERVATION_FAILURE",
            "Numeric.ConservationFailure",
            [
                new(
                    "expected",
                    DiagnosticArgumentType.Decimal,
                    "1234.5"),
                new(
                    "actual",
                    DiagnosticArgumentType.Decimal,
                    "1200"),
                new(
                    "difference",
                    DiagnosticArgumentType.Decimal,
                    "34.5")
            ]);

        var formatter =
            new DomainDiagnosticFormatter(
                new UiTextCatalog());

        var result = formatter.Format(
            diagnostic,
            CultureInfo.GetCultureInfo("en"),
            CultureInfo.GetCultureInfo("es-ES"));

        Assert.AreEqual(
            "NUM_CONSERVATION_FAILURE",
            result.Code);
        Assert.IsTrue(
            result.Message.StartsWith(
                "Numeric conservation failed",
                StringComparison.Ordinal));
        Assert.IsTrue(
            result.Message.Contains(
                "1234,5",
                StringComparison.Ordinal));
        Assert.IsFalse(
            result.Message.Contains(
                "1234.5",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void R06_MissingPreferencesUseDefaultsWithoutRecoveryWarning()
    {
        using var fixture =
            new PreferenceFileFixture();

        var sink =
            new RecordingRecoverySink();
        var loader =
            CreateLoader(
                fixture.Path,
                sink);

        var result =
            loader.LoadOrDefault();

        Assert.AreEqual(
            UserPresentationPreferences.Default,
            result);
        Assert.AreEqual(
            0,
            sink.Reasons.Count);
    }

    [TestMethod]
    public void R06_MalformedJsonRecoversToDefaults()
    {
        using var fixture =
            new PreferenceFileFixture("{");
        AssertRecovered(
            fixture.Path,
            "PREFERENCES_JSON_INVALID");
    }

    [TestMethod]
    public void R06_NullNumberOptionsRecoverToDefaults()
    {
        using var fixture =
            new PreferenceFileFixture(
                ValidJson(
                    numberOptions:
                        "null"));
        AssertRecovered(
            fixture.Path,
            "PREFERENCES_NUMBER_OPTIONS_MISSING");
    }

    [TestMethod]
    public void R06_OutOfRangeDecimalsRecoverToDefaults()
    {
        using var fixture =
            new PreferenceFileFixture(
                ValidJson(
                    numberOptions:
                        "{\"DecimalDigits\":9,\"UseGrouping\":true}"));
        AssertRecovered(
            fixture.Path,
            "PREFERENCES_DECIMAL_DIGITS_INVALID");
    }

    [TestMethod]
    public void R06_UnknownDatePresetRecoversToDefaults()
    {
        using var fixture =
            new PreferenceFileFixture(
                ValidJson(
                    datePreset: "999"));
        AssertRecovered(
            fixture.Path,
            "PREFERENCES_DATE_PRESET_INVALID");
    }

    [TestMethod]
    public void R06_UnknownZoneRecoversToDefaults()
    {
        using var fixture =
            new PreferenceFileFixture(
                ValidJson(
                    timeZone:
                        "Invalid/ReviewZone"));
        AssertRecovered(
            fixture.Path,
            "PREFERENCES_TIME_ZONE_INVALID");
    }

    private static void AssertRecovered(
        string path,
        string expectedReason)
    {
        var sink =
            new RecordingRecoverySink();
        var loader =
            CreateLoader(
                path,
                sink);

        var result =
            loader.LoadOrDefault();

        Assert.AreEqual(
            UserPresentationPreferences.Default,
            result);
        CollectionAssert.AreEqual(
            new[] { expectedReason },
            sink.Reasons.ToArray());
    }

    private static PresentationPreferencesLoader CreateLoader(
        string path,
        IPresentationPreferencesRecoverySink sink)
    {
        var languages =
            new LanguageCatalog();
        var validator =
            new PresentationPreferencesValidator(
                languages,
                new DisplayTimeZoneResolver());

        return new PresentationPreferencesLoader(
            new JsonUserPresentationPreferencesStore(
                path),
            validator,
            sink);
    }

    private static string ValidJson(
        string timeZone = "UTC",
        string datePreset = "0",
        string numberOptions =
            "{\"DecimalDigits\":2,\"UseGrouping\":true}") =>
        $$"""
        {
          "UiLanguageTag": "en",
          "FormatCultureTag": "en-US",
          "DisplayTimeZoneId": "{{timeZone}}",
          "DateDisplayPreset": {{datePreset}},
          "NumberDisplayOptions": {{numberOptions}}
        }
        """;

    private sealed class RecordingRecoverySink
        : IPresentationPreferencesRecoverySink
    {
        public List<string> Reasons { get; } = [];

        public void Recovered(
            string reasonCode) =>
            Reasons.Add(reasonCode);
    }

    private sealed class PreferenceFileFixture
        : IDisposable
    {
        private readonly string directory;

        public PreferenceFileFixture(
            string? content = null)
        {
            directory = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "valdrio-review-"
                + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(
                directory);

            Path = System.IO.Path.Combine(
                directory,
                "presentation.json");

            if (content is not null)
            {
                File.WriteAllText(
                    Path,
                    content,
                    new UTF8Encoding(false));
            }
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(
                    directory,
                    recursive: true);
            }
        }
    }
}
