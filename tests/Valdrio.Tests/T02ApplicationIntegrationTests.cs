using Valdrio.Application;

namespace Valdrio.Tests;

[TestClass]
public sealed class T02ApplicationIntegrationTests
{
    [TestMethod]
    public void C07ExampleService_UsesCalculationResultAndStableIds()
    {
        var example = new T02RoundingExampleService().BuildC07();

        Assert.AreEqual("C07", example.ReferenceCase);
        Assert.AreEqual("0.01", example.SourceTotal);
        Assert.AreEqual("0.01", example.Quantum);
        Assert.AreEqual("0.01", example.PostedTotal);

        CollectionAssert.AreEqual(
            new[] { "A", "B", "C" },
            example.Rows.Select(row => row.DestinationId).ToArray());

        Assert.AreEqual(
            "0.01",
            example.Rows.Single(row => row.DestinationId == "A").PostedAmount);
        Assert.AreEqual(
            "0",
            example.Rows.Single(row => row.DestinationId == "B").PostedAmount);
        Assert.AreEqual(
            "0",
            example.Rows.Single(row => row.DestinationId == "C").PostedAmount);
    }
}
