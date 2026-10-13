using System.Globalization;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Infrastructure;
using Valdrio.Infrastructure.CompanyFiles;
using Valdrio.Localization;

namespace Valdrio.Tests;

[TestClass]
public sealed class DefaultActivityUnitCatalogTests
{
    private sealed class Clock : IClock
    {
        public UtcInstant GetUtcNow() => UtcInstant.ParseIso("2026-10-13T14:00:00.0000000Z");
    }

    [TestMethod]
    public void R0_PresetsAreUniqueAndHaveCompleteEnglishSpanishLabels()
    {
        var all = DefaultActivityUnitCatalog.All;
        Assert.AreEqual(20, all.Count);
        Assert.AreEqual(20, all.Select(x => x.Code).Distinct(StringComparer.Ordinal).Count());
        Assert.IsTrue(all.All(x => x.DecimalScale is >= 0 and <= 12));
        Assert.IsTrue(all.All(x => !x.IsDiscrete || x.DecimalScale == 0));
        var texts = new UiTextCatalog();
        foreach (var preset in all)
        foreach (var language in new[] { "en", "es" })
            Assert.IsFalse(string.IsNullOrWhiteSpace(texts.Get(
                "ActivityUnit." + preset.Code, CultureInfo.GetCultureInfo(language))));
        Assert.AreEqual("Machine hour", texts.Get("ActivityUnit.machine_hour", CultureInfo.GetCultureInfo("en")));
        Assert.AreEqual("Hora de máquina", texts.Get("ActivityUnit.machine_hour", CultureInfo.GetCultureInfo("es")));
    }

    [TestMethod]
    public void R0_ExistingCustomUnitIsPreservedAndAddingMissingDefaultsIsIdempotent()
    {
        var custom = new UnitDefinition(new(new(Guid.NewGuid())), "machine_hour", "custom", "x", 0, true);
        var missing = DefaultActivityUnitCatalog.Missing([custom]);
        Assert.AreEqual(19, missing.Count);
        Assert.IsFalse(DefaultActivityUnitCatalog.IsPreset(custom));
        Assert.IsFalse(missing.Any(x => x.Code == "machine_hour"));
        Assert.IsFalse(DefaultActivityUnitCatalog.HasMissing(new[] { custom }.Concat(missing)));
        Assert.AreEqual(0, DefaultActivityUnitCatalog.Missing(new[] { custom }.Concat(missing)).Count);
        Assert.AreEqual(20, new[] { custom }.Concat(missing).Select(x => x.Id).Distinct().Count());
    }

    [TestMethod]
    public void R0_DefaultsPersistThroughExistingAuditedCompanyFileFormat()
    {
        var directory = Path.Combine(Path.GetTempPath(), "valdrio-r0-units-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "units.valdrio");
            var catalog = new JsonCurrencyCatalog(Path.Combine(AppContext.BaseDirectory, "reference", "currency-catalog-example.json"));
            var service = new SqliteCompanyFileService(catalog, new Clock());
            using (var file = service.Create(new(path, "Units", new("EUR"), PrecisionPolicy.Pdec1Proposed)))
            {
                var initial = file.Read();
                var saved = file.AddUnits(DefaultActivityUnitCatalog.Missing(initial.Units), initial.Revision, "test");
                Assert.AreEqual(20, saved.Units.Count);
                Assert.AreEqual(1L, saved.Revision);
                Assert.IsFalse(DefaultActivityUnitCatalog.HasMissing(saved.Units));
            }
            using var reopened = service.Open(path);
            Assert.AreEqual(20, reopened.Read().Units.Count);
            Assert.AreEqual(20, reopened.Models.ReadDriverUnits().Count);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
