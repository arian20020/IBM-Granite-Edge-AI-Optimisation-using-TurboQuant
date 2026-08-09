using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.Reflection;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class InspectionContentCardPresentationTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public void ExpansionState_IsAnImmutablePresentationSnapshot()
    {
        PropertyInfo property = typeof(InspectionContentCardPresentation)
            .GetProperty(nameof(InspectionContentCardPresentation.IsExpanded))!;
        var collapsed = new InspectionContentCardPresentation();
        var expanded = new InspectionContentCardPresentation
        {
            IsExpanded = true
        };

        Assert.IsNotNull(property.SetMethod);
        CollectionAssert.Contains(
            property.SetMethod.ReturnParameter.GetRequiredCustomModifiers(),
            typeof(System.Runtime.CompilerServices.IsExternalInit));
        Assert.IsFalse(collapsed.IsExpanded);
        Assert.IsTrue(expanded.IsExpanded);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Constructor_UsesIndependentHiddenPresentation()
    {
        InspectionContentCard first = new();
        InspectionContentCard second = new();

        Assert.AreNotSame(first.Presentation, second.Presentation);
        Assert.IsFalse(first.Presentation.IsExpanded);
        Assert.IsFalse(second.Presentation.IsExpanded);
    }
}
