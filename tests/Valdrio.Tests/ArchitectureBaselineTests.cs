using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Tests;

[TestClass]
public sealed class ArchitectureBaselineTests
{
    [TestMethod]
    public void Domain_DoesNotReferenceCalculationUiOrEntityFramework()
    {
        var references = typeof(DomainAssemblyMarker).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.IsFalse(references.Any(name =>
            name.StartsWith("Valdrio.Calculation", StringComparison.Ordinal)
            || name.StartsWith("Valdrio.Infrastructure", StringComparison.Ordinal)
            || name.StartsWith("PresentationFramework", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Calculation_DoesNotReferenceUiInfrastructureOrEntityFramework()
    {
        var references = typeof(CalculationAssemblyMarker).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.IsFalse(references.Any(name =>
            name.StartsWith("Valdrio.Infrastructure", StringComparison.Ordinal)
            || name.StartsWith("Valdrio.Desktop", StringComparison.Ordinal)
            || name.StartsWith("Valdrio.Localization", StringComparison.Ordinal)
            || name.StartsWith("PresentationFramework", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)));
    }
}
