using Microsoft.VisualStudio.TestTools.UnitTesting;
using Valdrio.Desktop;

namespace Valdrio.Tests;

public sealed partial class T14PhysicalActivityTests
{
    [TestMethod]
    public void UxHistorySeparatesNormalRealStateAndPreservesAllReasonsWithoutWriting()
    {
        var (file, study, node, pool) = Fixture();
        using (file)
        {
            file.PhysicalActivity.Save(Save(study, node, pool, 0, 700, 610));
            file.PhysicalActivity.Save(Save(study, node, pool, 1, 700, 625, reason: "Parte omitido"));
            var editor = Editor(file, study);
            var history = editor.HistoryRows();
            Assert.AreEqual(2, history.Count);
            Assert.AreEqual("700", history[0].Normal);
            Assert.AreEqual("610", history[0].Real);
            Assert.AreEqual("625", history[1].Real);
            StringAssert.Contains(history[1].Reason, "Parte omitido");
            Assert.IsFalse(editor.HasPendingEdits);
            Assert.AreEqual(2, file.PhysicalActivity.ReadHistory(study.CompanyId, study.Id, pool.Id).Count);
            StringAssert.Contains(editor.SaveDraftLabel, editor.SelectedRow!.Node);
        }
    }

    [TestMethod]
    public async Task UxPhysicalTemplateUsesActualCodesAndDatesButNeverInventsMeasurements()
    {
        var (file, study, node, _) = Fixture();
        using (file)
        {
            var editor = Editor(file, study);
            var import = new PhysicalImportEditor(editor);
            var template = import.CreateTemplate();
            StringAssert.Contains(template, node.Code);
            StringAssert.Contains(template, "2026-10-01");
            StringAssert.Contains(template, "2026-10-31");
            var path = Path.Combine(directory, "template.csv");
            File.WriteAllText(path, template);
            await import.LoadFile(path); await import.Preview();
            Assert.AreEqual(1, import.Lines.Count);
            Assert.IsFalse(import.Lines[0].Source.CanChoose);
            Assert.IsFalse(import.CanApply);
            Assert.AreEqual(0, file.PhysicalActivity.ReadCurrent(study.CompanyId, study.Id).Count);
        }
    }
}
