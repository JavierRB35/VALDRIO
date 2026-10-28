using Valdrio.Application;
using Valdrio.Desktop;
using Valdrio.Domain;

namespace Valdrio.Tests;

public sealed partial class T14PhysicalActivityTests
{
    [TestMethod]
    public void Guided_PreviousCalendarSuggestsRatesButNeverCopiesDaysOrActual()
    {
        var (file, study, node, pool) = Fixture();
        using (file)
        {
            var previous = file.Studies.Create(new(study.CompanyId, Id(), "Previous",
                BusinessDate.ParseIso("2026-09-01"), BusinessDate.ParseIso("2026-09-30"), study.Purpose,
                study.ScenarioKind, study.SelectedModelVersionId, study.Currency.FunctionalCurrencyCode, "test"));
            file.PhysicalActivity.Save(Save(previous, node, pool, 0, 415.8m, 400) with
            {
                NormalEvidence = new(PhysicalEvidenceQuality.Calculated, "Calendar", "calendar: days=22; shifts=1; hours=8; resources=3; stops=66; factor=0.9; reason=; stopsBasis=PerResourceShift; stopsEntered=1; stopsMultiplier=66")
            });
            var assistant = new PhysicalCapacityAssistantEditor(Editor(file, study));
            Assert.AreEqual("", assistant.OperatingDays);
            Assert.AreEqual("1", assistant.ShiftsPerDay);
            Assert.AreEqual("8", assistant.HoursPerShift);
            Assert.AreEqual("90,0", assistant.NormalFactor);
            Assert.AreEqual("", assistant.RealValue);
            Assert.IsFalse(assistant.CanAdopt);
            Assert.IsFalse(string.IsNullOrWhiteSpace(assistant.InferenceSummary));
        }
    }

    [TestMethod]
    public void Guided_DraftCanSaveWithoutACompletedCalendarAndClosedStudyCannotSave()
    {
        var (file, study, _, _) = Fixture();
        using (file)
        {
            var editor = new PhysicalCapacityAssistantEditor(Editor(file, study));
            editor.RealValue = "3";
            editor.Save(false);
            var saved = file.PhysicalActivity.ReadCurrent(study.CompanyId, study.Id).Single();
            Assert.IsNull(saved.NormalQuantity);
            Assert.AreEqual(3m, saved.RealQuantity);
            file.Studies.ChangeState(new(study.CompanyId, study.Id, study.Revision, StudyState.Closed, "test"));
            var closed = file.Studies.ReadStudies().Single();
            var readOnly = new PhysicalCapacityAssistantEditor(Editor(file, closed));
            Assert.IsFalse(readOnly.CanConfirm);
            Assert.IsFalse(readOnly.CanSave);
            readOnly.Save(true);
            Assert.AreEqual(saved.Version, file.PhysicalActivity.ReadCurrent(study.CompanyId, study.Id).Single().Version);
        }
    }

    [TestMethod]
    public void Guided_ConfirmationCorrectionAndReopenDoNotRequireNarratives()
    {
        var (file, study, node, pool) = Fixture();
        var path = file.Path;
        using (file)
        {
            var first = file.PhysicalActivity.Save(Save(study, node, pool, 0, 0, 20) with
                { NormalEvidence = null, RealEvidence = null });
            Assert.IsNull(first.NormalZeroReason);
            Assert.IsNull(first.ExceptionReason);
            Assert.IsNull(first.UtilizationPercent);
            Assert.AreEqual(PhysicalEvidenceQuality.Declared, first.RealEvidence!.Quality);
            Assert.IsFalse(string.IsNullOrWhiteSpace(first.RealEvidence.Source));
            var second = file.PhysicalActivity.Save(Save(study, node, pool, 1, 100, 120));
            Assert.IsNull(second.CorrectionReason);
            Assert.AreEqual(120m, second.UtilizationPercent);
            Assert.AreEqual(study.Revision, file.Studies.ReadStudies().Single().Revision);
        }
        using var reopened = Service().Open(path);
        Assert.AreEqual(2, reopened.PhysicalActivity.ReadHistory(study.CompanyId, study.Id, pool.Id).Count);
        Assert.AreEqual(120m, reopened.PhysicalActivity.ReadCurrent(study.CompanyId, study.Id).Single().RealQuantity);
    }

    [TestMethod]
    public void Guided_CalendarSaveIsSingleActionAndPreservesPrecisionAndUnknownReal()
    {
        var (file, study, _, _) = Fixture();
        using (file)
        {
            var host = Editor(file, study);
            var editor = new PhysicalCapacityAssistantEditor(host)
            {
                Method = PhysicalCapacityMethod.Calendar, OperatingDays = "22", ShiftsPerDay = "1",
                HoursPerShift = "8", EquivalentResources = "3", Stoppages = "1", NormalFactor = "90",
                UseProposal = true
            };
            Assert.IsTrue(editor.CanAdopt, editor.Feedback);
            Assert.AreEqual("415,8", editor.NormalValue);
            Assert.IsFalse(editor.CanConfirm);
            editor.Save(false);
            var draft = file.PhysicalActivity.ReadCurrent(study.CompanyId, study.Id).Single();
            Assert.AreEqual(415.8m, draft.NormalQuantity);
            Assert.IsNull(draft.RealQuantity);
            Assert.AreEqual(PhysicalObservationState.Draft, draft.State);
            editor.RealValue = "400";
            Assert.IsTrue(editor.CanConfirm);
            editor.Save(true);
            Assert.AreEqual(PhysicalObservationState.Confirmed, file.PhysicalActivity.ReadCurrent(study.CompanyId, study.Id).Single().State);
            Assert.IsFalse(editor.HasPendingProposal);
        }
    }

    [TestMethod]
    public void Guided_ManualOverrideNeverGetsReplacedByRecalculationAndShowsConditionalNotes()
    {
        var (file, study, _, _) = Fixture();
        using (file)
        {
            var host = Editor(file, study);
            var editor = new PhysicalCapacityAssistantEditor(host)
            { OperatingDays = "22", ShiftsPerDay = "1", HoursPerShift = "8", EquivalentResources = "3", Stoppages = "1", NormalFactor = "90" };
            editor.NormalValue = "0";
            editor.RealValue = "10";
            Assert.IsTrue(editor.ShowZeroNote);
            Assert.IsTrue(editor.ShowExceptionNote);
            Assert.IsFalse(editor.DetailsExpanded);
            editor.NormalFactor = "80";
            Assert.AreEqual("0", editor.NormalValue);
            Assert.IsFalse(editor.UseProposal);
            editor.Save(true);
            var saved = file.PhysicalActivity.ReadCurrent(study.CompanyId, study.Id).Single();
            Assert.AreEqual(0m, saved.NormalQuantity);
            Assert.AreEqual(PhysicalEvidenceQuality.Declared, saved.NormalEvidence!.Quality);
            editor.NormalValue = "-1";
            Assert.IsFalse(editor.CanConfirm);
            Assert.IsFalse(string.IsNullOrWhiteSpace(editor.NormalError));
        }
    }

    [TestMethod]
    public void Guided_InferenceRejectsForeignScopeFutureAndDuplicatePeriods()
    {
        var (file, study, node, pool) = Fixture();
        using (file)
        {
            var observation = file.PhysicalActivity.Save(Save(study, node, pool, 0, 100, 80));
            var context = new PhysicalSuggestionContext(study.CompanyId, study.SelectedModelVersionId,
                node.Id, pool.Id, observation.UnitId, BusinessDate.ParseIso("2026-12-01"), BusinessDate.ParseIso("2026-12-31"));
            var otherStudy = observation with { StudyId = Id() };
            var wrongUnit = observation with { PoolId = Id() };
            var annulled = observation with { Version = 2, State = PhysicalObservationState.Annulled };
            var suggestions = PhysicalActivityInference.Suggest(context, [observation, otherStudy, wrongUnit]);
            Assert.AreEqual(1, suggestions.ComparableHistory.Count);
            Assert.IsEmpty(PhysicalActivityInference.Suggest(context, [observation, annulled]).ComparableHistory);
            Assert.IsEmpty(PhysicalActivityInference.Suggest(context with { Start = observation.PeriodStart, End = observation.PeriodEnd }, [observation]).ComparableHistory);
        }
    }

    [TestMethod]
    public void Guided_CalendarSegmentsSumOnceAndRejectOverlap()
    {
        var first = new PhysicalCalendarSegment(BusinessDate.ParseIso("2026-10-01"), BusinessDate.ParseIso("2026-10-15"), new(10, 1, 8, 2, 0, 1, null));
        var second = new PhysicalCalendarSegment(BusinessDate.ParseIso("2026-10-16"), BusinessDate.ParseIso("2026-10-31"), new(12, 2, 8, 3, 6, .9m, null));
        var sum = PhysicalActivityInference.CalendarSegments([first, second]);
        Assert.AreEqual(736m, sum.Theoretical);
        Assert.AreEqual(730m, sum.Practical);
        Assert.AreEqual(673m, sum.Normal);
        Assert.ThrowsExactly<DomainRuleException>(() => PhysicalActivityInference.CalendarSegments([first, first]));
        Assert.IsNull(PhysicalActivityInference.ReadCalendar(null));
        Assert.IsNull(PhysicalActivityInference.ReadCalendar("manual"));
    }
}
