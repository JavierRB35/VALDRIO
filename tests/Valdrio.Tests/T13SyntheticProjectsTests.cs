using System.Globalization;
using System.Text.Json;
using Valdrio.Application;
using Valdrio.Calculation;
using Valdrio.Domain;
using Valdrio.Infrastructure;
using Valdrio.Infrastructure.CompanyFiles;

namespace Valdrio.Tests;

public sealed partial class T06ImportConfirmationTests
{
    [TestMethod]
    public void T13_ClosedSelfOnlyGraphRemainsInfeasibleAlthoughSelfAllocationIsAllowed()
    {
        var closed = new ReciprocalInput(["MAINTENANCE"], ["CUTTING"],
            [[1m]], [[0m]], true);
        var failure = Assert.ThrowsExactly<DomainRuleException>(
            () => ReciprocalFactorization.Prepare(closed));
        Assert.AreEqual("GRAPH_CLOSED_COMPONENT", failure.Diagnostic.Code);
    }

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
        string completedRun = "";
        EntityId savedReceiptId = default;
        EntityId completedRunId = default;

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
            savedReceiptId = receipt.Id;
            frozenSnapshot = CanonicalSnapshotJson.Serialize(receipt);
            Assert.AreEqual(terminalFacts,
                CanonicalSnapshotJson.Serialize(
                    SecondaryAllocationService.Calculate((AllocationStudySnapshotInput)receipt.Input).Facts));

            // T13 now proves the complete T12 publication path against the same
            // independent totals, rather than treating a legacy T11 closure as a run.
            var run = file.Runs.PrepareRun(new(companyId, studyId,
                closed.ConfirmedStudy.Revision, Id(), "T13"));
            var runOutput = CalculationRunService.Calculate(run);
            Assert.IsNotNull(runOutput.Primary);
            Assert.IsNotNull(runOutput.Secondary, "The synthetic reciprocal graph must complete as a T12 run.");
            Assert.AreEqual(Number(expected, "importTotal"),
                runOutput.Secondary!.Facts.Sum(f => f.PostedAmount.Amount),
                "Internal auxiliary transfers must never be added to terminal expense.");
            Assert.AreEqual(terminalFacts, CanonicalSnapshotJson.Serialize(runOutput.Secondary.Facts));
            Assert.IsTrue(runOutput.Transfers.Any(t => t.Internal),
                "Both cases must cover actual auxiliary-to-auxiliary circulation.");
            Assert.IsTrue(runOutput.Secondary.Reconciliations.All(r => r.Difference.Amount == 0m));
            Assert.IsTrue(runOutput.Secondary.ResourceBridges.All(r =>
                r.Difference.Amount == 0m && r.Pending.Amount == 0m));
            foreach (var entry in expected.GetProperty("terminalTotals").EnumerateObject())
                Assert.IsTrue(Math.Abs(runOutput.Secondary.Facts
                    .Where(f => f.TerminalNodeId == nodeIds[entry.Name])
                    .Sum(f => f.PostedAmount.Amount) -
                    decimal.Parse(entry.Value.GetString()!, CultureInfo.InvariantCulture)) <= tolerance,
                    "T12 final node differs from the independent T13 oracle: " + entry.Name);
            var publishedRun = file.Runs.PublishRun(run.Id, runOutput);
            Assert.AreEqual(CalculationRunStatus.Completed, publishedRun.Status);
            completedRunId = publishedRun.Id;
            completedRun = CanonicalSnapshotJson.Serialize(publishedRun);
            var beforeRead = file.Studies.ReadSnapshots(companyId, studyId).Count;
            var history = file.Runs.ReadRuns(companyId, studyId);
            Assert.AreEqual(1, history.Runs.Count);
            Assert.AreEqual(completedRunId, history.CurrentRunId);
            Assert.AreEqual(completedRunId, history.MatchingCompleted(history.StudyRevision)?.Id);
            Assert.AreEqual(beforeRead, file.Studies.ReadSnapshots(companyId, studyId).Count,
                "Reading or checking an identical completed run must not create another snapshot.");
        }

        using (var reopened = Service().Open(companyPath, CompanyFileOpenMode.ReadOnly))
        {
            var persisted = reopened.Allocations.ReadAllocations(companyId, studyId);
            Assert.IsTrue(persisted.Configuration.PrimaryClosed);
            Assert.IsTrue(persisted.Configuration.SecondaryClosed);
            Assert.AreEqual(terminalFacts,
                CanonicalSnapshotJson.Serialize(persisted.SecondaryPublication!.Facts));
            Assert.AreEqual(frozenSnapshot, CanonicalSnapshotJson.Serialize(
                reopened.Studies.ReadSnapshots(companyId, studyId).Single(s => s.Id == savedReceiptId)));
            var history = reopened.Runs.ReadRuns(companyId, studyId);
            Assert.AreEqual(1, history.Runs.Count);
            Assert.AreEqual(completedRunId, history.CurrentRunId);
            Assert.AreEqual(completedRunId, history.MatchingCompleted(history.StudyRevision)?.Id);
            Assert.AreEqual(completedRun,
                CanonicalSnapshotJson.Serialize(history.Runs.Single(r => r.Id == completedRunId)),
                "A historical T12 run must reopen byte-for-byte with no solver replay.");
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
            File.WriteAllText(Path.Combine(target, "completed-t12-run.json"), completedRun);
        }
    }

    // Preserve both tracked T13 v9 files as migration fixtures. Never rewrite
    // them while generating a current-format copy with a real T12 run.
    [TestMethod]
    [DataRow("Service Company", "services", 17500)]
    [DataRow("Industrial Company", "industrial", 36000)]
    public void T13_LegacySyntheticFileUpgradesToT12WithoutChangingSource(
        string folder, string fileName, int expectedAmount)
    {
        var basePath = Path.Combine(AppContext.BaseDirectory, "Synthetic Projects", folder);
        var sourcePath = Path.Combine(basePath, fileName + ".valdrio");
        var goldenPath = Path.Combine(basePath, "frozen-snapshot.json");
        Assert.IsTrue(File.Exists(sourcePath), "The real T13 SQLite v9 company must be copied to test output.");
        var originalHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            File.ReadAllBytes(sourcePath)));
        Assert.IsTrue(Service().RequiresUpgrade(sourcePath), "The checked-in T13 file is a genuine v9 input.");

        var destination = FileOf(fileName + "-t12-upgraded.valdrio");
        EntityId companyId;
        EntityId studyId;
        string completedRun;
        EntityId runId;
        using (var file = Service().Upgrade(sourcePath, destination, "T13-upgrade"))
        {
            Assert.AreEqual(SqliteCompanyFileService.SchemaVersion, file.Read().SchemaVersion);
            Assert.IsFalse(Service().RequiresUpgrade(destination));
            var study = file.Studies.ReadStudies().Single();
            companyId = study.CompanyId;
            studyId = study.Id;
            Assert.AreEqual(File.ReadAllText(goldenPath),
                CanonicalSnapshotJson.Serialize(file.Studies.ReadSnapshots(companyId, studyId).Single()),
                "T11 receipt and imported history must survive the 9-to-10 upgrade exactly.");
            var oldClosure = file.Allocations.ReadAllocations(companyId, studyId);
            Assert.AreEqual((decimal)expectedAmount, oldClosure.SecondaryPublication!.Facts
                .Sum(f => f.PostedAmount.Amount), "T13 terminal amount must survive migration.");

            var run = file.Runs.PrepareRun(new(companyId, studyId, study.Revision, Id(), "T13-upgrade"));
            var output = CalculationRunService.Calculate(run);
            Assert.IsNotNull(output.Secondary, "The migrated T13 model must run under T12.");
            Assert.AreEqual((decimal)expectedAmount, output.Secondary!.Facts.Sum(f => f.PostedAmount.Amount));
            Assert.IsTrue(output.Transfers.Any(t => t.Internal), "Reciprocal transfer audit was lost.");
            Assert.IsTrue(output.Secondary.Reconciliations.All(r => r.Difference.Amount == 0m));
            Assert.IsTrue(output.Secondary.ResourceBridges.All(r => r.Difference.Amount == 0m));
            var completed = file.Runs.PublishRun(run.Id, output);
            Assert.AreEqual(CalculationRunStatus.Completed, completed.Status);
            runId = completed.Id;
            completedRun = CanonicalSnapshotJson.Serialize(completed);
            var before = file.Studies.ReadSnapshots(companyId, studyId).Count;
            var history = file.Runs.ReadRuns(companyId, studyId);
            Assert.AreEqual(1, history.Runs.Count);
            Assert.AreEqual(runId, history.MatchingCompleted(study.Revision)?.Id);
            Assert.AreEqual(before, file.Studies.ReadSnapshots(companyId, studyId).Count);
        }

        using (var reopened = Service().Open(destination, CompanyFileOpenMode.ReadOnly))
        {
            var history = reopened.Runs.ReadRuns(companyId, studyId);
            Assert.AreEqual(1, history.Runs.Count);
            Assert.AreEqual(runId, history.CurrentRunId);
            Assert.AreEqual(completedRun,
                CanonicalSnapshotJson.Serialize(history.Runs.Single()),
                "Historical T12 runs must remain frozen after reopening a migrated company.");
            Assert.AreEqual(File.ReadAllText(goldenPath),
                CanonicalSnapshotJson.Serialize(reopened.Studies.ReadSnapshots(companyId, studyId)
                    .Single(s => s.Id != runId)),
                "Migration must preserve the original T11 snapshot.");
        }
        Assert.AreEqual(originalHash,
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(sourcePath))),
            "The packaged T13 v9 company must stay byte-for-byte intact.");
    }

}
