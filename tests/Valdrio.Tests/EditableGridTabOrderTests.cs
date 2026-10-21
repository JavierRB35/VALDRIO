using Microsoft.VisualStudio.TestTools.UnitTesting;
using Valdrio.Desktop;

namespace Valdrio.Tests;

[TestClass]
public sealed class EditableGridTabOrderTests
{
    [TestMethod]
    public void Forward_SkipsPreviousColumnsAndTraversesNextRow()
    {
        var result = EditableGridTabOrder.After(2, 4, 0, 2, backwards: false).ToArray();
        Assert.IsTrue(result.SequenceEqual(new[] { (0, 3), (1, 0), (1, 1), (1, 2), (1, 3) }));
    }

    [TestMethod]
    public void Backward_TraversesPreviousRowInReverseOrder()
    {
        var result = EditableGridTabOrder.After(2, 4, 1, 1, backwards: true).ToArray();
        Assert.IsTrue(result.SequenceEqual(new[] { (1, 0), (0, 3), (0, 2), (0, 1), (0, 0) }));
    }

    [TestMethod]
    public void AtTheEdge_LeavesNoInternalCells()
    {
        Assert.AreEqual(0, EditableGridTabOrder.After(2, 4, 1, 3, false).Count());
        Assert.AreEqual(0, EditableGridTabOrder.After(2, 4, 0, 0, true).Count());
    }

    [TestMethod]
    public void EmptyOrInvalidPosition_HasNoCandidates()
    {
        Assert.AreEqual(0, EditableGridTabOrder.After(0, 3, 0, 0, false).Count());
        Assert.AreEqual(0, EditableGridTabOrder.After(2, 0, 0, 0, false).Count());
        Assert.AreEqual(0, EditableGridTabOrder.After(2, 3, -1, 0, false).Count());
        Assert.AreEqual(0, EditableGridTabOrder.After(2, 3, 0, 4, false).Count());
    }

    [TestMethod]
    public void Forward_FromIncludedCheckbox_VisitsWeightThenNextRowCheckbox()
    {
        var cells = EditableGridTabOrder.After(3, 4, 0, 0, backwards: false)
            .Where(x => x.Column is 0 or 3).Take(3).ToArray();
        CollectionAssert.AreEqual(new[] { (0, 3), (1, 0), (1, 3) }, cells);
    }

    [TestMethod]
    public void Backward_FromWeight_VisitsCheckboxOfSameRowThenPriorWeight()
    {
        var cells = EditableGridTabOrder.After(3, 4, 1, 3, backwards: true)
            .Where(x => x.Column is 0 or 3).Take(3).ToArray();
        CollectionAssert.AreEqual(new[] { (1, 0), (0, 3), (0, 0) }, cells);
    }
}
