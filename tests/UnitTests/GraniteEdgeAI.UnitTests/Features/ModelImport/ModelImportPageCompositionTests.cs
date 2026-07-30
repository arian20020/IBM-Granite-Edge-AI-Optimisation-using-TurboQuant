using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelImport.ModelDownload;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class ModelImportPageCompositionTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public void Constructor_ConfiguresHeadingForNarrowLayouts()
    {
        var page = new ModelImportPage();
        var heading = page.FindName("ImportPageHeading") as TextBlock;

        Assert.IsNotNull(heading);
        Assert.AreEqual(
            HorizontalAlignment.Stretch,
            heading.HorizontalAlignment);
        Assert.AreEqual(TextWrapping.WrapWholeWords, heading.TextWrapping);
        Assert.AreEqual(TextTrimming.None, heading.TextTrimming);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Constructor_EmbedsRecommendedModelCardWithoutChangingReadiness()
    {
        var page = new ModelImportPage();

        var downloadCard = page.FindName("RecommendedModelDownloadCard")
            as ModelDownloadCard;
        var continueButton = (Button)page.FindName(
            "ContinueToModelInspectionButton");

        Assert.IsNotNull(downloadCard);
        Assert.AreEqual(
            "Recommended model download option",
            AutomationProperties.GetName(downloadCard));
        Assert.IsNull(page.FindName("RecommendedModelDownloadButton"));
        Assert.IsFalse(page.HasValidatedModel);
        Assert.IsFalse(continueButton.IsEnabled);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void EmbeddedCardInteraction_DoesNotChangeLocalModelReadiness()
    {
        var page = new ModelImportPage();
        var downloadCard = (ModelDownloadCard)page.FindName(
            "RecommendedModelDownloadCard");
        var slider = (Slider)downloadCard.FindName("ModelScaleSlider");

        slider.Value = 85;

        Assert.IsNull(page.SelectedModelPath);
        Assert.IsNull(page.ValidatedScanResult);
        Assert.IsFalse(page.HasValidatedModel);
        Assert.IsFalse(
            ((Button)page.FindName(
                "ContinueToModelInspectionButton")).IsEnabled);
    }
}
