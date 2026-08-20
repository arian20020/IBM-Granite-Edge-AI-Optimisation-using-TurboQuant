using GraniteEdgeAI.Features.ModelImport.Selection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class SourceModelSelectionPreflightTests
{
    [TestMethod]
    public void ConstructedPreflight_PreservesOnlyTheBoundedSourceShape()
    {
        var preflight = new SourceModelSelectionPreflight(
            HasRootConfig: true,
            HasWeightFileOrIndex: true,
            HasCompleteIndexedShards: true,
            RequiresCustomCode: false);

        Assert.IsTrue(preflight.HasRootConfig);
        Assert.IsTrue(preflight.HasWeightFileOrIndex);
        Assert.IsTrue(preflight.HasCompleteIndexedShards);
        Assert.IsFalse(preflight.RequiresCustomCode);
    }
}
