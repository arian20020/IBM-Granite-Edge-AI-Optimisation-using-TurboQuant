using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class InspectionContentCardPresentationTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public void Hidden_ReturnsIndependentExpansionState()
    {
        InspectionContentCardPresentation first =
            InspectionContentCardPresentation.Hidden;
        InspectionContentCardPresentation second =
            InspectionContentCardPresentation.Hidden;

        first.IsExpanded = true;

        Assert.AreNotSame(first, second);
        Assert.IsFalse(second.IsExpanded);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Constructor_UsesIndependentHiddenPresentation()
    {
        InspectionContentCard first = new();
        InspectionContentCard second = new();

        first.Presentation.IsExpanded = true;

        Assert.AreNotSame(first.Presentation, second.Presentation);
        Assert.IsFalse(second.Presentation.IsExpanded);
    }
}
