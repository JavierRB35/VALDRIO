using System.Text.Json;
using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Runner;

// Portable diagnostic/explanation from a frozen real study snapshot.
// This report does not change the company file or implicitly close a phase.
internal static class T11SecondaryRunner
{
    public static int RunStudy(string[] args)
    {
        try
        {
            var receipt = CanonicalSnapshotJson.Deserialize<StudySnapshotReceipt>(File.ReadAllText(args[1]));
            receipt.EnsureValid();
            if (receipt.Input is not AllocationStudySnapshotInput snapshot)
            {
                Console.Error.WriteLine("SECONDARY_SNAPSHOT_SCHEMA_UNSUPPORTED");
                return 3;
            }
            var calculated = SecondaryAllocationService.Calculate(snapshot);
            var result = new
            {
                status = "balanced",
                task = "T11",
                schema = SecondaryAllocationResult.Schema,
                exportProfile = "CanonicalExchange",
                receipt.StudyId, receipt.EconomicInputHash, receipt.FullSnapshotHash,
                calculated.Primary.Currency,
                calculated.Primary.Revision,
                policy = calculated.Circulation.PolicyVersion,
                calculated.Facts,
                calculated.Totals,
                calculated.Reconciliations,
                calculated.ResourceBridges,
                calculated.ExplanationLinks,
                grossAuxiliaryFlows = calculated.Circulation.AuxiliaryIds.Select((id, i) =>
                    new SecondaryAuxiliaryGross(id, calculated.Circulation.Gross[i])).ToArray(),
                calculated.Circulation.MaxCirculationAmplification,
                calculated.Circulation.MinNormalizedPivot,
                published = false
            };
            using var doc = JsonDocument.Parse(CanonicalSnapshotJson.Serialize(result));
            var output = doc.RootElement.GetProperty("Value").GetRawText();
            if (args.Length == 3) File.WriteAllText(args[2], output);
            Console.WriteLine(output);
            return 0;
        }
        catch (DomainRuleException ex)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                status = "blocked",
                task = "T11",
                schema = SecondaryAllocationResult.Schema,
                ex.Diagnostic.Code,
                ex.Diagnostic.MessageKey,
                arguments = ex.Diagnostic.Arguments.Select(a => new
                {
                    a.Name, Type = a.Type.ToString(), a.CanonicalValue
                }).ToArray()
            }));
            return 4;
        }
        catch (Exception ex) when (ex is IOException or JsonException or FormatException or
            ArgumentException or InvalidOperationException or OverflowException)
        {
            Console.Error.WriteLine("SECONDARY_SNAPSHOT_INVALID: " + ex.GetType().Name);
            return 3;
        }
    }
}
