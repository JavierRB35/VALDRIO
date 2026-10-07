using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Infrastructure.CompanyFiles;

namespace Valdrio.Tests;

public sealed partial class T06ImportConfirmationTests
{
    private static SourceParseProfileVersion GroupProfile(StudyDefinition study, params ImportRecordTarget[] selected) =>
        new(SourceParseProfileVersion.GroupingSchema, Id(), 1, "Synthetic grouping", ImportSourceKind.Csv,
            null, 1, 2, new(3, description: 2, amount: 1), new("utf-8", ';', '"', CsvBomPolicy.Optional),
            new(",", null, [], ImportNegativeSyntax.LeadingMinus), "yyyy-MM-dd", ImportAmountMode.ClosingBalanceAsStudyAmount,
            ImportSignConvention.SignedAmount, study.PeriodStart, study.PeriodEnd, new CurrencyCode("EUR"), null,
            ImportDuplicatePolicy.AggregateWithProvenance, ImportZeroPolicy.Keep, ImportAccountSelection.All,
            new(ImportProfileApprovalState.ApprovedBusiness, "Synthetic example 280+78.35=358.35"), true,
            grouping: new(selected));

    [TestMethod]
    public async Task GroupingUsesCodeDespiteDifferentDescriptionsAndRetainsEverySourceLink()
    {
        EntityId companyId, studyId; string canonical;
        using (var file = Baseline())
        {
            var study = Study(file); companyId = study.CompanyId; studyId = study.Id;
            var profile = GroupProfile(study, new(2, "6005"), new(3, "6005"), new(4, "ZERO"));
            var source = await Source(profile, "Amount;Description;Code\n280;First item;6005\n78,35;Different item;6005\n0;Zero;ZERO\n9000;Excluded;OTHER");
            var before = Hash(file.Path); var prepared = Prepare(file, study, source, profile);
            Assert.IsTrue(prepared.CanConfirm); Assert.AreEqual(before, Hash(file.Path));
            Assert.AreEqual(358.35m, prepared.Groups!.Single(g => g.Code == "6005").Amount.Amount);
            Assert.AreEqual(3, prepared.Reconciliation.SelectedRows); Assert.AreEqual(1, prepared.Reconciliation.IgnoredRows);
            var command = Command(prepared); var batch = file.Imports.ConfirmImport(command);
            Assert.AreEqual(batch.Id, file.Imports.ConfirmImport(command).Id);
            Assert.AreEqual(2, batch.Resources.Count);
            CollectionAssert.AreEqual(new[] { 2, 3 }, batch.Resources.Single(r => r.Account == "6005").SourceRecords!.ToArray());
            Assert.AreEqual("3", Query(file.Path, "SELECT count(*) FROM ResourceSourceLinks"));
            Assert.AreEqual("4", Query(file.Path, "SELECT count(*) FROM ImportedRows"));
            var photo = file.Studies.Capture(new(companyId, studyId, 1, Id(), "test"));
            Assert.AreEqual(ImportedStudySnapshotInput.GroupedSchema, photo.PayloadSchema);
            Assert.AreEqual(2, photo.Input.Resources.Count); photo.EnsureValid();
            Assert.AreEqual(photo.CanonicalJson, StudySnapshotJson.Serialize(StudySnapshotJson.Read(photo.CanonicalJson)));
            canonical = CanonicalSnapshotJson.Serialize(batch);
        }
        using var reopened = Service().Open(FileOf("current.valdrio"));
        Assert.AreEqual(canonical, CanonicalSnapshotJson.Serialize(reopened.Imports.ReadImports(companyId, studyId).Single()));
        reopened.Studies.ReadSnapshots(companyId, studyId).Single().EnsureValid();
    }

    [TestMethod]
    public async Task ARepeatedCodeCanImportOnlyOneSelectedRecord()
    {
        using var file = Baseline(); var study = Study(file);
        var profile = GroupProfile(study, new ImportRecordTarget(3, "6005"));
        var source = await Source(profile, "Amount;Description;Code\n280;First;6005\n78,35;Second;6005");
        var prepared = Prepare(file, study, source, profile);
        Assert.IsTrue(prepared.CanConfirm); Assert.AreEqual(78.35m, prepared.Groups!.Single().Amount.Amount);
        Assert.IsFalse(prepared.Rows[0].Selected); Assert.IsTrue(prepared.Rows[1].Selected);
        var batch = file.Imports.ConfirmImport(Command(prepared));
        CollectionAssert.AreEqual(new[] { 3 }, batch.Resources.Single().SourceRecords!.ToArray());
        Assert.AreEqual(78.35m, batch.Resources.Single().Functional.Amount);
    }

    [TestMethod]
    public async Task OpposingRowsProduceAnExplicitZeroGroupAndKeepSignedReconciliation()
    {
        using var file = Baseline(); var study = Study(file);
        var profile = GroupProfile(study, new(2, "EXP-01"), new(3, "EXP-01"));
        var source = await Source(profile, "Amount;Description;Code\n280;Expense;EXP-01\n-280;Reversal;EXP-01");
        var prepared = Prepare(file, study, source, profile);
        Assert.IsTrue(prepared.CanConfirm); Assert.AreEqual(0m, prepared.Groups!.Single().Amount.Amount);
        Assert.AreEqual(280m, prepared.Reconciliation.PositiveAmount); Assert.AreEqual(-280m, prepared.Reconciliation.NegativeAmount);
        Assert.AreEqual(0, prepared.Reconciliation.ZeroRows);
        Assert.AreEqual(1, file.Imports.ConfirmImport(Command(prepared)).Resources.Count);
    }

    [TestMethod]
    public async Task UnknownRecordsAndMismatchedCodesCannotBeConfirmed()
    {
        using var file = Baseline(); var study = Study(file);
        foreach (var records in new[] { new[] { new ImportRecordTarget(99, "6005") }, new[] { new ImportRecordTarget(2, "DIFFERENT") } })
        {
            var profile = GroupProfile(study, records);
            var source = await Source(profile, "Amount;Description;Code\n280;First;6005");
            var before = Hash(file.Path); var prepared = Prepare(file, study, source, profile);
            Assert.IsFalse(prepared.CanConfirm);
            Assert.ThrowsExactly<DomainRuleException>(() => file.Imports.ConfirmImport(Command(prepared)));
            Assert.AreEqual(before, Hash(file.Path));
        }
        Assert.ThrowsExactly<DomainRuleException>(() => new ImportGroupingPlan([]));
        Assert.ThrowsExactly<DomainRuleException>(() => new ImportGroupingPlan([new(2, "A"), new(2, "B")]));
    }

    [TestMethod]
    public async Task GroupMagnitudeCannotExceedPrecisionPolicyEvenWhenIndividualRowsFit()
    {
        using var file = Baseline(); var study = Study(file);
        var profile = GroupProfile(study, new(2, "6005"), new(3, "6005"));
        var maximum = file.Read().PrecisionPolicy.MaximumInitialMagnitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var source = await Source(profile, $"Amount;Description;Code\n{maximum};First;6005\n{maximum};Second;6005");
        var prepared = Prepare(file, study, source, profile);
        Assert.IsFalse(prepared.CanConfirm); Assert.AreEqual(0, prepared.Groups!.Count);
        Assert.AreEqual(0, prepared.Reconciliation.InvalidRows); Assert.AreEqual(0, prepared.Reconciliation.IgnoredRows);
        Assert.AreEqual(2, prepared.Reconciliation.SelectedRows);
        Assert.ThrowsExactly<DomainRuleException>(() => file.Imports.ConfirmImport(Command(prepared)));
    }

    [TestMethod]
    public async Task LiveSelectionReconcilesWithoutSavedStudyAndKeepsSourceEvidence()
    {
        using var file = Baseline(); var study = Study(file); var before = Hash(file.Path);
        using var vm = await Vm(file, study, csv: "Amount;Description;Code\n280;First;6005\n78,35;Different;6005\n0;Zero;Z\n-5;Credit;GASTO");
        vm.BindTarget(null, null, false, null);
        Assert.AreEqual(358.35m, vm.SelectionGroups.Single(g => g.Code == "6005").Amount);
        CollectionAssert.AreEqual(new[] { 2, 3 }, vm.SelectionGroups.Single(g => g.Code == "6005").Records.ToArray());
        Assert.AreEqual(new Valdrio.Desktop.ImportSelectionTotals(4, 0, 3, 358.35m, -5m, 353.35m, 0m), vm.SelectionTotals);
        vm.AccountChoices.Single(r => r.RecordNumber == 2).Included = false;
        Assert.AreEqual(new Valdrio.Desktop.ImportSelectionTotals(3, 1, 3, 78.35m, -5m, 73.35m, 280m), vm.SelectionTotals);
        Assert.IsNull(vm.Preparation); Assert.IsFalse(vm.ConfirmCommand.CanExecute(null));
        vm.DeselectAllRecordsCommand.Execute(null);
        Assert.AreEqual(0, vm.SelectionGroups.Count);
        Assert.AreEqual(new Valdrio.Desktop.ImportSelectionTotals(0, 4, 0, 0m, 0m, 0m, 353.35m), vm.SelectionTotals);
        vm.SelectAllRecordsCommand.Execute(null);
        vm.BindTarget(file.Imports, study, true, null);
        vm.SourceCurrency = "EUR"; vm.ApprovalReference = "Synthetic reconciliation"; vm.InterpretationApproved = true;
        await vm.PrepareCommand.ExecuteAsync(null);
        Assert.AreEqual(vm.SelectionTotals!.Net, vm.Preparation!.Reconciliation.NetAmount);
        Assert.AreEqual(vm.SelectionTotals.Groups, vm.Preparation.Groups!.Count);
        Assert.AreEqual(before, Hash(file.Path));
        vm.AmountColumn = 2;
        Assert.IsNull(vm.SelectionTotals); Assert.AreEqual(0, vm.SelectionGroups.Count);
    }

    [TestMethod]
    public async Task LiveSelectionIncludesRowsBeyondPreviewAndKeepsOrdinalCodesAndZeroGroups()
    {
        using var file = Baseline(); var study = Study(file);
        var csv = "Amount;Description;Code\n" + string.Join("\n", Enumerable.Repeat("1;x;00123", 60)) + "\n-60;y;00123\n5;z;123\n-2;q;a\n2;q;A";
        using var vm = await Vm(file, study, csv: csv);
        Assert.AreEqual(64, vm.SelectionTotals!.SelectedRows);
        Assert.AreEqual(4, vm.SelectionGroups.Count);
        Assert.AreEqual(0m, vm.SelectionGroups.Single(g => g.Code == "00123").Amount);
        Assert.AreEqual(61, vm.SelectionGroups.Single(g => g.Code == "00123").Records.Count);
        Assert.AreEqual(5m, vm.SelectionTotals.Net);
    }

    [TestMethod]
    public async Task LiveSelectionOverflowIsVisibleAndRecoversAfterExclusion()
    {
        using var file = Baseline(); var study = Study(file);
        var maximum = decimal.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
        using var vm = await Vm(file, study, csv: $"Amount;Description;Code\n{maximum};x;A\n{maximum};y;A");
        Assert.IsTrue(vm.SelectionOverflow); Assert.IsNull(vm.SelectionTotals);
        Assert.AreEqual(0, vm.SelectionGroups.Count);
        vm.AccountChoices[1].Included = false;
        Assert.IsFalse(vm.SelectionOverflow); Assert.AreEqual(decimal.MaxValue, vm.SelectionTotals!.Net);
    }

    [TestMethod]
    public async Task RowSelectionSupportsAllNoneSomeAndAllExceptOneDuplicate()
    {
        using var file = Baseline(); var study = Study(file);
        using var vm = await Vm(file, study, csv: "Amount;Description;Code\n280;First;6005\n78,35;Second;6005\n0;Zero;Z\n-5;Credit;GASTO");
        Assert.IsTrue(vm.IsReady); Assert.AreEqual(4, vm.AccountChoices.Count);
        Assert.AreEqual(358.35m, vm.Preparation!.Groups!.Single(g => g.Code == "6005").Amount.Amount);
        vm.DeselectAllRecordsCommand.Execute(null);
        Assert.IsTrue(vm.AccountChoices.All(r => !r.Included)); Assert.IsFalse(vm.PrepareCommand.CanExecute(null));
        vm.AccountChoices.Single(r => r.RecordNumber == 3).Included = true;
        vm.InterpretationApproved = true; await vm.PrepareCommand.ExecuteAsync(null);
        Assert.AreEqual(78.35m, vm.Preparation!.Groups!.Single().Amount.Amount);
        vm.SelectAllRecordsCommand.Execute(null);
        Assert.IsTrue(vm.AccountChoices.All(r => r.Included)); Assert.IsNull(vm.Preparation); Assert.IsFalse(vm.InterpretationApproved);
        vm.AccountChoices.Single(r => r.RecordNumber == 2).Included = false;
        Assert.AreEqual(3, vm.AccountChoices.Count(r => r.Included));
        vm.InterpretationApproved = true; await vm.PrepareCommand.ExecuteAsync(null);
        Assert.AreEqual(73.35m, vm.Preparation!.Reconciliation.NetAmount);
        Assert.AreEqual(78.35m, vm.Preparation.Groups!.Single(g => g.Code == "6005").Amount.Amount);
        await vm.ConfirmCommand.ExecuteAsync(null); Assert.AreEqual(3, vm.ConfirmedImport!.Resources.Count);
    }

    [TestMethod]
    public void V5MigrationPreservesGenuineV4LedgerAndSnapshotsByteForByte()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "t06-v4.json")));
        var source = FileOf("genuine-v4.valdrio");
        using (var gzip = new GZipStream(new MemoryStream(Convert.FromBase64String(document.RootElement.GetProperty("GzipBase64").GetString()!)), CompressionMode.Decompress))
        using (var output = File.Create(source)) gzip.CopyTo(output);
        var before = Hash(source); Assert.AreEqual(document.RootElement.GetProperty("Sha256").GetString(), before);
        var batchJson = Query(source, "SELECT CanonicalJson FROM ImportBatches");
        var photos = Query(source, "SELECT group_concat(CanonicalJson) FROM StudySnapshots ORDER BY Id");
        var links = Query(source, "SELECT group_concat(ResourceId||RecordNumber) FROM ResourceSourceLinks ORDER BY ResourceId");
        Assert.AreEqual("FILE_UPGRADE_REQUIRED", Assert.ThrowsExactly<CompanyFileException>(() => Service().Open(source)).Diagnostic.Code);
        using var file = Service().Upgrade(source, FileOf("upgraded-v5.valdrio"), "test");
        Assert.AreEqual(5, file.Read().SchemaVersion); Assert.AreEqual(before, Hash(source));
        Assert.AreEqual(batchJson, Query(file.Path, "SELECT CanonicalJson FROM ImportBatches"));
        Assert.AreEqual(photos, Query(file.Path, "SELECT group_concat(CanonicalJson) FROM StudySnapshots ORDER BY Id"));
        Assert.AreEqual(links, Query(file.Path, "SELECT group_concat(ResourceId||RecordNumber) FROM ResourceSourceLinks ORDER BY ResourceId"));
        using var connection = new SqliteConnection("Data Source=" + file.Path + ";Pooling=False"); connection.Open();
        using var model = new GroupedImportDbContext(connection);
        Assert.IsFalse(model.Database.HasPendingModelChanges());
        Assert.AreEqual("ok", Query(file.Path, "PRAGMA integrity_check"));
        Assert.AreEqual("", Query(file.Path, "PRAGMA foreign_key_check"));
        foreach (var study in file.Studies.ReadStudies())
        {
            foreach (var snapshot in file.Studies.ReadSnapshots(study.CompanyId, study.Id)) snapshot.EnsureValid();
            _ = file.Imports.ReadImports(study.CompanyId, study.Id);
        }
    }

    [TestMethod]
    [DataRow(CompanyWriteCheckpoint.AfterFirstWrite, false)]
    [DataRow(CompanyWriteCheckpoint.BeforeCommit, false)]
    [DataRow(CompanyWriteCheckpoint.AfterFirstWrite, true)]
    [DataRow(CompanyWriteCheckpoint.BeforeCommit, true)]
    public async Task GroupedWriteFaultOrCancellationRollsBackEveryContribution(CompanyWriteCheckpoint stage, bool cancel)
    {
        PreparedImport prepared; string before;
        using (var file = Baseline())
        {
            var study = Study(file); var profile = GroupProfile(study, new(2, "6005"), new(3, "6005"));
            prepared = Prepare(file, study, await Source(profile, "Amount;Description;Code\n280;First;6005\n78,35;Second;6005"), profile);
            before = Hash(file.Path);
        }
        using var token = new CancellationTokenSource();
        using (var failing = Service(s => { if (s == stage) { if (cancel) token.Cancel(); else throw new IOException("Injected grouped write failure"); } }).Open(FileOf("current.valdrio")))
        {
            if (cancel) Assert.ThrowsExactly<OperationCanceledException>(() => failing.Imports.ConfirmImport(Command(prepared), token.Token));
            else Assert.ThrowsExactly<CompanyFileException>(() => failing.Imports.ConfirmImport(Command(prepared)));
        }
        using var read = Service().Open(FileOf("current.valdrio"));
        Assert.AreEqual(before, Hash(read.Path));
        foreach (var table in new[] { "ImportBatches", "ImportedRows", "Resources", "ResourceSourceLinks" })
            Assert.AreEqual("0", Query(read.Path, "SELECT count(*) FROM " + table));
    }

    [TestMethod]
    public async Task MissingGroupedContributionIsDetectedOnReopen()
    {
        using (var file = Baseline())
        {
            var study = Study(file); var profile = GroupProfile(study, new(2, "6005"), new(3, "6005"));
            var prepared = Prepare(file, study, await Source(profile, "Amount;Description;Code\n280;First;6005\n78,35;Second;6005"), profile);
            file.Imports.ConfirmImport(Command(prepared));
            Assert.ThrowsExactly<SqliteException>(() => Query(file.Path, "DELETE FROM ResourceSourceLinks WHERE RecordNumber=3"));
        }
        Query(FileOf("current.valdrio"), "DROP TRIGGER ResourceSourceLinksImmutableDelete; DELETE FROM ResourceSourceLinks WHERE RecordNumber=3;");
        Assert.AreEqual("FILE_INVALID_DATA", Assert.ThrowsExactly<CompanyFileException>(() => Service().Open(FileOf("current.valdrio"))).Diagnostic.Code);
    }
}
