using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Tests;

[TestClass]
public sealed class T10ReciprocalTests
{
    private const decimal NumericTolerance = 0.00000001m;

    [TestMethod]
    public void CanonicalRationalCorpus_C09ToC14AndC32()
    {
        var filename = Path.Combine(AppContext.BaseDirectory, "reference", "t10-cases.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(filename));
        var accepted = new List<string>();
        foreach (var test in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            var id = test.GetProperty("id").GetString()!;
            if (id is not ("C09" or "C10" or "C11" or "C12" or "C13" or "C14" or "C32"))
                continue;
            var json = test.GetProperty("input");
            var aux = Strings(json.GetProperty("aux_ids"));
            var terminals = Strings(json.GetProperty("principal_ids"));
            var request = new ReciprocalInput(aux, terminals,
                Matrix(json.GetProperty("Q")), Matrix(json.GetProperty("B")),
                json.GetProperty("allow_self").GetBoolean());
            if (test.TryGetProperty("expected_diagnostic", out var diagnostic))
            {
                var ex = Assert.ThrowsExactly<DomainRuleException>(() => ReciprocalFactorization.Prepare(request));
                Assert.AreEqual(diagnostic.GetString(), ex.Diagnostic.Code, id);
            }
            else
            {
                var prepared = ReciprocalFactorization.Prepare(request);
                var values = prepared.Solve(
                    aux.Zip(Strings(json.GetProperty("b_aux")).Select(Decimal))
                       .ToDictionary(pair => pair.First, pair => pair.Second, StringComparer.Ordinal),
                    terminals.Zip(Strings(json.GetProperty("b_principal")).Select(Decimal))
                       .ToDictionary(pair => pair.First, pair => pair.Second, StringComparer.Ordinal));
                var expected = test.GetProperty("expected");
                Assert.AreEqual(ReciprocalFactorization.Policy, values.PolicyVersion);
                Assert.IsTrue(values.AbsoluteResidual <= NumericTolerance, id + "/residual");
                CheckVector(expected.GetProperty("gross"), values.Gross, id + "/gross");
                CheckVector(expected.GetProperty("final"), values.Final, id + "/final");
                Close(Decimal(expected.GetProperty("total").GetString()!), values.Final.Sum(), id + "/total");

                if (expected.TryGetProperty("by_aux_origin", out var origins))
                    foreach (var origin in origins.EnumerateObject())
                    {
                        var originalIndex = Array.IndexOf(aux, origin.Name);
                        var canonicalIndex = Array.IndexOf(values.AuxiliaryIds.ToArray(), origin.Name);
                        var cost = Decimal(json.GetProperty("b_aux")[originalIndex].GetString()!);
                        var byDestination = values.FactorsByAuxiliaryOrigin[canonicalIndex].Select(f => f * cost).ToArray();
                        CheckVector(origin.Value, byDestination, id + "/" + origin.Name);
                    }
            }
            accepted.Add(id);
        }
        CollectionAssert.AreEqual(new[] { "C09", "C10", "C11", "C12", "C13", "C14", "C32" }, accepted.ToArray());
    }

    [TestMethod]
    public void PermutingIdsAndInputsDoesNotChangeCanonicalOutput_AndReuseDoesNotMutateFactors()
    {
        var normal = new ReciprocalInput(["A", "B"], ["P1", "P2"],
            [[0m, 0.1m], [0.2m, 0m]], [[0.6m, 0.3m], [0.3m, 0.5m]], false);
        var permuted = new ReciprocalInput(["B", "A"], ["P2", "P1"],
            [[0m, 0.2m], [0.1m, 0m]], [[0.5m, 0.3m], [0.3m, 0.6m]], false);
        var a = ReciprocalFactorization.Prepare(normal);
        var b = ReciprocalFactorization.Prepare(permuted);
        var rhs = new Dictionary<string, decimal> { ["A"] = 1000m, ["B"] = 500m };
        var direct = new Dictionary<string, decimal> { ["P1"] = 2000m, ["P2"] = 3000m };
        var resultA = a.Solve(rhs, direct);
        var resultB = b.Solve(rhs, direct);
        CollectionAssert.AreEqual(resultA.Gross, resultB.Gross);
        CollectionAssert.AreEqual(resultA.Final, resultB.Final);
        for (var i = 0; i < 2; i++)
            CollectionAssert.AreEqual(resultA.FactorsByAuxiliaryOrigin[i], resultB.FactorsByAuxiliaryOrigin[i]);

        // Multiple independent economic origins reuse the same decomposition.
        var originA = a.Solve(new Dictionary<string, decimal> { ["A"] = 1000m, ["B"] = 0m },
            new Dictionary<string, decimal> { ["P1"] = 0m, ["P2"] = 0m });
        var originB = a.Solve(new Dictionary<string, decimal> { ["A"] = 0m, ["B"] = 500m },
            new Dictionary<string, decimal> { ["P1"] = 0m, ["P2"] = 0m });
        for (var i = 0; i < 2; i++)
            Close(resultA.Final[i] - direct[resultA.TerminalIds[i]], originA.Final[i] + originB.Final[i], "multiple RHS");
        var exported = a.EffectiveFactors;
        exported[0][0] = -1m;
        Assert.IsTrue(a.EffectiveFactors[0][0] > 0m);
    }

    [TestMethod]
    public void ZeroVectorInOpenGraphIsValid_ClosedGraphWithZeroVectorIsNot()
    {
        var input = new ReciprocalInput(["A", "B"], ["P"],
            [[0m, 0.2m], [0.1m, 0m]], [[0.8m], [0.9m]], false);
        var f = ReciprocalFactorization.Prepare(input);
        var result = f.Solve(new Dictionary<string, decimal> { ["A"] = 0m, ["B"] = 0m },
            new Dictionary<string, decimal> { ["P"] = 0m });
        Assert.IsTrue(result.Gross.All(v => v == 0m));
        Assert.IsTrue(result.Final.All(v => v == 0m));
        Assert.AreEqual(0m, result.AbsoluteResidual);
        var closed = input with { Q = [[0m, 1m], [1m, 0m]], B = [[0m], [0m]] };
        var ex = Assert.ThrowsExactly<DomainRuleException>(() => ReciprocalFactorization.Prepare(closed));
        Assert.AreEqual("GRAPH_CLOSED_COMPONENT", ex.Diagnostic.Code);
        Assert.AreEqual("A,B", ex.Diagnostic.Arguments.Single().CanonicalValue);
    }

    [TestMethod]
    public void RejectsInvalidCoverageAndUnsupportedSelfAssignment()
    {
        var input = new ReciprocalInput(["A"], ["P"], [[0m]], [[0.7m]], false);
        Assert.AreEqual("GRAPH_COVERAGE_INVALID",
            Assert.ThrowsExactly<DomainRuleException>(() => ReciprocalFactorization.Prepare(input)).Diagnostic.Code);
        var self = input with { Q = [[0.1m]], B = [[0.9m]] };
        Assert.AreEqual("GRAPH_SELF_NOT_ALLOWED",
            Assert.ThrowsExactly<DomainRuleException>(() => ReciprocalFactorization.Prepare(self)).Diagnostic.Code);
    }

    [TestMethod]
    public void HundredAuxiliariesBenchmarkAndCancellation()
    {
        const int count = 100;
        var ids = Enumerable.Range(0, count).Select(i => "A" + i.ToString("D3", CultureInfo.InvariantCulture)).ToArray();
        var q = Enumerable.Range(0, count).Select(_ => new decimal[count]).ToArray();
        var b = Enumerable.Range(0, count).Select(_ => new[] { 0.9m }).ToArray();
        for (var i = 0; i < count; i++)
            q[i][(i + 1) % count] = 0.1m;

        var input = new ReciprocalInput(ids, ["P"], q, b, false);
        var time = Stopwatch.StartNew();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var factorization = ReciprocalFactorization.Prepare(input);
        var outcome = factorization.Solve(ids.ToDictionary(id => id, _ => 1m),
            new Dictionary<string, decimal> { ["P"] = 0m });
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        time.Stop();
        Close(100m, outcome.Final.Single(), "100 auxiliaries conserved");
        Console.WriteLine("T10 100 auxiliary benchmark: elapsedMs=" + time.ElapsedMilliseconds +
            "; allocatedBytesCurrentThread=" + allocated + "; maxAmplification=" +
            factorization.MaxCirculationAmplification.ToString(CultureInfo.InvariantCulture));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => ReciprocalFactorization.Prepare(input, cts.Token));
        Assert.ThrowsExactly<OperationCanceledException>(() => factorization.Solve(
            ids.ToDictionary(id => id, _ => 1m),
            new Dictionary<string, decimal> { ["P"] = 0m }, cts.Token));
    }

    private static string[] Strings(JsonElement array) =>
        array.EnumerateArray().Select(item => item.GetString()!).ToArray();

    private static decimal[][] Matrix(JsonElement rows) =>
        rows.EnumerateArray().Select(row => Strings(row).Select(Decimal).ToArray()).ToArray();

    private static decimal Decimal(string text)
    {
        var slash = text.IndexOf('/');
        return slash < 0
            ? decimal.Parse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture)
            : Decimal(text[..slash]) / Decimal(text[(slash + 1)..]);
    }

    private static void CheckVector(JsonElement expected, IReadOnlyList<decimal> actual, string name)
    {
        Assert.AreEqual(expected.GetArrayLength(), actual.Count, name + "/length");
        for (var i = 0; i < actual.Count; i++)
            Close(Decimal(expected[i].GetString()!), actual[i], name + "/" + i);
    }

    private static void Close(decimal expected, decimal actual, string name) =>
        Assert.IsTrue(Math.Abs(expected - actual) <= NumericTolerance,
            name + ": expected " + expected.ToString(CultureInfo.InvariantCulture) +
            "; actual " + actual.ToString(CultureInfo.InvariantCulture));
}
