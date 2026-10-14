using Valdrio.Calculation;
using Valdrio.Reporting;

namespace Valdrio.Tests;

[TestClass]
public sealed class AuditPortableFlowTests
{
    [TestMethod]
    [DataRow("Service Company")]
    [DataRow("Industrial Company")]
    public async Task HeadlessStudyVerificationRetainsImportAndCoverageOfAllocationSnapshots(string company)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Synthetic Projects", company, "frozen-snapshot.json");
        var receipt = Valdrio.Application.CanonicalSnapshotJson.Deserialize<Valdrio.Application.StudySnapshotReceipt>(File.ReadAllText(path));
        var input = (Valdrio.Application.AllocationStudySnapshotInput)receipt.Input;
        var info = new System.Diagnostics.ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        info.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "runner", "Valdrio.Runner.dll"));
        info.ArgumentList.Add("--study-snapshot");
        info.ArgumentList.Add(path);
        using var process = System.Diagnostics.Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw; }
        Assert.AreEqual(0, process.ExitCode, await stderr);
        using var output = System.Text.Json.JsonDocument.Parse(await stdout);
        Assert.AreEqual("T08-allocation-preparation", output.RootElement.GetProperty("task").GetString());
        Assert.IsTrue(input.Imports.Count > 0);
        Assert.AreEqual(input.Imports.Count, output.RootElement.GetProperty("imports").GetArrayLength());
        Assert.AreEqual(input.Workspace.Preparation.Coverage.Count, output.RootElement.GetProperty("resourceCoverage").GetArrayLength());
        Assert.AreEqual(receipt.EconomicInputHash, output.RootElement.GetProperty("EconomicInputHash").GetString());
    }

    [TestMethod]
    public void PublishedFlowsConserveSignedSourcesAcrossOpenReciprocalGraphs()
    {
        // Independent conservation invariant: internal circulation is excluded.
        foreach (var fraction in new[] { 0m, 0.1m, 0.5m, 0.9m })
        foreach (var amount in new[] { -1000m, -0.00000001m, 0m, 0.00000001m, 1000m })
        {
            var graph = new ReciprocalInput(["B", "A"], ["P"],
                [[0m, fraction], [fraction, 0m]], [[1m - fraction], [1m - fraction]], false);
            var solution = ReciprocalFactorization.Prepare(graph).Solve(
                new Dictionary<string, decimal> { ["A"] = amount, ["B"] = amount * 2m },
                new Dictionary<string, decimal> { ["P"] = 7m });
            var before = solution.Gross.ToArray();
            var flows = ReciprocalFlowProjector.Project(graph, solution);
            var received = flows.Where(x => x.Stage == ReciprocalFlowStage.Final).Sum(x => x.Amount);
            Assert.IsTrue(Math.Abs(received - amount * 3m) <= 0.00000001m);
            Assert.IsTrue(Math.Abs(received + 7m - solution.Final[0]) <= 0.00000001m);
            CollectionAssert.AreEqual(before, solution.Gross, "Projection must not mutate a published solution.");
            CollectionAssert.AreEqual(flows.ToArray(), ReciprocalFlowProjector.Project(graph, solution).ToArray());
        }
    }

    [TestMethod]
    public void PortableReportingAndFlowEngineHaveNoWpfOrPersistenceDependency()
    {
        foreach (var assembly in new[] { typeof(ReciprocalFlowProjector).Assembly, typeof(SecondaryContributionProjector).Assembly })
        {
            var forbidden = assembly.GetReferencedAssemblies().Where(r =>
                r.Name!.StartsWith("Presentation", StringComparison.Ordinal) ||
                r.Name.StartsWith("Valdrio.Desktop", StringComparison.Ordinal) ||
                r.Name.StartsWith("Valdrio.Infrastructure", StringComparison.Ordinal) ||
                r.Name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(0, forbidden.Length);
        }
    }
}
