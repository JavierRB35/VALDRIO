using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Tests;

public sealed partial class T06ImportConfirmationTests
{
    private static SourceParseProfileVersion DescriptionProfile(StudyDefinition study, bool mapped = true, ImportGroupingPlan? grouping = null) =>
        new(grouping is null ? SourceParseProfileVersion.CurrentSchema : SourceParseProfileVersion.GroupingSchema,
            Id(), 1, "Explicit description source", ImportSourceKind.Csv, null, 1, 2,
            new(3, description: mapped ? 2 : null, amount: 1), new("utf-8", ';', '"', CsvBomPolicy.Optional),
            new(",", null, [], ImportNegativeSyntax.LeadingMinus), "yyyy-MM-dd", ImportAmountMode.PeriodAmount,
            ImportSignConvention.SignedAmount, study.PeriodStart, study.PeriodEnd, new("EUR"), null,
            grouping is null ? ImportDuplicatePolicy.Reject : ImportDuplicatePolicy.AggregateWithProvenance,
            ImportZeroPolicy.Keep, ImportAccountSelection.All, new(ImportProfileApprovalState.ApprovedBusiness, "00123=160"), true,
            grouping: grouping);

    [TestMethod]
    public async Task T08DescriptionSelectionSurvivesReopenWithoutSourceAndKeepsIdentity()
    {
        EntityId studyId;
        using (var file = Baseline())
        {
            var study = Study(file); studyId = study.Id;
            var profile = DescriptionProfile(study);
            var source = await Source(profile, "Amount;Name;Account\n160;Material de oficina;00123\n5;Otra cuenta;123\n0;;ZERO");
            var mapped = ImportColumnSelection.Validate(source, new(profile.SourceKind, null, 1, 2, profile.Csv),
                profile.Columns, profile.NumberFormat, XlsxNumericAccountPolicy.Reject);
            Assert.AreEqual("Material de oficina", mapped.Rows[0].Description);
            Assert.AreEqual("00123", mapped.Rows[0].Account);
            file.Imports.ConfirmImport(Command(Prepare(file, study, source, profile)));
        }
        File.Delete(FileOf("source.csv"));
        using var reopened = Service().Open(FileOf("current.valdrio"));
        var workspace = reopened.Resources.ReadResources(reopened.Read().Id, studyId);
        Assert.AreEqual(3, workspace.Origins.Count);
        var origin = workspace.Origins.Single(o => o.Code == "00123");
        Assert.AreEqual("Material de oficina", origin.Descriptions.Single());
        Assert.AreEqual(2, origin.Rows.Single().DescriptionColumn);
        Assert.IsFalse(string.IsNullOrWhiteSpace(origin.Rows.Single().FileHash));
        Assert.AreEqual(160m, workspace.Preparation.Coverage.Single(c => c.ResourceId == origin.Id).Effective.Amount);
        Assert.AreEqual(0, workspace.Origins.Single(o => o.Code == "ZERO").Descriptions.Count);
    }

    [TestMethod]
    public async Task T08DescriptionsKeepConflictingProvenanceWithoutSplittingAccountsAndReplacementRetiresNames()
    {
        using var file = Baseline();
        var study = Study(file);
        var profile = DescriptionProfile(study, grouping: new([new(2, "00123"), new(3, "00123")]));
        var first = file.Imports.ConfirmImport(Command(Prepare(file, study,
            await Source(profile, "Amount;Name;Account\n100;Material de oficina;00123\n60;Papelería;00123"), profile)));
        var workspace = file.Resources.ReadResources(study.CompanyId, study.Id);
        Assert.AreEqual(1, workspace.Resources.Count);
        Assert.IsTrue(workspace.Origins.Single().HasDescriptionConflict);
        Assert.AreEqual(2, workspace.Origins.Single().Descriptions.Count);
        Assert.AreEqual(160m, workspace.Preparation.Coverage.Single().Effective.Amount);
        var nextProfile = DescriptionProfile(first.ConfirmedStudy);
        var replacement = Prepare(file, first.ConfirmedStudy, await Source(nextProfile,
            "Amount;Name;Account\n160;Nombre revisado;00123"), nextProfile);
        file.Imports.ConfirmImport(new(study.CompanyId, study.Id, first.ConfirmedStudy.Revision, Id(), replacement.Fingerprint,
            replacement, ImportConfirmMode.ReplaceIdentifiedBatch, "test", first.Id));
        Assert.AreEqual("Nombre revisado", file.Resources.ReadResources(study.CompanyId, study.Id).Origins.Single().Descriptions.Single());
        Assert.AreEqual(2, file.Imports.ReadImports(study.CompanyId, study.Id).Count);
    }

    [TestMethod]
    public async Task T08UnmappedDescriptionsAreNeverInferredFromHeadersOrIgnoredCells()
    {
        using var file = Baseline(); var study = Study(file); var profile = DescriptionProfile(study, mapped: false);
        file.Imports.ConfirmImport(Command(Prepare(file, study, await Source(profile,
            "Amount;Description;Account\n160;Do not infer this name;00123"), profile)));
        var origin = file.Resources.ReadResources(study.CompanyId, study.Id).Origins.Single();
        Assert.AreEqual(0, origin.Descriptions.Count);
        Assert.IsNull(origin.Rows.Single().DescriptionColumn);
        Assert.AreEqual("00123", origin.Code);
    }
}
