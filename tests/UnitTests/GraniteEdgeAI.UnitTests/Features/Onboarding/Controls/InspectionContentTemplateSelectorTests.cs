using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests;

/// <summary>
/// Verifies that each WinUI selector overload returns the intended template.
/// </summary>
[TestClass]
public sealed class InspectionContentTemplateSelectorTests
{
    /// <summary>
    /// Protects the ContentControl route that previously returned no template
    /// and displayed the presentation type name.
    /// </summary>
    [UITestMethod]
    [TestCategory("WinUI")]
    public void SelectTemplate_WithContainerAndProgressMode_ReturnsProgressTemplate()
    {
        // Create two distinct templates so the selected result is unambiguous.
        var progressTemplate = new DataTemplate();
        var findingsTemplate = new DataTemplate();

        // Configure the selector exactly as InspectionContentCard.xaml does.
        var selector = new InspectionContentTemplateSelector
        {
            ProgressTemplate = progressTemplate,
            FindingsTemplate = findingsTemplate
        };

        // Create the same mode used by the initial inspection page.
        var presentation = new InspectionContentCardPresentation
        {
            Mode = InspectionContentCardMode.Progress
        };

        // Exercise the item-plus-container route used by ContentControl.
        DataTemplate selectedTemplate = selector.SelectTemplate(
            presentation,
            new ContentControl());

        // The progress template must be selected rather than null.
        Assert.AreSame(
            progressTemplate,
            selectedTemplate);
    }

    /// <summary>
    /// Verifies the item-only selector route as well.
    /// </summary>
    [UITestMethod]
    [TestCategory("WinUI")]
    public void SelectTemplate_WithoutContainerAndProgressMode_ReturnsProgressTemplate()
    {
        // Create two distinct template instances.
        var progressTemplate = new DataTemplate();
        var findingsTemplate = new DataTemplate();

        var selector = new InspectionContentTemplateSelector
        {
            ProgressTemplate = progressTemplate,
            FindingsTemplate = findingsTemplate
        };

        var presentation = new InspectionContentCardPresentation
        {
            Mode = InspectionContentCardMode.Progress
        };

        // Exercise the second public selector route.
        DataTemplate selectedTemplate =
            selector.SelectTemplate(presentation);

        Assert.AreSame(
            progressTemplate,
            selectedTemplate);
    }
}