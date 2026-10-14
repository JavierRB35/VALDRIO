namespace Valdrio.Calculation;

public enum ReciprocalFlowStage { Primary, Reciprocal, Final }

// This projector uses existing solution.Gross and the exact published
// coefficients. No iterating, no factorization and no per-account fake ledger.
public static class ReciprocalFlowProjector
{
    public sealed record Directed(ReciprocalFlowStage Stage, string SourceId, string TargetId, decimal Amount);

    public static IReadOnlyList<Directed> Project(ReciprocalInput graph, ReciprocalSolution solution)
    {
        var grossById = solution.AuxiliaryIds
            .Select((id, i) => (id, amount: solution.Gross[i]))
            .ToDictionary(x => x.id, x => x.amount, StringComparer.Ordinal);
        var arcs = new List<Directed>();
        for (var i = 0; i < graph.AuxiliaryIds.Length; i++)
        {
            var source = graph.AuxiliaryIds[i];
            // The published solution has a canonical sorted order, which need
            // not equal the published keys' source order.
            var gross = grossById[source];
            for (var j = 0; j < graph.AuxiliaryIds.Length; j++)
            {
                var coefficient = graph.Q[i][j];
                if (coefficient == 0m) continue;
                var amount = checked(gross * coefficient);
                if (amount != 0m)
                    arcs.Add(new(ReciprocalFlowStage.Reciprocal, source, graph.AuxiliaryIds[j], amount));
            }
            for (var j = 0; j < graph.TerminalIds.Length; j++)
            {
                var coefficient = graph.B[i][j];
                if (coefficient == 0m) continue;
                var amount = checked(gross * coefficient);
                if (amount != 0m)
                    arcs.Add(new(ReciprocalFlowStage.Final, source, graph.TerminalIds[j], amount));
            }
        }
        return arcs;
    }
}

