using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Tests;
public sealed partial class T06ImportConfirmationTests
{
    [TestMethod]
    public async Task T07_MixedCurrencyAccountRetainsOriginalsAndEveryFxInSnapshot()
    {
        StudySnapshotReceipt photo; EntityId companyId, studyId;
        using (var file = Baseline())
        {
            var study = Study(file); companyId = study.CompanyId; studyId = study.Id; var profile = InternationalProfile(study);
            file.Imports.ConfirmImport(Command(Prepare(file, study, await Source(profile, InternationalCsv), profile)));
            var w = file.Resources.ReadResources(study.CompanyId, study.Id);
            Assert.AreEqual(3, w.Resources.Single().Sources.Count);
            Assert.AreEqual(101m, w.Preparation.Coverage.Single().Effective.Amount);
            var original = ResourceWorkspaceRules.OriginalTotals(w.Resources.Single(), w.Precision);
            Assert.AreEqual(90m, original.Single(m => m.Currency.Value == "USD").Amount);
            Assert.AreEqual(20m, original.Single(m => m.Currency.Value == "EUR").Amount);
            file.Resources.ChangeResources(Mutation(w, ResourceMutationKind.EditResource, w.Resources.Single().Id, [Pending(101)]));
            photo = file.Studies.Capture(new(study.CompanyId, study.Id, 2, Id(), "test")); photo.EnsureValid();
            Assert.AreEqual(3, photo.Input.AppliedFx.Count);
        }
        using var reopened = Service().Open(FileOf("current.valdrio"));
        Assert.AreEqual(photo.CanonicalJson, reopened.Studies.ReadSnapshots(companyId, studyId).Single().CanonicalJson);
    }

    [TestMethod]
    public async Task T07_CumulativeAccountPreservesEarlierNegativeSourceAndFrozenRate()
    {
        using var file = Baseline(); var study = Study(file);
        var priorStudy = new StudyDefinition(study.Id, study.CompanyId, study.Name, BusinessDate.ParseIso("2026-01-01"),
            BusinessDate.ParseIso("2026-09-30"), study.Purpose, study.ScenarioKind, null, study.SelectedModelVersionId,
            study.Currency, study.CurrencyValidity, study.Revision, study.State, null, null, study.CreatedAtUtc);
        var rows = new[] { new ImportRecordTarget(2, "00123") };
        var prior = InternationalProfile(priorStudy, FxPolicy(selections: [new(2, new("usd-eur"))]), records: rows,
            mode: ImportAmountMode.ClosingBalanceAsStudyAmount);
        var priorSource = await Source(prior, "Amount;Description;Account;Currency;Date\n100;prior;00123;USD;2026-09-30");
        var comparison = new ImportComparison(priorSource, prior, priorStudy.PeriodStart, BusinessDate.ParseIso("2026-12-31"), "Same synthetic scope");
        var profile = InternationalProfile(study, FxPolicy(selections: [new(2, new("usd-eur"))]), comparison: comparison,
            records: rows, mode: ImportAmountMode.CumulativeExpenseDifference);
        file.Imports.ConfirmImport(Command(Prepare(file, study, await Source(profile, "Amount;Description;Account;Currency;Date\n160;current;00123;USD;2026-10-31"), profile)));
        var w = file.Resources.ReadResources(study.CompanyId, study.Id);
        Assert.AreEqual(2, w.Resources.Single().Sources.Count);
        Assert.AreEqual(54m, w.Preparation.Coverage.Single().Effective.Amount);
        var earlier = w.Resources.Single().Sources.Single(s => s.Conversion.OriginalMoney.Amount < 0);
        Assert.AreEqual(-100m, earlier.Conversion.OriginalMoney.Amount);
        Assert.AreEqual(-90m, earlier.Conversion.ConvertedOriginalMoney.Amount);
        file.Resources.ChangeResources(Mutation(w, ResourceMutationKind.EditResource, w.Resources.Single().Id, [Pending(54)]));
        var photo = file.Studies.Capture(new(study.CompanyId, study.Id, 2, Id(), "test")); photo.EnsureValid();
        Assert.AreEqual(2, photo.Input.AppliedFx.Count);
    }
}
