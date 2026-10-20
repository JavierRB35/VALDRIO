using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Valdrio.Application;
using Valdrio.Desktop;
using Valdrio.Domain;

namespace Valdrio.Tests;

public sealed partial class T14PhysicalActivityTests
{
    [TestMethod]
    public void T14_CalendarOpeningAndTypingDoNotReadPersistenceAndKeepZeroDistinctFromEmpty()
    {
        var (file, study, _, _) = Fixture();
        var host = Editor(file, study);
        var structure = host.Structure;
        // A disposed session turns any accidental database read into a failure.
        // Calculator proposals must depend only on the already validated row snapshot.
        file.Dispose();
        host.SelectedRow!.NormalText = "0";
        var first = new PhysicalCapacityAssistantEditor(host);
        Assert.AreEqual("0", first.DeclaredQuantity);
        Assert.AreSame(structure, host.Structure);
        first.OperatingDays = "20"; first.ShiftsPerDay = "1"; first.HoursPerShift = "8";
        first.EquivalentResources = "1"; first.Stoppages = "0";
        first.NormalFactor = "100"; first.FactorReason = "Calendario aprobado";
        Assert.IsTrue(first.CanAdopt, first.Feedback);
        Assert.AreEqual("0", host.SelectedRow.NormalText); // typing is only a proposal
        host.SelectedRow.NormalText = "";
        var second = new PhysicalCapacityAssistantEditor(host);
        Assert.AreEqual("", second.DeclaredQuantity);
        Assert.AreEqual("", second.OperatingDays); // no prior form's temporary inputs
        Assert.IsFalse(second.CanAdopt);
        Assert.IsEmpty(second.Historical); // history is not loaded for calendar use
    }

    [TestMethod]
    public void T14_DowntimeFrequencyRecalculatesAndPersistsTheAppliedProposal()
    {
        var (file, study, _, _) = Fixture();
        var path = file.Path;
        using (file)
        {
            var host = Editor(file, study);
            var assistant = new PhysicalCapacityAssistantEditor(host)
            {
                OperatingDays = "20", ShiftsPerDay = "2", HoursPerShift = "8",
                EquivalentResources = "3", Stoppages = "0,5", NormalFactor = "90",
                FactorReason = "Calendario y referencia aprobados"
            };
            Assert.IsTrue(assistant.CanAdopt, assistant.Feedback);
            StringAssert.StartsWith(assistant.Proposed, "810"); // (960 - 60) * 90%
            Assert.IsFalse(host.HasPendingEdits);
            assistant.StoppageBasis = PhysicalStoppageBasis.PerResourceDay;
            StringAssert.StartsWith(assistant.Proposed, "837"); // 30 total hours
            assistant.StoppageBasis = PhysicalStoppageBasis.PerResourcePeriod;
            StringAssert.StartsWith(assistant.Proposed, "862,65"); // 1.5 total hours
            assistant.StoppageBasis = PhysicalStoppageBasis.TotalPeriod;
            assistant.Stoppages = "60";
            StringAssert.StartsWith(assistant.Proposed, "810");
            assistant.StoppageBasis = PhysicalStoppageBasis.PerResourceShift;
            Assert.IsFalse(assistant.CanAdopt);
            Assert.AreEqual("", assistant.Proposed); // no stale result after invalid changes
            assistant.Stoppages = "0,5";
            assistant.OperatingDays = "10";
            StringAssert.StartsWith(assistant.Proposed, "405");
            assistant.Adopt();
            host.SelectedRow!.RealText = "400";
            host.SelectedRow.RealSource = "Partes del período";
            host.Save(true);
            Assert.IsFalse(host.HasPendingEdits, host.Feedback);
            var restored = new PhysicalCapacityAssistantEditor(host);
            Assert.AreEqual("0,5", restored.Stoppages);
            Assert.AreEqual(PhysicalStoppageBasis.PerResourceShift, restored.StoppageBasis);
            Assert.AreEqual("90", restored.NormalFactor);
            Assert.AreEqual("Calendario y referencia aprobados", restored.FactorReason);
            StringAssert.StartsWith(restored.Proposed, "405");
        }
        using var reopened = Service().Open(path);
        var saved = reopened.PhysicalActivity.ReadCurrent(study.CompanyId, study.Id).Single();
        Assert.AreEqual(405m, saved.NormalQuantity);
        Assert.AreEqual(480m, saved.TheoreticalQuantity);
        Assert.AreEqual(450m, saved.PracticalQuantity);
        Assert.AreEqual(400m, saved.RealQuantity);
        StringAssert.Contains(saved.NormalEvidence!.Description, "stopsBasis=PerResourceShift");
    }

    [TestMethod]
    public void T14_DesktopFixtureReopensWithIndependentPeriods()
    {
        var (file, study, node, pool) = Fixture();
        var path = file.Path;
        using (file)
        {
            file.PhysicalActivity.Save(Save(study, node, pool, 0, 700, 610));
            file.Studies.Create(new(study.CompanyId, Id(), "Noviembre", BusinessDate.ParseIso("2026-11-01"),
                BusinessDate.ParseIso("2026-11-30"), study.Purpose, study.ScenarioKind,
                study.SelectedModelVersionId, study.Currency.FunctionalCurrencyCode, "test"));
        }
        using (var reopened = Service().Open(path)) Assert.AreEqual(2, reopened.Studies.ReadStudies().Count);
        if (Environment.GetEnvironmentVariable("VALDRIO_T14_UI_DIR") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            File.Copy(path, Path.Combine(output, "t14-demo.valdrio"), true);
        }
    }
    private static PhysicalActivityEditor Editor(ICompanyFileSession file, StudyDefinition study) =>
        new(study, file.Studies, file.PhysicalActivity, file.Models, file.Runs, file.Read(), true,
            CultureInfo.GetCultureInfo("es"), CultureInfo.GetCultureInfo("es-ES"));

    [TestMethod]
    public void T14_EditedRowShowsUnsavedAndLiveUtilizationAndRestoresSavedStatus()
    {
        var (file, study, node, pool) = Fixture();
        using (file)
        {
            file.PhysicalActivity.Save(Save(study, node, pool, 0, 700, 610));
            var editor = Editor(file, study);
            var row = editor.SelectedRow!;
            var saved = row.Status;
            Assert.IsFalse(editor.CanSaveDraft);
            row.RealText = "350";
            Assert.AreEqual("50 %", row.Utilization);
            Assert.AreEqual("Cambios sin guardar", row.Status);
            Assert.IsTrue(editor.CanSaveDraft);
            row.RealText = "610";
            Assert.AreEqual(saved, row.Status);
            Assert.IsFalse(editor.CanSaveDraft);
            row.RealText = "1.5"; // wrong decimal separator must not silently become 15 in es-ES
            editor.Save(false);
            Assert.AreEqual(610m, file.PhysicalActivity.ReadCurrent(study.CompanyId, study.Id).Single().RealQuantity);
        }
    }

    [TestMethod]
    public void T14_AssistantRejectsOverlongCalendarAndUnjustifiedDeclaration()
    {
        var (file, study, _, _) = Fixture();
        using (file)
        {
            var editor = Editor(file, study);
            var assistant = new PhysicalCapacityAssistantEditor(editor)
            {
                OperatingDays = "32", ShiftsPerDay = "1", HoursPerShift = "8",
                EquivalentResources = "1", Stoppages = "0", NormalFactor = "100", FactorReason = "Registro"
            };
            assistant.Adopt();
            Assert.IsFalse(assistant.Adopted);
            Assert.AreEqual("", editor.SelectedRow!.NormalText);
            assistant.OperatingDays = "22";
            assistant.Preview();
            Assert.IsFalse(editor.HasPendingEdits); // preview/cancel never writes the row
            assistant.Adopt();
            Assert.AreEqual("176", editor.SelectedRow.NormalText);
            Assert.AreEqual("", editor.SelectedRow.RealText);
            var declared = new PhysicalCapacityAssistantEditor(editor)
            { Method = PhysicalCapacityMethod.Declared, DeclaredQuantity = "200", DeclaredSource = "Responsable" };
            declared.Adopt();
            Assert.IsFalse(declared.Adopted);
            Assert.AreEqual("176", editor.SelectedRow.NormalText);
            Assert.ThrowsExactly<DomainRuleException>(() => PhysicalNormalCalculator.Calendar(new(22, 4, 8, 1, 0, 1, "Registro")));
        }
    }

    [TestMethod]
    public void T14_XlsxNumbersUseInvariantStorageAndRejectBooleanAndMissingDates()
    {
        var (file, study, node, pool) = Fixture();
        using (file)
        {
            var model = file.Models.ReadModels().Single(m => m.Structure.Id == study.SelectedModelVersionId);
            var unit = file.Read().Units.Single(u => u.Id == pool.DriverUnitId);
            PhysicalImportLine Map(SourceValueKind kind, string raw, string start = "2026-10-01")
            {
                var values = new[] { node.Code, unit.Code, start, "2026-10-31", raw };
                var cells = values.Select((v, index) => new SourceCell(index + 1,
                    index == 4 ? kind : SourceValueKind.Text, v, v, false)).ToArray();
                var document = new ImportSourceDocument(ImportSourceKind.Xlsx, "physical.xlsx", new string('a', 64), "Sheet1",
                    ["node", "unit", "period_start", "period_end", "real"], [new SourceRow(2, 2, cells)], XlsxDateSystem.Date1900);
                return PhysicalImportMapper.Preview(document, model.Structure, file.Read().Units, study,
                    CultureInfo.GetCultureInfo("es-ES")).Lines.Single();
            }
            Assert.AreEqual(1.5m, Map(SourceValueKind.Number, "1.5").RealQuantity);
            Assert.AreEqual(1.5m, Map(SourceValueKind.Text, "1,5").RealQuantity);
            Assert.AreEqual("Activity.ImportNumber", Map(SourceValueKind.Text, "1.5").ProblemKey);
            Assert.AreEqual("Activity.ImportNumber", Map(SourceValueKind.Boolean, "1").ProblemKey);
            Assert.AreEqual("Activity.ImportNumber", Map(SourceValueKind.FormulaWithoutCachedValue, "").ProblemKey);
            Assert.AreEqual("Activity.ImportPeriod", Map(SourceValueKind.Number, "1.5", "").ProblemKey);
        }
    }

    [TestMethod]
    public void T14_HistoricalExclusionKeepsReasonAndRejectsDifferentTargetDuration()
    {
        var (file, study, node, pool) = Fixture();
        using (file)
        {
            foreach (var month in new[] { "07", "08" })
            {
                var previous = file.Studies.Create(new(study.CompanyId, Id(), month,
                    BusinessDate.ParseIso($"2026-{month}-01"), BusinessDate.ParseIso($"2026-{month}-31"),
                    study.Purpose, study.ScenarioKind, study.SelectedModelVersionId, study.Currency.FunctionalCurrencyCode, "test"));
                file.PhysicalActivity.Save(Save(previous, node, pool, 0, 700, 610));
            }
            var editor = Editor(file, study);
            var assistant = new PhysicalCapacityAssistantEditor(editor) { Method = PhysicalCapacityMethod.Historical };
            foreach (var choice in assistant.Historical) { choice.Reviewed = true; choice.Include = true; }
            assistant.Historical[0].Include = false;
            assistant.Historical[0].ReviewReason = "No representativo";
            assistant.Adopt();
            Assert.IsTrue(assistant.Adopted);
            StringAssert.Contains(editor.SelectedRow!.NormalDescription, "No representativo");
            Assert.AreEqual("610", editor.SelectedRow.NormalText);
            var shorter = file.Studies.UpdateContext(new(study.CompanyId, study.Id, study.Revision, study.Name,
                study.PeriodStart, BusinessDate.ParseIso("2026-10-30"), study.Purpose, study.ScenarioKind,
                study.SelectedModelVersionId, study.Currency.FunctionalCurrencyCode, "test"));
            var invalid = new PhysicalCapacityAssistantEditor(Editor(file, shorter)) { Method = PhysicalCapacityMethod.Historical };
            foreach (var choice in invalid.Historical) { choice.Reviewed = true; choice.Include = true; }
            invalid.Adopt();
            Assert.IsFalse(invalid.Adopted);
        }
    }

    [TestMethod]
    public async Task T14_ImportDoesNotResurrectAnnulledQuantitiesOrOverwriteUnreviewedVersion()
    {
        var (file, study, node, pool) = Fixture();
        using (file)
        {
            file.PhysicalActivity.Save(Save(study, node, pool, 0, 700, 610));
            file.PhysicalActivity.Annul(new(study.CompanyId, study.Id, study.Revision, node.Id, pool.Id, 1, "Wrong log", "test"));
            var path = Path.Combine(directory, "real.csv");
            File.WriteAllText(path, "node;unit;period_start;period_end;real\nMONT;labor_hour;2026-10-01;2026-10-31;620,5\n");
            var editor = new PhysicalImportEditor(Editor(file, study));
            await editor.LoadFile(path); await editor.Preview();
            Assert.AreEqual(1, editor.Lines.Count);
            editor.Lines[0].Replace = true;
            Assert.IsTrue(editor.Apply(), editor.Feedback);
            var saved = file.PhysicalActivity.ReadCurrent(study.CompanyId, study.Id).Single();
            Assert.IsNull(saved.NormalQuantity);
            Assert.IsNull(saved.NormalEvidence);
            Assert.AreEqual(620.5m, saved.RealQuantity);
            await editor.Preview(); editor.Lines[0].Replace = true;
            file.PhysicalActivity.Save(Save(study, node, pool, 3, 700, 650, PhysicalObservationState.Draft));
            Assert.IsFalse(editor.Apply());
            Assert.AreEqual(650m, file.PhysicalActivity.ReadCurrent(study.CompanyId, study.Id).Single().RealQuantity);
        }
    }

    [TestMethod]
    public void T14_AnnulmentPreservesExceptionalEvidence()
    {
        var (file, study, node, pool) = Fixture();
        using (file)
        {
            file.PhysicalActivity.Save(Save(study, node, pool, 0, 0, 20, zero: "Closed", exception: "Emergency"));
            var annulled = file.PhysicalActivity.Annul(new(study.CompanyId, study.Id, study.Revision,
                node.Id, pool.Id, 1, "Wrong period", "test"));
            Assert.AreEqual("Emergency", annulled.ExceptionReason);
            Assert.AreEqual("Wrong period", annulled.CorrectionReason);
        }
    }
}
