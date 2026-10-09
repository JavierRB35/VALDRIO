using System.Globalization;
using Valdrio.Domain;

namespace Valdrio.Calculation;

// PNUM-1 candidate. All fractions are in [0,1] and each auxiliary's routes
// must cover exactly 1. No presentation settings, I/O, or monetary rounding.
public sealed record ReciprocalInput(
    string[] AuxiliaryIds, string[] TerminalIds,
    decimal[][] Q, decimal[][] B, bool AllowSelf);

public sealed record ReciprocalSolution(
    string PolicyVersion,
    IReadOnlyList<string> AuxiliaryIds,
    IReadOnlyList<string> TerminalIds,
    decimal[] Gross,
    decimal[] Final,
    decimal[][] FactorsByAuxiliaryOrigin,
    decimal AbsoluteResidual,
    decimal RelativeResidual,
    decimal MaxCirculationAmplification,
    decimal MinNormalizedPivot,
    IReadOnlyList<DomainDiagnostic> Warnings);

// Reusable factorization: several economic origins can be solved against the
// same M without calculating an inverse or refactorizing.
public sealed class ReciprocalFactorization
{
    public const string Policy = "PNUM-1-proposed";
    private const decimal PivotFloor = 0.000000000001m;
    private const decimal FactorTolerance = 0.000000000000000001m;
    private const decimal RelativeTolerance = 0.000000000001m;
    private const decimal WarnAmplification = 1000000m;
    private const decimal BlockAmplification = 1000000000000m;

    private readonly decimal[,] matrix;
    private readonly decimal[,] lu;
    private readonly decimal[,] terminalRoutes;
    private readonly int[] permutation;
    private readonly decimal[][] factors;
    private readonly string[] auxiliaryIds;
    private readonly string[] terminalIds;

    private ReciprocalFactorization(
        decimal[,] matrix, decimal[,] lu, decimal[,] terminalRoutes,
        int[] permutation, decimal[][] factors, string[] auxiliaries,
        string[] terminals, decimal minPivot, decimal amplification)
    {
        this.matrix = matrix;
        this.lu = lu;
        this.terminalRoutes = terminalRoutes;
        this.permutation = permutation;
        this.factors = factors;
        auxiliaryIds = auxiliaries;
        terminalIds = terminals;
        MinNormalizedPivot = minPivot;
        MaxCirculationAmplification = amplification;
    }

    public IReadOnlyList<string> AuxiliaryIds => Array.AsReadOnly(auxiliaryIds);
    public IReadOnlyList<string> TerminalIds => Array.AsReadOnly(terminalIds);
    public decimal MinNormalizedPivot { get; }
    public decimal MaxCirculationAmplification { get; }
    public decimal[][] EffectiveFactors => factors.Select(row => (decimal[])row.Clone()).ToArray();

    public static ReciprocalFactorization Prepare(ReciprocalInput input, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.AuxiliaryIds);
        ArgumentNullException.ThrowIfNull(input.TerminalIds);
        ArgumentNullException.ThrowIfNull(input.Q);
        ArgumentNullException.ThrowIfNull(input.B);

        var n = input.AuxiliaryIds.Length;
        var p = input.TerminalIds.Length;
        if (n == 0 || p == 0 || input.AuxiliaryIds.Concat(input.TerminalIds).Any(string.IsNullOrWhiteSpace) ||
            input.AuxiliaryIds.Concat(input.TerminalIds).Distinct(StringComparer.Ordinal).Count() != n + p ||
            input.Q.Length != n || input.B.Length != n ||
            input.Q.Any(row => row is null || row.Length != n) ||
            input.B.Any(row => row is null || row.Length != p))
            throw Error("GRAPH_SCHEMA_INVALID", "Reciprocal.SchemaInvalid");

        // Normalize only ordering, never the economic weights.
        var ai = Enumerable.Range(0, n).OrderBy(i => input.AuxiliaryIds[i], StringComparer.Ordinal).ToArray();
        var ti = Enumerable.Range(0, p).OrderBy(i => input.TerminalIds[i], StringComparer.Ordinal).ToArray();
        var aux = ai.Select(i => input.AuxiliaryIds[i]).ToArray();
        var terminal = ti.Select(i => input.TerminalIds[i]).ToArray();
        var q = new decimal[n, n];
        var b = new decimal[n, p];
        try
        {
            for (var i = 0; i < n; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                decimal coverage = 0m;
                for (var j = 0; j < n; j++)
                {
                    var weight = input.Q[ai[i]][ai[j]];
                    if (weight is < 0m or > 1m)
                        throw Error("GRAPH_WEIGHT_INVALID", "Reciprocal.WeightInvalid", Id("auxiliary", aux[i]));
                    if (i == j && weight != 0m && !input.AllowSelf)
                        throw Error("GRAPH_SELF_NOT_ALLOWED", "Reciprocal.SelfNotAllowed", Id("auxiliary", aux[i]));
                    q[i, j] = weight;
                    coverage = checked(coverage + weight);
                }
                for (var j = 0; j < p; j++)
                {
                    var weight = input.B[ai[i]][ti[j]];
                    if (weight is < 0m or > 1m)
                        throw Error("GRAPH_WEIGHT_INVALID", "Reciprocal.WeightInvalid", Id("auxiliary", aux[i]));
                    b[i, j] = weight;
                    coverage = checked(coverage + weight);
                }
                if (coverage != 1m)
                    throw Error("GRAPH_COVERAGE_INVALID", "Reciprocal.CoverageInvalid", Id("auxiliary", aux[i]),
                        Number("coverage", coverage));
            }

            // Reachability must hold even for b=0; zero cost cannot legitimize
            // a closed component. Include the actual disconnected IDs.
            var reachable = new bool[n];
            for (var i = 0; i < n; i++)
                for (var j = 0; j < p; j++)
                    reachable[i] |= b[i, j] > 0m;
            bool changed;
            do
            {
                changed = false;
                for (var i = 0; i < n; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (reachable[i]) continue;
                    for (var j = 0; j < n; j++)
                        if (q[i, j] > 0m && reachable[j])
                        {
                            reachable[i] = true;
                            changed = true;
                            break;
                        }
                }
            } while (changed);
            if (reachable.Any(value => !value))
                throw Error("GRAPH_CLOSED_COMPONENT", "Reciprocal.ClosedComponent",
                    Id("auxiliaries", string.Join(",", Enumerable.Range(0, n).Where(i => !reachable[i]).Select(i => aux[i]))));

            var m = new decimal[n, n];
            var lu = new decimal[n, n];
            var rowNorm = new decimal[n];
            var pivots = Enumerable.Range(0, n).ToArray();
            for (var i = 0; i < n; i++)
                for (var j = 0; j < n; j++)
                {
                    m[i, j] = (i == j ? 1m : 0m) - q[j, i];
                    lu[i, j] = m[i, j];
                    rowNorm[i] = Math.Max(rowNorm[i], Math.Abs(m[i, j]));
                }

            var smallest = 1m;
            for (var k = 0; k < n; k++)
            {
                cancellation.ThrowIfCancellationRequested();
                var pivot = k;
                for (var i = k + 1; i < n; i++)
                    if (Math.Abs(lu[i, k]) > Math.Abs(lu[pivot, k]))
                        pivot = i;
                if (lu[pivot, k] == 0m)
                    throw Error("SOLVER_SINGULAR", "Reciprocal.Singular", Id("auxiliary", aux[pivots[pivot]]));
                if (pivot != k)
                {
                    for (var j = 0; j < n; j++)
                        (lu[k, j], lu[pivot, j]) = (lu[pivot, j], lu[k, j]);
                    (pivots[k], pivots[pivot]) = (pivots[pivot], pivots[k]);
                    (rowNorm[k], rowNorm[pivot]) = (rowNorm[pivot], rowNorm[k]);
                }
                var normalized = Math.Abs(lu[k, k]) / rowNorm[k];
                smallest = Math.Min(smallest, normalized);
                if (normalized < PivotFloor)
                    throw Error("SOLVER_SENSITIVE", "Reciprocal.SensitivePivot", Id("auxiliary", aux[pivots[k]]),
                        Number("pivot", normalized));
                for (var i = k + 1; i < n; i++)
                {
                    lu[i, k] /= lu[k, k];
                    for (var j = k + 1; j < n; j++)
                        lu[i, j] = checked(lu[i, j] - lu[i, k] * lu[k, j]);
                }
            }

            // Each origin's unit vector is an independent RHS. The same LU is
            // reused to obtain terminal transfer factors and circulation.
            var columns = new decimal[n][];
            var amplification = 0m;
            for (var origin = 0; origin < n; origin++)
            {
                cancellation.ThrowIfCancellationRequested();
                var unit = new decimal[n];
                unit[origin] = 1m;
                columns[origin] = SolveLu(lu, pivots, unit, cancellation);
            }
            var factors = new decimal[n][];
            for (var origin = 0; origin < n; origin++)
            {
                cancellation.ThrowIfCancellationRequested();
                factors[origin] = new decimal[p];
                decimal factorTotal = 0m;
                for (var j = 0; j < p; j++)
                {
                    decimal factor = 0m;
                    for (var i = 0; i < n; i++)
                        factor = checked(factor + b[i, j] * columns[origin][i]);
                    if (factor < -FactorTolerance)
                        throw Error("SOLVER_NEGATIVE_FACTOR", "Reciprocal.NegativeFactor", Id("auxiliary", aux[origin]));
                    factors[origin][j] = factor;
                    factorTotal = checked(factorTotal + factor);
                }
                if (Math.Abs(factorTotal - 1m) > FactorTolerance)
                    throw Error("SOLVER_FACTOR_DRIFT", "Reciprocal.FactorDrift", Id("auxiliary", aux[origin]),
                        Number("drift", Math.Abs(factorTotal - 1m)));
            }
            for (var i = 0; i < n; i++)
            {
                decimal circulation = 0m;
                for (var origin = 0; origin < n; origin++)
                    circulation = checked(circulation + Math.Abs(columns[origin][i]));
                amplification = Math.Max(amplification, circulation);
            }
            if (amplification > BlockAmplification)
                throw Error("SOLVER_SENSITIVE", "Reciprocal.SensitiveAmplification", Number("amplification", amplification));
            return new(m, lu, b, pivots, factors, aux, terminal, smallest, amplification);
        }
        catch (OverflowException)
        {
            throw Error("SOLVER_NUMERIC_OVERFLOW", "Reciprocal.NumericOverflow");
        }
    }

    // Values are indexed by IDs, not by the original input order or UI order.
    public ReciprocalSolution Solve(
        IReadOnlyDictionary<string, decimal> auxiliaryCosts,
        IReadOnlyDictionary<string, decimal> terminalCosts,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(auxiliaryCosts);
        ArgumentNullException.ThrowIfNull(terminalCosts);
        if (auxiliaryCosts.Count != auxiliaryIds.Length ||
            terminalCosts.Count != terminalIds.Length ||
            auxiliaryIds.Any(id => !auxiliaryCosts.ContainsKey(id)) ||
            terminalIds.Any(id => !terminalCosts.ContainsKey(id)))
            throw Error("SOLVER_RHS_INVALID", "Reciprocal.RhsInvalid");

        var rhs = auxiliaryIds.Select(id => auxiliaryCosts[id]).ToArray();
        try
        {
            var gross = SolveLu(lu, permutation, rhs, cancellation);
            var terminal = terminalIds.Select(id => terminalCosts[id]).ToArray();
            for (var j = 0; j < terminal.Length; j++)
            {
                cancellation.ThrowIfCancellationRequested();
                for (var i = 0; i < gross.Length; i++)
                    terminal[j] = checked(terminal[j] + terminalRoutes[i, j] * gross[i]);
            }
            decimal residual = 0m, normMatrix = 0m, normX = 0m, normB = 0m;
            for (var i = 0; i < gross.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                decimal rowSum = 0m, actual = 0m;
                for (var j = 0; j < gross.Length; j++)
                {
                    rowSum = checked(rowSum + Math.Abs(matrix[i, j]));
                    actual = checked(actual + matrix[i, j] * gross[j]);
                }
                residual = Math.Max(residual, Math.Abs(actual - rhs[i]));
                normMatrix = Math.Max(normMatrix, rowSum);
                normX = Math.Max(normX, Math.Abs(gross[i]));
                normB = Math.Max(normB, Math.Abs(rhs[i]));
            }
            var denominator = checked(normMatrix * normX + normB);
            var relative = denominator == 0m ? 0m : residual / denominator;
            if (residual > Math.Max(0.00000001m, 0.000000000001m * normB) || relative > RelativeTolerance)
                throw Error("SOLVER_RESIDUAL", "Reciprocal.Residual", Number("residual", residual),
                    Number("relative", relative));
            var warnings = MaxCirculationAmplification > WarnAmplification
                ? new[] { new DomainDiagnostic("SOLVER_AMPLIFICATION_WARNING", "Reciprocal.AmplificationWarning",
                    [Number("amplification", MaxCirculationAmplification)]) }
                : Array.Empty<DomainDiagnostic>();
            return new(Policy, AuxiliaryIds, TerminalIds, gross, terminal, EffectiveFactors,
                residual, relative, MaxCirculationAmplification, MinNormalizedPivot, warnings);
        }
        catch (OverflowException)
        {
            throw Error("SOLVER_NUMERIC_OVERFLOW", "Reciprocal.NumericOverflow");
        }
    }

    private static decimal[] SolveLu(decimal[,] lu, int[] pivots, decimal[] rhs, CancellationToken cancellation)
    {
        var n = pivots.Length;
        var x = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            x[i] = rhs[pivots[i]];
            for (var j = 0; j < i; j++)
                x[i] -= lu[i, j] * x[j];
        }
        for (var i = n - 1; i >= 0; i--)
        {
            cancellation.ThrowIfCancellationRequested();
            for (var j = i + 1; j < n; j++)
                x[i] -= lu[i, j] * x[j];
            x[i] /= lu[i, i];
        }
        return x;
    }

    private static DiagnosticArgument Id(string name, string value) =>
        new(name, DiagnosticArgumentType.Identifier, value);

    private static DiagnosticArgument Number(string name, decimal value) =>
        new(name, DiagnosticArgumentType.Decimal, CanonicalDecimal.Format(value));

    private static DomainRuleException Error(string code, string key, params DiagnosticArgument[] args) =>
        new(new DomainDiagnostic(code, key, args));
}
