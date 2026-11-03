using Valdrio.Application;
using Valdrio.Desktop;

namespace Valdrio.Tests;

public sealed partial class T14PhysicalActivityTests
{
    [TestMethod]
    public void Calendar_ManualAdjustmentAndNotesPreserveParametersAcrossReopen()
    {
        var (file, study, _, _) = Fixture();
        var path = file.Path;
        using (file)
        {
            var editor = new PhysicalCapacityAssistantEditor(Editor(file, study))
            {
                OperatingDays = "22", ShiftsPerDay = "1", HoursPerShift = "8",
                EquivalentResources = "3", Stoppages = "1", NormalFactor = "90", UseProposal = true
            };
            editor.RealValue = "400";
            editor.Save(true);
            editor.NormalValue = "416";
            editor.NormalNotes = "Reviewed adjustment";
            editor.Save(true);
        }
        using var reopened = Service().Open(path);
        var restored = new PhysicalCapacityAssistantEditor(Editor(reopened, study));
        Assert.AreEqual(PhysicalCapacityMethod.Calendar, restored.Method);
        Assert.AreEqual("22", restored.OperatingDays);
        Assert.AreEqual("1", restored.ShiftsPerDay);
        Assert.AreEqual("8", restored.HoursPerShift);
        Assert.AreEqual("3", restored.EquivalentResources);
        Assert.AreEqual("1", restored.Stoppages);
        Assert.AreEqual(PhysicalStoppageBasis.PerResourceShift, restored.StoppageBasis);
        Assert.AreEqual("416", restored.NormalValue);
        Assert.AreEqual("400", restored.RealValue);
        Assert.AreEqual("Reviewed adjustment", restored.NormalNotes);
        Assert.IsFalse(restored.UseProposal);
        StringAssert.StartsWith(restored.Proposed, "415,8");
        restored.HoursPerShift = "7";
        StringAssert.StartsWith(restored.Proposed, "356,4");
        Assert.AreEqual("416", restored.NormalValue); // recalculation must not erase the adjustment
    }

    [TestMethod]
    public void Calendar_RecoversLostParametersFromSamePeriodHistoryWithoutRewritingActualValues()
    {
        var (file, study, node, pool) = Fixture();
        using (file)
        {
            var description = "calendar: days=22; shifts=1; hours=8; resources=3; stops=66; factor=0.9; reason=plan; stopsBasis=PerResourceShift; stopsEntered=1; stopsMultiplier=66";
            file.PhysicalActivity.Save(Save(study, node, pool, 0, 415.8m, 400) with
                { NormalEvidence = new(PhysicalEvidenceQuality.Calculated, "Calendar", description) });
            file.PhysicalActivity.Save(Save(study, node, pool, 1, 416, 401) with
                { NormalEvidence = new(PhysicalEvidenceQuality.Declared, "Manual entry", "") });
            var restored = new PhysicalCapacityAssistantEditor(Editor(file, study));
            Assert.AreEqual("8", restored.HoursPerShift);
            Assert.AreEqual("22", restored.OperatingDays);
            StringAssert.StartsWith(restored.Proposed, "415,8");
            Assert.AreEqual("416", restored.NormalValue);
            Assert.AreEqual("401", restored.RealValue);
            Assert.IsFalse(restored.Host.HasPendingEdits);
            Assert.AreEqual(2, file.PhysicalActivity.ReadHistory(study.CompanyId, study.Id, pool.Id).Count);
            file.PhysicalActivity.Annul(new(study.CompanyId, study.Id, study.Revision, node.Id, pool.Id, 2, "void", "test"));
            var annulled = new PhysicalCapacityAssistantEditor(Editor(file, study));
            Assert.AreEqual("", annulled.OperatingDays);
            Assert.AreEqual("", annulled.NormalValue);
        }
    }
}
