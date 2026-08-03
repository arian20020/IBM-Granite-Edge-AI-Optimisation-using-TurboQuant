using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests;

/// <summary>
/// Verifies that the inspection-content selector handles every WinUI entry route
/// used while the ContentControl is created and updated.
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
        var progressTemplate = new DataTemplate();
        var findingsTemplate = new DataTemplate();
        var selector = CreateSelector(progressTemplate, findingsTemplate);
        var presentation = CreateProgressPresentation();

        DataTemplate selectedTemplate = selector.SelectTemplate(
            presentation,
            new ContentControl());

        Assert.AreSame(progressTemplate, selectedTemplate);
    }

    /// <summary>
    /// Verifies the item-only selector route used by controls that do not
    /// provide a separate container argument.
    /// </summary>
    [UITestMethod]
    [TestCategory("WinUI")]
    public void SelectTemplate_WithoutContainerAndProgressMode_ReturnsProgressTemplate()
    {
        var progressTemplate = new DataTemplate();
        var findingsTemplate = new DataTemplate();
        var selector = CreateSelector(progressTemplate, findingsTemplate);
        var presentation = CreateProgressPresentation();

        DataTemplate selectedTemplate = selector.SelectTemplate(presentation);

        Assert.AreSame(progressTemplate, selectedTemplate);
    }

    /// <summary>
    /// Reproduces the WinUI bootstrap call that can occur before the compiled
    /// Content binding has supplied its presentation object.
    /// </summary>
    [UITestMethod]
    [TestCategory("WinUI")]
    public void SelectTemplate_WithNullBootstrapItem_ReturnsProgressTemplate()
    {
        var progressTemplate = new DataTemplate();
        var findingsTemplate = new DataTemplate();
        var selector = CreateSelector(progressTemplate, findingsTemplate);

        DataTemplate selectedTemplate = selector.SelectTemplate(
            null!,
            new ContentControl());

        Assert.AreSame(progressTemplate, selectedTemplate);
    }

    /// <summary>
    /// Verifies the framework route where the ContentControl itself is supplied
    /// as the selector item and the presentation is stored in its Content.
    /// </summary>
    [UITestMethod]
    [TestCategory("WinUI")]
    public void SelectTemplate_WithContentControlAsItem_UsesControlContent()
    {
        var progressTemplate = new DataTemplate();
        var findingsTemplate = new DataTemplate();
        var selector = CreateSelector(progressTemplate, findingsTemplate);
        var contentControl = new ContentControl
        {
            Content = CreateProgressPresentation()
        };

        DataTemplate selectedTemplate = selector.SelectTemplate(contentControl);

        Assert.AreSame(progressTemplate, selectedTemplate);
    }

    /// <summary>
    /// Confirms that completed non-ready states continue to use the shared
    /// findings layout after the bootstrap handling is added.
    /// </summary>
    [UITestMethod]
    [TestCategory("WinUI")]
    public void SelectTemplate_WithWarningMode_ReturnsFindingsTemplate()
    {
        var progressTemplate = new DataTemplate();
        var findingsTemplate = new DataTemplate();
        var selector = CreateSelector(progressTemplate, findingsTemplate);
        var presentation = new InspectionContentCardPresentation
        {
            Mode = InspectionContentCardMode.Warnings
        };

        DataTemplate selectedTemplate = selector.SelectTemplate(presentation);

        Assert.AreSame(findingsTemplate, selectedTemplate);
    }

    private static InspectionContentTemplateSelector CreateSelector(
        DataTemplate progressTemplate,
        DataTemplate findingsTemplate)
    {
        return new InspectionContentTemplateSelector
        {
            ProgressTemplate = progressTemplate,
            FindingsTemplate = findingsTemplate
        };
    }

    private static InspectionContentCardPresentation CreateProgressPresentation()
    {
        return new InspectionContentCardPresentation
        {
            Mode = InspectionContentCardMode.Progress
        };
    }
}
