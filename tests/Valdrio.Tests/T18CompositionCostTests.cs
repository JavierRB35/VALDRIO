using System.Globalization;
using Valdrio.Application;
using Valdrio.Calculation;
using Valdrio.Desktop;
using Valdrio.Domain;

namespace Valdrio.Tests;

[TestClass]
public sealed class T18CompositionCostTests
{
    private static EntityId Id() => new(Guid.NewGuid());

    [TestMethod]
    public void T18_EstimateSeparatesPricedComponentsMissingPricesActivityAndOutputReference()
    {
        var each = new UnitDefinition(new(Id()), "each", "count", "ea", 0, true);
        var kg = new UnitDefinition(new(Id()), "kg", "mass", "kg", 3, false);
        var hour = new UnitDefinition(new(Id()), "hour", "time", "h", 3, false);
        var product = new CompositionItem(Id(), "OUT", "Output", each.Id, CompositionItemKind.Product, true) { ReferenceUnitCost = 10m, ReferenceCostSource = "costs.csv · 2026-10-25" };
        var priced = new CompositionItem(Id(), "MAT-A", "Priced", kg.Id, CompositionItemKind.Material, true) { ReferenceUnitCost = 4m, ReferenceCostSource = "costs.csv · 2026-10-25" };
        var unpriced = new CompositionItem(Id(), "MAT-B", "Unpriced", kg.Id, CompositionItemKind.Material, true);
        var model = Id(); var pool = new CompositionPool(Id(), model, "Labour", hour.Id, true);
        var version = new CompositionVersion(Id(), product.Id, 1, model, 1, CompositionState.Published, "", [
            new(Id(), priced.Id, null, null, 3, kg.Id, 1, ConsumptionBasis.PerUnit, CompositionLossKind.None, 0),
            new(Id(), unpriced.Id, null, null, 2, kg.Id, 1, ConsumptionBasis.PerUnit, CompositionLossKind.None, 0),
            new(Id(), null, null, pool.Id, 2, hour.Id, 1, ConsumptionBasis.PerUnit, CompositionLossKind.None, 0)]);
        var catalog = new CompositionCatalog(Id(), 1, [product, priced, unpriced], [version]);
        var preview = CompositionCalculator.Preview(catalog, version.Id, 2, [each, kg, hour], [pool]);

        var estimate = CompositionCostEstimator.Estimate(catalog, preview, product.Id, new CurrencyCode("EUR"));

        Assert.AreEqual(24m, estimate.PricedComponents);
        Assert.AreEqual(20m, estimate.ImportedOutputReference);
        CollectionAssert.AreEqual(new[] { "MAT-B · Unpriced" }, estimate.UnpricedComponents.ToArray());
        Assert.IsTrue(estimate.HasActivity);
        Assert.AreEqual("EUR", estimate.Currency.Value);
    }

    [TestMethod]
    public void T18_ImportReadsSpanishSemicolonAndRegionalDecimalHeaders()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "Referencia;Nombre;Unidad de medida;Coste unitario\r\nP-104;\"Tornillo; inox\";ud;0,12\r\n", System.Text.Encoding.UTF8);
            var rows = ProductCostImport.Read(path, CultureInfo.GetCultureInfo("es-ES"));
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("P-104", rows[0].Reference);
            Assert.AreEqual("Tornillo; inox", rows[0].Name);
            Assert.AreEqual("ud", rows[0].Unit);
            Assert.AreEqual(0.12m, rows[0].UnitCost);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void T18_ImportReadsEnglishTabAndRejectsDuplicateReferencesAndNegativeCost()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "Reference\tName\tUnit of measure\tUnit cost\nX-1\tWidget\tea\t2.50\n", System.Text.Encoding.UTF8);
            Assert.AreEqual(2.50m, ProductCostImport.Read(path, CultureInfo.InvariantCulture).Single().UnitCost);
            File.WriteAllText(path, "Reference,Name,Unit,Unit Cost\nX-1,Widget,ea,1\nx-1,Widget 2,ea,2\n", System.Text.Encoding.UTF8);
            Assert.ThrowsExactly<InvalidDataException>(() => ProductCostImport.Read(path, CultureInfo.InvariantCulture));
            File.WriteAllText(path, "Reference,Name,Unit,Unit Cost\nX-1,Widget,ea,-1\n", System.Text.Encoding.UTF8);
            Assert.ThrowsExactly<InvalidDataException>(() => ProductCostImport.Read(path, CultureInfo.InvariantCulture));
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void T18_ReimportUpdatesNameAndCostKeepsIdentityAndAddsNewReferencesAtomically()
    {
        var each = new UnitDefinition(new(Id()), "each", "count", "ea", 0, true);
        var existingId = Id();
        var existing = new CompositionItem(existingId, "P-1", "Old name", each.Id, CompositionItemKind.Product, false)
            { ReferenceUnitCost = 2m, ReferenceCostSource = "old.csv" };
        var source = "new.csv · 2026-10-25";
        var input = new[]
        {
            new ProductCostImportRow("P-1", "Updated name", "ea", 3.5m, 2),
            new ProductCostImportRow("M-2", "New material", "each", 1.25m, 3)
        };

        var merged = ProductCostImport.Merge([existing], input, [each], CompositionItemKind.Material, source);

        var updated = merged.Single(i => i.Code == "P-1");
        Assert.AreEqual(existingId, updated.Id);
        Assert.AreEqual("Updated name", updated.Name);
        Assert.AreEqual(3.5m, updated.ReferenceUnitCost);
        Assert.AreEqual(each.Id, updated.UnitId);
        Assert.AreEqual(CompositionItemKind.Product, updated.Kind);
        Assert.IsFalse(updated.Active);
        var added = merged.Single(i => i.Code == "M-2");
        Assert.AreEqual("New material", added.Name);
        Assert.AreEqual(CompositionItemKind.Material, added.Kind);
        Assert.AreEqual(1.25m, added.ReferenceUnitCost);
        Assert.AreEqual(source, added.ReferenceCostSource);

        Assert.ThrowsExactly<InvalidDataException>(() => ProductCostImport.Merge([existing],
            [new("P-1", "Must not partially apply", "unknown", 99m, 2)], [each], CompositionItemKind.Product, source));
        Assert.AreEqual("Old name", existing.Name);
        Assert.AreEqual(2m, existing.ReferenceUnitCost);
    }

    [TestMethod]
    public void T18_OptionalCostFieldsPreserveOlderCompositionSnapshots()
    {
        var item = new CompositionItem(Id(), "P", "Product", new(Id()), CompositionItemKind.Product, true);
        var json = CanonicalSnapshotJson.Serialize(new CompositionCatalog(Id(), 0, [item], []));
        Assert.IsFalse(json.Contains("ReferenceUnitCost", StringComparison.Ordinal));
        var loaded = CanonicalSnapshotJson.Deserialize<CompositionCatalog>(json);
        Assert.IsNull(loaded.Items.Single().ReferenceUnitCost);
        Assert.IsNull(loaded.Items.Single().ReferenceCostSource);
    }
}
