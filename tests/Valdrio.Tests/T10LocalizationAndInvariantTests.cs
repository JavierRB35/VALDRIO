using System.Globalization;
using Valdrio.Calculation;
using Valdrio.Domain;
using Valdrio.Localization;

namespace Valdrio.Tests;

[TestClass]
public sealed class T10LocalizationAndInvariantTests
{
    [TestMethod]
    public void AllReciprocalDiagnosticsExistInBothLanguages()
    {
        var catalog = new UiTextCatalog();
        var english = catalog.ReadAll(CultureInfo.GetCultureInfo("en"));
        var spanish = catalog.ReadAll(CultureInfo.GetCultureInfo("es"));
        var keys = new[]
        {
            "SchemaInvalid", "WeightInvalid", "SelfNotAllowed", "CoverageInvalid",
            "ClosedComponent", "Singular", "Sensitive", "NegativeFactor",
            "FactorDrift", "Residual", "NumericOverflow", "RhsInvalid", "AmplificationWarning"
        };
        foreach (var suffix in keys)
        {
            var key = "Reciprocal." + suffix;
            Assert.IsTrue(english.TryGetValue(key, out var en) && !string.IsNullOrWhiteSpace(en), "EN " + key);
            Assert.IsTrue(spanish.TryGetValue(key, out var es) && !string.IsNullOrWhiteSpace(es), "ES " + key);
        }
        var uiKeys = new[]
        {
            "ReciprocalTitle", "ReciprocalHelp", "CheckReciprocal",
            "ReciprocalNeedStudy", "ReciprocalNotChecked", "ReciprocalNoAuxiliaries",
            "ReciprocalSummary", "ReciprocalGroup", "ReciprocalStatus",
            "ReciprocalDetail", "ReciprocalViable", "ReciprocalBlocked",
            "ReciprocalInvalid", "ReciprocalMetrics", "ReciprocalNoPosting"
        };
        foreach (var suffix in uiKeys)
        {
            var key = "AllocationUI." + suffix;
            Assert.IsTrue(english.TryGetValue(key, out var en) && !string.IsNullOrWhiteSpace(en), "EN " + key);
            Assert.IsTrue(spanish.TryGetValue(key, out var es) && !string.IsNullOrWhiteSpace(es), "ES " + key);
        }
        var blocked = Assert.ThrowsExactly<DomainRuleException>(() =>
            ReciprocalFactorization.Prepare(new ReciprocalInput(
                ["A"], ["P"], [[0.1m]], [[0.9m]], false)));
        var formatter = new DomainDiagnosticFormatter(catalog);
        var esDiagnostic = formatter.Format(blocked.Diagnostic, CultureInfo.GetCultureInfo("es"), CultureInfo.GetCultureInfo("es-ES"));
        var enDiagnostic = formatter.Format(blocked.Diagnostic, CultureInfo.GetCultureInfo("en"), CultureInfo.GetCultureInfo("en-US"));
        Assert.AreEqual("GRAPH_SELF_NOT_ALLOWED", esDiagnostic.Code);
        Assert.AreEqual(esDiagnostic.Code, enDiagnostic.Code);
        Assert.AreNotEqual(esDiagnostic.Message, enDiagnostic.Message);
        Assert.IsTrue(esDiagnostic.Message.Contains('A'));
    }

    [TestMethod]
    public void UiCultureAndFormatCultureCannotChangeEconomicOutput()
    {
        var before = CultureInfo.CurrentCulture;
        var beforeUi = CultureInfo.CurrentUICulture;
        try
        {
            var input = new ReciprocalInput(["B", "A"], ["P2", "P1"],
                [[0m, 0.2m], [0.1m, 0m]], [[0.5m, 0.3m], [0.3m, 0.6m]], false);
            var rhs = new Dictionary<string, decimal> { ["A"] = 1000m, ["B"] = -1000m };
            var terminals = new Dictionary<string, decimal> { ["P1"] = 0m, ["P2"] = 0m };
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var en = ReciprocalFactorization.Prepare(input).Solve(rhs, terminals);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("es");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es-ES");
            var es = ReciprocalFactorization.Prepare(input).Solve(rhs, terminals);
            CollectionAssert.AreEqual(en.Gross, es.Gross);
            CollectionAssert.AreEqual(en.Final, es.Final);
            Assert.AreEqual(en.AbsoluteResidual, es.AbsoluteResidual);
            Assert.AreEqual(en.MaxCirculationAmplification, es.MaxCirculationAmplification);
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
            CultureInfo.CurrentUICulture = beforeUi;
        }
    }
}
