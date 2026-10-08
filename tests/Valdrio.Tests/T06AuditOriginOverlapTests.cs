using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Tests;

public sealed partial class T06ImportConfirmationTests
{
    private const string OverlapCsv = "Amount;Description;Code\n280;First;6005\n78,35;Second;6005";

    [TestMethod]
    public async Task AppendingAnOverlappingGroupingSelectionCannotCountTheFirstRowTwice()
    {
        using var file = Baseline(); var study = Study(file);
        var first = GroupProfile(study, new ImportRecordTarget(2, "6005"));
        var source = await Source(first, OverlapCsv);
        var saved = file.Imports.ConfirmImport(Command(Prepare(file, study, source, first)));
        var next = GroupProfile(saved.ConfirmedStudy, new ImportRecordTarget(2, "6005"), new ImportRecordTarget(3, "6005"));
        var prepared = Prepare(file, saved.ConfirmedStudy, source, next);
        var before = Hash(file.Path);
        var error = Assert.ThrowsExactly<DomainRuleException>(() =>
        {
            file.Imports.ConfirmImport(Command(prepared));
            var total = file.Resources.ReadResources(study.CompanyId, study.Id).Preparation.Coverage.Single().Effective.Amount;
            Console.WriteLine("Observed total after overlapping append: " + total);
        });
        Assert.AreEqual("IMP_SOURCE_ROWS_REUSED", error.Diagnostic.Code);
        Assert.AreEqual(before, Hash(file.Path));
    }

    [TestMethod]
    public async Task DisjointSelectionsFromOneFileStillAppendAndReopenAs358Point35()
    {
        EntityId companyId, studyId;
        using (var file = Baseline())
        {
            var study = Study(file); companyId = study.CompanyId; studyId = study.Id;
            var first = GroupProfile(study, new ImportRecordTarget(2, "6005")); var source = await Source(first, OverlapCsv);
            var saved = file.Imports.ConfirmImport(Command(Prepare(file, study, source, first)));
            var next = GroupProfile(saved.ConfirmedStudy, new ImportRecordTarget(3, "6005"));
            file.Imports.ConfirmImport(Command(Prepare(file, saved.ConfirmedStudy, source, next)));
            Assert.AreEqual(358.35m, file.Resources.ReadResources(companyId, studyId).Preparation.Coverage.Single().Effective.Amount);
        }
        using var reopened = Service().Open(FileOf("current.valdrio"));
        Assert.AreEqual(358.35m, reopened.Resources.ReadResources(companyId, studyId).Preparation.Coverage.Single().Effective.Amount);
    }

    [TestMethod]
    public async Task IdentifiedReplacementCanExpandASelectionWithoutCountingTheRetiredBatch()
    {
        using var file = Baseline(); var study = Study(file);
        var first = GroupProfile(study, new ImportRecordTarget(2, "6005")); var source = await Source(first, OverlapCsv);
        var saved = file.Imports.ConfirmImport(Command(Prepare(file, study, source, first)));
        var next = GroupProfile(saved.ConfirmedStudy, new ImportRecordTarget(2, "6005"), new ImportRecordTarget(3, "6005"));
        var command = Command(Prepare(file, saved.ConfirmedStudy, source, next));
        var replacement = new ConfirmImport(command.CompanyId, command.StudyId, command.ExpectedRevision, command.RequestId,
            command.PreparedFingerprint, command.Prepared, ImportConfirmMode.ReplaceIdentifiedBatch, command.Actor, saved.Id);
        file.Imports.ConfirmImport(replacement);
        Assert.AreEqual(358.35m, file.Resources.ReadResources(study.CompanyId, study.Id).Preparation.Coverage.Single().Effective.Amount);
        Assert.AreEqual(2, file.Imports.ReadImports(study.CompanyId, study.Id).Count);
    }

    [TestMethod]
    public async Task DifferentFinancialColumnsOnTheSameRowRemainDistinctSources()
    {
        using var file = Baseline(); var study = Study(file);
        var first = GroupProfile(study, new ImportRecordTarget(2, "6005"));
        var source = await Source(first, "Amount;Description;Code;SecondAmount\n280;Two source values;6005;78,35");
        var saved = file.Imports.ConfirmImport(Command(Prepare(file, study, source, first)));
        var p = GroupProfile(saved.ConfirmedStudy, new ImportRecordTarget(2, "6005"));
        var next = new SourceParseProfileVersion(p.SchemaVersion, Id(), p.Version, p.Name, p.SourceKind,
            p.WorksheetName, p.HeaderRow, p.FirstDataRow, new(3, description: 2, amount: 4), p.Csv,
            p.NumberFormat, p.DatePattern, p.AmountMode, p.SignConvention, p.PeriodStart, p.PeriodEnd,
            p.FixedCurrency, p.CurrencyColumn, p.DuplicatePolicy, p.ZeroPolicy, p.AccountSelection,
            p.Approval, p.SignConventionConfirmed, grouping: p.Grouping);
        file.Imports.ConfirmImport(Command(Prepare(file, saved.ConfirmedStudy, source, next)));
        Assert.AreEqual(358.35m, file.Resources.ReadResources(study.CompanyId, study.Id).Preparation.Coverage.Single().Effective.Amount);
    }

    [TestMethod]
    public async Task DifferentFxQuoteDoesNotMakeTheSameSourceRowsNew()
    {
        using var file = Baseline(); var study = Study(file);
        var first = InternationalProfile(study); var source = await Source(first, InternationalCsv);
        var saved = file.Imports.ConfirmImport(Command(Prepare(file, study, source, first)));
        var next = InternationalProfile(saved.ConfirmedStudy, FxPolicy(.95m));
        var prepared = Prepare(file, saved.ConfirmedStudy, source, next); var before = Hash(file.Path);
        var error = Assert.ThrowsExactly<DomainRuleException>(() => file.Imports.ConfirmImport(Command(prepared)));
        Assert.AreEqual("IMP_SOURCE_ROWS_REUSED", error.Diagnostic.Code);
        Assert.AreEqual(before, Hash(file.Path));
        Assert.AreEqual(101m, file.Resources.ReadResources(study.CompanyId, study.Id).Preparation.Coverage.Single().Effective.Amount);
    }
}
