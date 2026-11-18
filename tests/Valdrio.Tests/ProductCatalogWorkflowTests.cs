using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Valdrio.Application;
using Valdrio.Desktop;
using Valdrio.Domain;

namespace Valdrio.Tests;

public sealed partial class T06ImportConfirmationTests
{
    [TestMethod]
    public async Task ProductWorkflowSavesEffectiveCompositionAndPreservesPublishedSnapshots()
    {
        using var ui = await ResourceUiReady();
        var editor = new CompositionEditor(ui.Studies);
        editor.AddItem(); var product = editor.Items.Last();
        product.Code = "P-WORKFLOW"; product.Name = "Product"; product.ReferenceUnitCostInput = "99.25";
        editor.AddItem(); var material = editor.Items.Last();
        material.Code = "M-WORKFLOW"; material.Name = "Material"; material.Kind = CompositionItemKind.Material;
        material.ReferenceUnitCostInput = "2,5";
        Assert.IsTrue(await editor.SaveAsync(false), editor.Feedback);
        Assert.IsTrue(editor.OpenProduct(product.Id));
        Assert.IsFalse(editor.Dirty, "Opening alone must not persist a new snapshot.");
        editor.AddLine(); var line = editor.Lines.Single();
        line.Target = editor.Targets.Single(t => t.Id == material.Id);
        line.Quantity = "10"; line.Loss = CompositionLossKind.ExtraInput; line.Percent = "10";
        await editor.PreviewAsync();
        Assert.AreEqual("11", editor.Results.Single().Required);
        Assert.IsTrue(await editor.SaveChangesAsync(), editor.Feedback);
        var first = ui.Studies.ActiveCompositions!.ReadCompositions();
        var snapshot = CanonicalSnapshotJson.Serialize(first.Versions.Single());
        Assert.AreEqual(CompositionState.Published, first.Versions.Single().State);
        Assert.AreEqual(27.5m, first.Items.Single(i => i.Id == product.Id).ReferenceUnitCost);

        Assert.IsTrue(editor.OpenProduct(product.Id));
        await editor.PreviewAsync();
        Assert.AreEqual("11", editor.Results.Single().Required, "A transient editable copy must be previewable.");
        Assert.IsFalse(editor.Dirty);
        Assert.AreEqual(first.Revision, ui.Studies.ActiveCompositions.ReadCompositions().Revision);
        // Switching away from an untouched transient definition must not leave an invisible draft.
        Assert.IsTrue(editor.OpenProduct(product.Id));
        Assert.AreEqual(2, editor.Versions.Count);
        editor.Items.Single(i => i.Id == product.Id).Name = "Renamed";
        Assert.IsTrue(await editor.SaveChangesAsync(), editor.Feedback);
        Assert.AreEqual(1, ui.Studies.ActiveCompositions.ReadCompositions().Versions.Count);

        Assert.IsTrue(editor.OpenProduct(product.Id));
        editor.Lines.Single().Quantity = "12.5";
        Assert.IsTrue(await editor.SaveChangesAsync(), editor.Feedback);
        var second = ui.Studies.ActiveCompositions.ReadCompositions();
        Assert.AreEqual(2, second.Versions.Count);
        Assert.AreEqual(snapshot, CanonicalSnapshotJson.Serialize(second.Versions.First()));
        Assert.AreEqual(CompositionState.Published, second.Versions.Last().State);

        Assert.IsTrue(editor.OpenProduct(product.Id));
        editor.Lines.Single().Basis = ConsumptionBasis.PerOrder;
        Assert.IsTrue(await editor.SaveChangesAsync(), editor.Feedback);
        Assert.IsNull(ui.Studies.ActiveCompositions.ReadCompositions().Items.Single(i => i.Id == product.Id).ReferenceUnitCost,
            "An incomplete new calculation must not retain a stale composition-derived unit cost.");
        Assert.AreEqual(editor["PartialCostSaved"], editor.Feedback);
    }

    [TestMethod]
    public async Task ProductWorkflowInvalidNumbersKeepPendingEditsWithoutWriting()
    {
        using var ui = await ResourceUiReady();
        var editor = new CompositionEditor(ui.Studies);
        editor.AddItem(); var row = editor.Items.Last(); row.Code = "P-INVALID"; row.Name = "Product";
        row.ReferenceUnitCostInput = "1.234,50";
        Assert.IsFalse(await editor.SaveChangesAsync());
        Assert.IsTrue(editor.Dirty);
        Assert.AreEqual(0, ui.Studies.ActiveCompositions!.ReadCompositions().Items.Count);
        row.ReferenceUnitCostInput = "12.50";
        Assert.IsTrue(await editor.SaveChangesAsync(), editor.Feedback);
        var reopened = new CompositionEditor(ui.Studies);
        var saved = reopened.Items.Single(); saved.Active = false;
        Assert.IsFalse(saved.CanOpenComposition);
    }
}

[TestClass]
public sealed class ProductWorkbookTests
{
    [TestMethod]
    [DataRow("es-ES")]
    [DataRow("en-US")]
    public void ProductXlsxUsesFirstSheetPreservesTextCodesAndRejectsNumericIdentities(string culture)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xlsx");
        try
        {
            using (var doc = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook))
            {
                var book = doc.AddWorkbookPart(); book.Workbook = new Workbook();
                var sheet = book.AddNewPart<WorksheetPart>();
                Cell Text(string address, string text) => new() { CellReference = address, DataType = CellValues.InlineString, InlineString = new InlineString(new Text(text)) };
                var data = new SheetData(
                    new Row(Text("A1", "Reference"), Text("B1", "Name"), Text("C1", "Unit"), Text("D1", "Unit cost")) { RowIndex = 1 },
                    new Row(Text("A2", "0012"), Text("B2", "Steel"), Text("C2", "kg"), new Cell { CellReference = "D2", CellValue = new CellValue("1.234"), DataType = CellValues.Number }) { RowIndex = 2 });
                sheet.Worksheet = new Worksheet(data);
                book.Workbook.Append(new Sheets(new Sheet { Id = book.GetIdOfPart(sheet), SheetId = 1, Name = "Catalogue" }));
                book.Workbook.Save();
            }
            var result = ProductCostImport.ReadResult(path, CultureInfo.GetCultureInfo(culture));
            Assert.IsNull(result.Error); Assert.AreEqual("0012", result.Value!.Single().Reference);
            Assert.AreEqual(1.234m, result.Value!.Single().UnitCost);
            using (var doc = SpreadsheetDocument.Open(path, true))
            {
                var cell = doc.WorkbookPart!.WorksheetParts.Single().Worksheet!.Descendants<Cell>().Single(c => c.CellReference == "A2");
                cell.InlineString = null; cell.DataType = CellValues.Number; cell.CellValue = new CellValue("12");
            }
            Assert.IsNotNull(ProductCostImport.ReadResult(path, CultureInfo.GetCultureInfo(culture)).Error);
        }
        finally { File.Delete(path); }
    }
}

public sealed partial class T06ImportConfirmationTests
{
    [TestMethod]
    public async Task ProductWorkflowManualCostPrecedenceAndPartialBreakdown()
    {
        using var ui = await ResourceUiReady();
        var editor = new CompositionEditor(ui.Studies);
        editor.AddItem(); var product = editor.Items.Last();
        product.Code = "P-PRECEDENCE"; product.Name = "Producto"; product.Kind = CompositionItemKind.Product;
        product.ReferenceUnitCostInput = "99,25";
        Assert.AreEqual(editor["OriginManual"], product.CostOriginDisplay);
        editor.AddItem(); var material = editor.Items.Last();
        material.Code = "M-PRECEDENCE"; material.Name = "Acero"; material.Kind = CompositionItemKind.Material;
        material.ReferenceUnitCostInput = "2,5";
        Assert.IsTrue(await editor.SaveAsync(false), editor.Feedback);

        Assert.IsTrue(editor.OpenProduct(product.Id));
        editor.AddLine(); var line = editor.Lines.Single();
        line.Target = editor.Targets.Single(t => t.Id == material.Id);
        line.Quantity = "10"; line.Loss = CompositionLossKind.ExtraInput; line.Percent = "10";
        await editor.PreviewAsync();
        Assert.AreEqual("11", editor.Results.Single().Required);
        Assert.IsTrue(await editor.SaveAsync(true), editor.Feedback);

        var published = ui.Studies.ActiveCompositions!.ReadCompositions();
        var saved = published.Items.Single(i => i.Id == product.Id);
        Assert.AreEqual(27.5m, saved.ReferenceUnitCost);
        Assert.IsTrue(saved.ReferenceCostSource!.StartsWith("Composition · ", StringComparison.Ordinal));
        Assert.AreEqual(editor["OriginComposition"], editor.Items.Single(i => i.Id == product.Id).CostOriginDisplay);

        editor.NewVersion();
        editor.AddLine(); var orderLine = editor.Lines.Last();
        orderLine.Target = editor.Targets.Single(t => t.Id == material.Id);
        orderLine.Quantity = "1"; orderLine.Basis = ConsumptionBasis.PerOrder;
        var manual = editor.Items.Single(i => i.Id == product.Id);
        manual.ReferenceUnitCostInput = "99,25";
        Assert.AreEqual(editor["OriginManual"], manual.CostOriginDisplay,
            "An explicit manual edit must regain precedence before a partial publication.");
        Assert.IsTrue(await editor.SaveAsync(true), editor.Feedback);
        Assert.AreEqual(editor["PartialCostSaved"], editor.Feedback);
        var partial = ui.Studies.ActiveCompositions.ReadCompositions().Items.Single(i => i.Id == product.Id);
        Assert.AreEqual(99.25m, partial.ReferenceUnitCost);
        Assert.IsTrue(partial.ReferenceCostSource!.StartsWith("Manual · ", StringComparison.Ordinal));

        await editor.PreviewAsync();
        Assert.AreEqual(6, editor.Answers.Count, "Version comparison was removed; only the analyst questions remain.");
        CollectionAssert.AreEquivalent(
            new[] { editor["QNeeds"], editor["QBatches"], editor["QVersion"], editor["QLoss"], editor["QCost"], editor["QActual"] },
            editor.Answers.Select(a => a.Question).ToArray());
    }

    [TestMethod]
    public async Task ProductWorkflowNestedPerOrderInputDoesNotPublishStableUnitCost()
    {
        using var ui = await ResourceUiReady();
        var editor = new CompositionEditor(ui.Studies);
        editor.AddItem(); var material = editor.Items.Last();
        material.Code = "M-NESTED"; material.Name = "Material"; material.Kind = CompositionItemKind.Material;
        material.ReferenceUnitCostInput = "2";
        editor.AddItem(); var semi = editor.Items.Last();
        semi.Code = "S-NESTED"; semi.Name = "Semielaborado"; semi.Kind = CompositionItemKind.SemiFinished;
        editor.AddItem(); var final = editor.Items.Last();
        final.Code = "P-NESTED"; final.Name = "Producto"; final.Kind = CompositionItemKind.Product;
        Assert.IsTrue(await editor.SaveAsync(false), editor.Feedback);

        Assert.IsTrue(editor.OpenProduct(semi.Id));
        editor.AddLine(); var semiLine = editor.Lines.Single();
        semiLine.Target = editor.Targets.Single(t => t.Id == material.Id);
        semiLine.Quantity = "10"; semiLine.Basis = ConsumptionBasis.PerOrder;
        Assert.IsTrue(await editor.SaveChangesAsync(), editor.Feedback);
        var semiVersionId = editor.Version;
        Assert.IsNull(ui.Studies.ActiveCompositions!.ReadCompositions().Items.Single(i => i.Id == semi.Id).ReferenceUnitCost,
            "A direct per-order line already makes the semi-finished cost partial.");

        Assert.IsTrue(editor.OpenProduct(final.Id));
        editor.AddLine(); var finalLine = editor.Lines.Single();
        finalLine.Target = editor.Targets.Single(t => t.Kind == 1 && t.Id == semiVersionId);
        finalLine.Quantity = "1";
        await editor.PreviewAsync();
        Assert.IsTrue(editor.Results.Count > 0, editor.Feedback);
        Assert.IsTrue(await editor.SaveChangesAsync(), editor.Feedback);
        var savedFinal = ui.Studies.ActiveCompositions.ReadCompositions().Items.Single(i => i.Id == final.Id);
        Assert.IsFalse(savedFinal.ReferenceCostSource?.StartsWith("Composition · ", StringComparison.Ordinal) == true,
            "A per-order input expanded through a semi-finished recipe cannot produce a stable unit cost.");
        Assert.IsNull(savedFinal.ReferenceUnitCost,
            "Publishing the final product must not keep a composition-derived cost when its expansion depends on an order.");
    }

    [TestMethod]
    public async Task ProductWorkflowCsvImportKeepsTextCodeAndReportsBadUnit()
    {
        using var ui = await ResourceUiReady();
        var editor = new CompositionEditor(ui.Studies);
        var units = ui.Studies.PhysicalCompany!.Units;
        // The importer resolves a unit by code or symbol, so choose an
        // unambiguous one to test the happy path rather than unit ambiguity.
        var unit = units.First(u => units.Count(x =>
            string.Equals(x.Code, u.Code, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.Symbol, u.Code, StringComparison.OrdinalIgnoreCase)) == 1);
        var separator = ui.Studies.PhysicalFormatCulture.NumberFormat.NumberDecimalSeparator;
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        try
        {
            File.WriteAllText(path,
                "Reference;Name;Unit;Unit cost\r\n0012;Acero;" + unit.Code + ";12" + separator + "5\r\n",
                System.Text.Encoding.UTF8);
            await editor.ImportProductsAsync(path);
            var imported = editor.Items.Single(i => i.Code == "0012");
            Assert.AreEqual(12.5m, imported.ReferenceUnitCost);
            Assert.AreEqual(editor["OriginManual"], imported.CostOriginDisplay);
            Assert.IsTrue(imported.ReferenceCostSource!.Contains(Path.GetFileName(path), StringComparison.Ordinal));
            Assert.IsTrue(await editor.SaveAsync(false), editor.Feedback);
            Assert.AreEqual(12.5m, new CompositionEditor(ui.Studies).Items.Single(i => i.Code == "0012").ReferenceUnitCost);

            var count = editor.Items.Count;
            File.WriteAllText(path, "Reference;Name;Unit;Unit cost\r\n0013;Bad;NO-SUCH-UNIT;1\r\n", System.Text.Encoding.UTF8);
            await editor.ImportProductsAsync(path);
            Assert.AreEqual(string.Format(ui.Studies.PhysicalUiCulture, editor["ImportUnknownUnit"], "2"), editor.Feedback);
            Assert.AreEqual(count, editor.Items.Count, "A rejected import must not leave a partial row.");
        }
        finally { File.Delete(path); }
    }
}

[TestClass]
public sealed class ProductCompositionPresentationTests
{
    [TestMethod]
    [DataRow("es-ES", "× 1,1", "× 1,25", "12,5")]
    [DataRow("en-US", "× 1.1", "× 1.25", "12.5")]
    public void CompositionLossRowsShowExtraInputAndYieldExamples(
        string cultureName, string extraFactor, string yieldFactor, string yieldResult)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        var extra = new CompositionLineRow
        {
            Quantity = "10", Percent = "10", Loss = CompositionLossKind.ExtraInput,
            DisplayCulture = culture, QuantityWord = "Quantity"
        };
        Assert.AreEqual(extraFactor, extra.LossFactorDisplay);
        Assert.IsTrue(extra.AdjustedQuantityDisplay.EndsWith("11", StringComparison.Ordinal),
            "10 with 10% extra input must show 11.");

        var yield = new CompositionLineRow
        {
            Quantity = "10", Percent = "80", Loss = CompositionLossKind.Yield,
            DisplayCulture = culture, QuantityWord = "Quantity"
        };
        Assert.AreEqual(yieldFactor, yield.LossFactorDisplay);
        Assert.IsTrue(yield.AdjustedQuantityDisplay.EndsWith(yieldResult, StringComparison.Ordinal),
            "10 at 80% yield must show 12.5 with the active decimal mark.");
    }
}
