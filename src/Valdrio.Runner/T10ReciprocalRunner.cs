using System.Globalization;
using System.Text.Json;
using Valdrio.Application;
using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Runner;

// Portable T10 feasibility report, independent of UI, database and display culture.
// Input is a synthetic/proposed routes file, not a published economic run.
internal static class T10ReciprocalRunner
{
    public static int Run(string[] args)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(args[1]));
            var root = doc.RootElement;
            if (root.TryGetProperty("input", out var contained))
                root = contained;
            var auxiliaries = Strings(root.GetProperty("aux_ids"));
            var terminals = Strings(root.GetProperty("principal_ids"));
            var request = new ReciprocalInput(
                auxiliaries, terminals, Matrix(root.GetProperty("Q")),
                Matrix(root.GetProperty("B")), root.GetProperty("allow_self").GetBoolean());
            var factor = ReciprocalFactorization.Prepare(request);
            var answer = factor.Solve(
                auxiliaries.Zip(Strings(root.GetProperty("b_aux")).Select(CanonicalDecimal.Parse))
                    .ToDictionary(pair => pair.First, pair => pair.Second, StringComparer.Ordinal),
                terminals.Zip(Strings(root.GetProperty("b_principal")).Select(CanonicalDecimal.Parse))
                    .ToDictionary(pair => pair.First, pair => pair.Second, StringComparer.Ordinal));
            var serialized = JsonSerializer.Serialize(new
            {
                status = "viable",
                task = "T10",
                schema = "valdrio-reciprocal-feasibility-v1",
                policy = answer.PolicyVersion,
                auxiliaryIds = answer.AuxiliaryIds,
                terminalIds = answer.TerminalIds,
                gross = answer.Gross.Select(CanonicalDecimal.Format).ToArray(),
                final = answer.Final.Select(CanonicalDecimal.Format).ToArray(),
                factorsByAuxiliaryOrigin = answer.FactorsByAuxiliaryOrigin
                    .Select(row => row.Select(CanonicalDecimal.Format).ToArray()).ToArray(),
                absoluteResidual = CanonicalDecimal.Format(answer.AbsoluteResidual),
                relativeResidual = CanonicalDecimal.Format(answer.RelativeResidual),
                maxAmplification = CanonicalDecimal.Format(answer.MaxCirculationAmplification),
                minNormalizedPivot = CanonicalDecimal.Format(answer.MinNormalizedPivot),
                warnings = answer.Warnings.Select(Diagnostic).ToArray()
            }, new JsonSerializerOptions { WriteIndented = true });
            Output(args, serialized);
            return 0;
        }
        catch (DomainRuleException ex)
        {
            Output(args, JsonSerializer.Serialize(new
            {
                status = "blocked",
                task = "T10",
                schema = "valdrio-reciprocal-feasibility-v1",
                policy = ReciprocalFactorization.Policy,
                diagnostic = Diagnostic(ex.Diagnostic)
            }));
            return 4;
        }
        catch (Exception ex) when (ex is IOException or JsonException or FormatException or
            ArgumentException or OverflowException or InvalidOperationException)
        {
            Console.Error.WriteLine("RECIPROCAL_INPUT_INVALID: " + ex.GetType().Name);
            return 3;
        }
    }


    // Read a real frozen T08 receipt; validate routing only, never invent
    // primary costs or publish T11 results.
    public static int RunStudy(string[] args)
    {
        try
        {
            var receipt = CanonicalSnapshotJson.Deserialize<StudySnapshotReceipt>(File.ReadAllText(args[1]));
            receipt.EnsureValid();
            if (receipt.Input is not AllocationStudySnapshotInput snapshot)
            {
                Console.Error.WriteLine("RECIPROCAL_STUDY_SCHEMA_UNSUPPORTED");
                return 3;
            }
            var group = args.Length == 3 ? args[2] : "all";
            var graph = ReciprocalGraphBuilder.FromPublishedSecondaryKeys(
                snapshot.Model.Structure, snapshot.Allocations.Keys,
                snapshot.Allocations.Configuration.SecondaryRules,
                snapshot.Model.DriverUnits, snapshot.PrecisionPolicy, group);
            var factor = ReciprocalFactorization.Prepare(graph);
            var report = factor.Solve(
                graph.AuxiliaryIds.ToDictionary(id => id, _ => 0m, StringComparer.Ordinal),
                graph.TerminalIds.ToDictionary(id => id, _ => 0m, StringComparer.Ordinal));
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                status = "routing-viable",
                task = "T10",
                schema = "valdrio-reciprocal-feasibility-v1",
                policy = report.PolicyVersion,
                componentRoutingGroup = group,
                receipt.StudyId,
                receipt.EconomicInputHash,
                auxiliaryIds = report.AuxiliaryIds,
                terminalIds = report.TerminalIds,
                factorsByAuxiliaryOrigin = report.FactorsByAuxiliaryOrigin
                    .Select(row => row.Select(CanonicalDecimal.Format).ToArray()).ToArray(),
                maxAmplification = CanonicalDecimal.Format(report.MaxCirculationAmplification),
                minNormalizedPivot = CanonicalDecimal.Format(report.MinNormalizedPivot),
                warnings = report.Warnings.Select(Diagnostic).ToArray(),
                economicExecution = false
            }));
            return 0;
        }
        catch (DomainRuleException ex)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                status = "routing-blocked",
                task = "T10",
                policy = ReciprocalFactorization.Policy,
                diagnostic = Diagnostic(ex.Diagnostic)
            }));
            return 4;
        }
        catch (Exception ex) when (ex is IOException or JsonException or FormatException or
            ArgumentException or OverflowException or InvalidOperationException)
        {
            Console.Error.WriteLine("RECIPROCAL_STUDY_INVALID: " + ex.GetType().Name);
            return 3;
        }
    }

    private static object Diagnostic(DomainDiagnostic value) => new
    {
        value.Code,
        value.MessageKey,
        arguments = value.Arguments.Select(a => new { a.Name, type = a.Type.ToString(), a.CanonicalValue }).ToArray()
    };

    private static string[] Strings(JsonElement values) =>
        values.EnumerateArray().Select(value => value.GetString() ??
            throw new JsonException("Expected canonical text.")).ToArray();

    private static decimal[][] Matrix(JsonElement values) =>
        values.EnumerateArray().Select(row => Strings(row).Select(CanonicalDecimal.Parse).ToArray()).ToArray();

    private static void Output(string[] args, string serialized)
    {
        Console.WriteLine(serialized);
        if (args.Length == 3)
            File.WriteAllText(args[2], serialized);
    }
}
