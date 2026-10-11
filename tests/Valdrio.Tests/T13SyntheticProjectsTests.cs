using System.Globalization;
using System.Text.Json;
using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Tests;

public sealed partial class T06ImportConfirmationTests
{
    // T13: creates actual SQLite .valdrio files using the product's normal
    // import, model, allocation, publication and snapshot services.
    // The source fixtures and independent mathematical oracle are in
    // /Synthetic Projects/. No mocked ledger or invented posted facts.
    [TestMethod]
    [DataRow("Service Company")]
    [DataRow("Industrial Company")]
    public async Task T13_SyntheticCompanyRunsEndToEndAndReopens(string folder)
    {
        var basePath = Path.Combine(AppContext.BaseDirectory, "Synthetic Projects", folder);
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(basePath, "scenario.json")));
        var caseData = document.RootElement;
        Assert.AreEqual("T13/SyntheticProject/v1", caseData.GetProperty("schema").GetString());

        string Read(JsonElement root, string name) => root.GetProperty(name).GetString()!;
        decimal Number(JsonElement root, string name) =>
            decimal.Parse(Read(root, name), CultureInfo.InvariantCulture);
        var companyPath = FileOf(Read(caseData, "id").ToLowerInvariant() + ".valdrio");
        var companyId = default(EntityId);
        var studyId = default(EntityId);
        string terminalFacts = "";
        string frozenSnapshot = "";

        using (var file = Service().Create(new(companyPath, Read(caseData, "companyName"),
            new CurrencyCode("EUR"), PrecisionPolicy.Pdec1Proposed)))
        {
            var company = file.Read();
            companyId = company.Id;
            var hours = new UnitDefinition(new UnitId(Id()), "hour", "time", "h", 12, false);
            var lots = new UnitDefinition(new UnitId(Id()), "lot", "batch", "lot", 0, true);
            file.AddUnits([hours, lots], company.Revision, "T13");
            var unitIds = new Dictionary<string, UnitId>(StringComparer.Ordinal)
            {
                ["hour"] = hours.Id, ["lot"] = lots.Id
            };

            var centerIds = new Dictionary<string, EntityId>(StringComparer.Ordinal);
            foreach (var center in caseData.GetProperty("centers").EnumerateArray())
            {
                var code = Read(center, "code");
                centerIds.Add(code, file.Models.CreateCenter(
                    new(companyId, code, Read(center, "name"), "T13")).Id);
            }

            var draft = file.Models.CreateDraft(new(companyId, Read(caseData, "id"), "T13"));
            var model = draft.Structure;
            var nodeIds = new Dictionary<string, EntityId>(StringComparer.Ordinal);
            var nodes = new List<CalculationNodeDefinition>();
            var pools = new List<CostPoolDefinition>();
            foreach (var item in caseData.GetProperty("nodes").EnumerateArray())
            {
                var code = Read(item, "code");
                var role = Enum.Parse<NodeRole>(Read(item, "role"), false);
                var unit = unitIds[Read(item, "unit")];
                var node = new CalculationNodeDefinition(Id(), model.Id, centerIds[Read(item, "center")],
                    code, role, ["operations"], true);
                nodes.Add(node);
                nodeIds.Add(code, node.Id);
                pools.Add(new CostPoolDefinition(Id(), model.Id, node.Id, unit,
                    code + " cost pool", CostLayer.Unclassified, ModelPolicyReference.PendingCapacity));
            }
            var structure = new ModelStructure(model.Id, model.CompanyId, model.FamilyId,
                model.VersionNumber, model.ParentVersionId, model.Description,
                model.PrecisionPolicyVersion, model.MethodPolicy, nodes, pools);
            var saved = file.Models.SaveDraft(new(structure, draft.Revision, "T13"));
            Assert.AreEqual(0, file.Models.ValidateModel(companyId, structure.Id).Count,
                "Synthetic model must be valid before publication");
            var published = file.Models.Publish(new(companyId, structure.Id, saved.Revision, "T13"));
            var study = file.Studies.Create(new(companyId, Id(), Read(caseData, "studyName"),
                BusinessDate.ParseIso(Read(caseData, "periodStart")),
                BusinessDate.ParseIso(Read(caseData, "periodEnd")), StudyPurpose.Management,
                StudyScenario.Actual, published.Structure.Id, new CurrencyCode("EUR"), "T13"));
            studyId = study.Id;

            var profile = new SourceParseProfileVersion(SourceParseProfileVersion.CurrentSchema,
                Id(), 1, "T13 approved source", ImportSourceKind.Csv, null, 1, 2,
                new(3, description: 2, amount: 1),
                new("utf-8", ';', '"', CsvBomPolicy.Optional),
                new(",", ".", [3], ImportNegativeSyntax.LeadingMinus),
                "yyyy-MM-dd", ImportAmountMode.PeriodAmount, ImportSignConvention.SignedAmount,
                study.PeriodStart, study.PeriodEnd, new CurrencyCode("EUR"), null,
                ImportDuplicatePolicy.Reject, ImportZeroPolicy.Keep, ImportAccountSelection.All,
                new(ImportProfileApprovalState.ApprovedBusiness,
                    "T13 signed P&L accounts, decimal commas and a checked zero row"), true);

            var csv = Path.Combine(basePath, "accounts.csv");
            using var input = await new ImportSourceSessionFactory().OpenAsync(csv);
            var source = await input.ReadAsync(
                new(profile.SourceKind, profile.WorksheetName, profile.HeaderRow,
                    profile.FirstDataRow, profile.Csv));
            var prepared = file.Imports.PrepareImport(companyId, studyId,
                study.Revision, Id(), source, profile);
            Assert.IsTrue(prepared.CanConfirm, "T13 source is approved, valid and confirmable");
            file.Imports.ConfirmImport(Command(prepared));
            var resources = file.Resources.ReadResources(companyId, studyId);
            // Exercise fixed, variable and mixed composition with the production
            // resource mutation API, including revision/persistence transitions.
            foreach (var composition in caseData.GetProperty("composition").EnumerateObject())
            {
                var sourceId = resources.Origins.Single(origin =>
                    origin.Code == composition.Name).Id;
                var resource = resources.Resources.Single(item => item.Id == sourceId);
                var behavior = Enum.Parse<ResourceBehavior>(
                    Read(composition.Value, "behavior"), false);
                var variable = composition.Value.GetProperty("variablePercentage").GetDecimal();
                var parts = resource.Parts.Select(part =>
                    part.WithClassification(part.Classification with
                    { Behavior = behavior, VariablePercentage = variable })).ToArray();
                file.Resources.ChangeResources(Mutation(resources, ResourceMutationKind.ClassifyBatch,
                    replacements: [new(resource.Id, parts)]));
                resources = file.Resources.ReadResources(companyId, studyId);
                Assert.AreEqual(behavior, resources.Resources.Single(item => item.Id == sourceId)
                    .Parts.Single().Classification.Behavior);
            }
            var expected = caseData.GetProperty("expected");
            Assert.AreEqual(Number(expected, "importTotal"),
                resources.Preparation.Coverage.Sum(c => c.Effective.Amount));
            Assert.AreEqual(caseData.GetProperty("primaryRules").GetArrayLength(),
                resources.Resources.Count);

            var primaryRules = new List<PrimaryRule>();
            foreach (var rule in caseData.GetProperty("primaryRules").EnumerateArray())
            {
                var account = Read(rule, "account");
                var weights = rule.GetProperty("weights").EnumerateObject()
                    .Select(weight => new AllocationKeyLine(nodeIds[weight.Name],
                        weight.Value.GetDecimal())).ToArray();
                var key = file.Allocations.SaveAllocationKey(
                    KeyCommand(resources, lines: weights) with { Name = "Primary " + account }).After;
                primaryRules.Add(new PrimaryRule(Id(), companyId, structure.Id,
                    AccountSelectorKind.Exact, account, 0, key.Id, true));
            }
            var secondaryRules = new List<SecondaryRule>();
            foreach (var rule in caseData.GetProperty("secondaryRules").EnumerateArray())
            {
                var sourceNode = Read(rule, "auxiliary");
                var weights = rule.GetProperty("weights").EnumerateObject()
                    .Select(weight => new AllocationKeyLine(nodeIds[weight.Name],
                        weight.Value.GetDecimal())).ToArray();
                // An old "Forbidden" flag must NOT block a secondary self-service route.
                var key = file.Allocations.SaveAllocationKey(
                    KeyCommand(resources, stage: AllocationStage.Secondary, lines: weights) with
                    { Name = "Secondary " + sourceNode, SelfAllocation = SelfAllocationPolicy.Forbidden }).After;
                secondaryRules.Add(new SecondaryRule(Id(), companyId, structure.Id,
                    nodeIds[sourceNode], "all", key.Id, true));
            }

            var configuration = new AllocationConfiguration(primaryRules, secondaryRules, [], [], []);
            file.Allocations.ChangeAllocations(AllocationCommand(resources, configuration));
            var live = file.Allocations.ReadAllocations(companyId, studyId);
            var primary = PrimaryAllocationService.Calculate(live);
            Assert.IsTrue(primary.IsComplete, "Primary allocation must cover every P&L account");
            var tolerance = Number(expected, "maxAbsoluteError");
            foreach (var entry in expected.GetProperty("primaryTotals").EnumerateObject())
                Assert.IsTrue(Math.Abs(primary.ByNode(nodeIds[entry.Name])
                    .Sum(f => f.PostedAmount.Amount) -
                    decimal.Parse(entry.Value.GetString()!, CultureInfo.InvariantCulture)) <= tolerance,
                    "Primary allocation mismatch at " + entry.Name);
            Assert.AreEqual(Number(expected, "importTotal"),
                primary.Facts.Sum(f => f.PostedAmount.Amount));
            Assert.IsTrue(primary.Facts.Any(f => f.Behavior == ResourceBehavior.Fixed),
                "T13 scenario must exercise fixed costs");
            Assert.IsTrue(primary.Facts.Any(f => f.Behavior == ResourceBehavior.Variable),
                "T13 scenario must exercise variable costs");

            file.Allocations.ChangeAllocations(
                AllocationCommand(live.Resources, configuration.WithPrimaryClosed(true)));
            live = file.Allocations.ReadAllocations(companyId, studyId);
            Assert.IsTrue(live.Configuration.PrimaryClosed);
            var result = SecondaryAllocationService.Calculate(live);
            Assert.IsTrue(result.IsComplete, "Reciprocal allocation must be feasible");
            foreach (var entry in expected.GetProperty("terminalTotals").EnumerateObject())
                Assert.IsTrue(Math.Abs(result.ByTerminal(nodeIds[entry.Name])
                    .Sum(f => f.PostedAmount.Amount) -
                    decimal.Parse(entry.Value.GetString()!, CultureInfo.InvariantCulture)) <= tolerance,
                    "Terminal cost mismatch at " + entry.Name);
            Assert.AreEqual(Number(expected, "importTotal"),
                result.Facts.Sum(f => f.PostedAmount.Amount));
            Assert.IsTrue(result.Reconciliations.All(r => r.Difference.Amount == 0m));
            Assert.IsTrue(result.ResourceBridges.All(r => r.Difference.Amount == 0m));

            var fingerprint = SecondaryAllocationService.ResultFingerprint(result, live.Configuration);
            var closed = file.Allocations.ChangeAllocations(
                AllocationCommand(live.Resources, live.Configuration.WithSecondaryClosed(true, fingerprint)));
            Assert.IsNotNull(closed.SecondaryPublication);
            terminalFacts = CanonicalSnapshotJson.Serialize(closed.SecondaryPublication!.Facts);
            var receipt = file.Studies.Capture(new(companyId, studyId,
                closed.ConfirmedStudy.Revision, Id(), "T13"));
            receipt.EnsureValid();
            frozenSnapshot = CanonicalSnapshotJson.Serialize(receipt);
            Assert.AreEqual(terminalFacts,
                CanonicalSnapshotJson.Serialize(
                    SecondaryAllocationService.Calculate((AllocationStudySnapshotInput)receipt.Input).Facts));
        }

        using (var reopened = Service().Open(companyPath, CompanyFileOpenMode.ReadOnly))
        {
            var persisted = reopened.Allocations.ReadAllocations(companyId, studyId);
            Assert.IsTrue(persisted.Configuration.PrimaryClosed);
            Assert.IsTrue(persisted.Configuration.SecondaryClosed);
            Assert.AreEqual(terminalFacts,
                CanonicalSnapshotJson.Serialize(persisted.SecondaryPublication!.Facts));
            Assert.AreEqual(frozenSnapshot,
                CanonicalSnapshotJson.Serialize(reopened.Studies.ReadSnapshots(companyId, studyId).Single()));
        }

        // Optional: create usable .valdrio examples and audit receipts on Windows.
        // Never write generated files into the tracked source folders implicitly.
        var output = Environment.GetEnvironmentVariable("VALDRIO_T13_EXPORT_DIR");
        if (!string.IsNullOrWhiteSpace(output))
        {
            var target = Path.Combine(Path.GetFullPath(output), folder);
            Directory.CreateDirectory(target);
            File.Copy(companyPath, Path.Combine(target, Read(caseData, "id").ToLowerInvariant() + ".valdrio"), true);
            File.WriteAllText(Path.Combine(target, "frozen-snapshot.json"), frozenSnapshot);
        }
    }
}
