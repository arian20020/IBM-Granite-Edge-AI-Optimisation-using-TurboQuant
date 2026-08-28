using GraniteEdgeAI.Features.ModelImport.ModelDownload;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class ModelDownloadCardTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public void Constructor_DisplaysBalancedPreference()
    {
        var card = new ModelDownloadCard();

        Assert.AreEqual(
            50d,
            ((Slider)card.FindName("ModelScaleSlider")).Value);
        Assert.AreEqual(
            "Balanced",
            ((TextBlock)card.FindName("ModelScaleValueText")).Text);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Constructor_ConfiguresTextForNarrowLayouts()
    {
        var card = new ModelDownloadCard();
        string[] wrappingTextNames =
        [
            "ModelNameText",
            "ModelPackageSummaryText",
            "ModelDescriptionText",
            "EstimatedMemoryValueText",
            "ContextLengthValueText",
            "EfficiencyEndpointText",
            "CapabilityEndpointText"
        ];

        foreach (string elementName in wrappingTextNames)
        {
            var text = (TextBlock)card.FindName(elementName);
            Assert.AreEqual(TextWrapping.WrapWholeWords, text.TextWrapping);
            Assert.AreEqual(TextTrimming.None, text.TextTrimming);
        }

        Assert.IsNotNull(card.FindName("ModelHeaderGrid"));
        Assert.IsNotNull(card.FindName("ModelFactsGrid"));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void PreferenceSlider_MapsEveryBoundaryToExpectedLabel()
    {
        var card = new ModelDownloadCard();
        var slider = (Slider)card.FindName("ModelScaleSlider");
        var label = (TextBlock)card.FindName("ModelScaleValueText");
        (double Value, string Label)[] expectations =
        [
            (0, "Maximum efficiency"),
            (20, "Efficient"),
            (40, "Balanced"),
            (60, "High capability"),
            (80, "Maximum capability"),
            (100, "Maximum capability")
        ];

        foreach ((double value, string expectedLabel) in expectations)
        {
            slider.Value = value;
            Assert.AreEqual(expectedLabel, label.Text);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void PreferenceSlider_UpdatesPinnedArtifactDetails()
    {
        var card = new ModelDownloadCard();
        var slider = (Slider)card.FindName("ModelScaleSlider");
        var quantisation = (TextBlock)card.FindName("QuantizationValueText");
        var summary = (TextBlock)card.FindName("ModelPackageSummaryText");

        slider.Value = 0;
        Assert.AreEqual("Q2_K", quantisation.Text);
        Assert.IsTrue(summary.Text.Contains("1.23 GB", StringComparison.Ordinal));

        slider.Value = 100;
        Assert.AreEqual("Q8_0", quantisation.Text);
        Assert.IsTrue(summary.Text.Contains("3.40 GB", StringComparison.Ordinal));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Constructor_ProvidesAccessibleDownloadStatusControls()
    {
        var card = new ModelDownloadCard();

        Assert.IsNotNull(card.FindName("DownloadStatusRegion"));
        Assert.IsNotNull(card.FindName("DownloadStatusText"));
        Assert.IsNotNull(card.FindName("DownloadProgressBar"));
        Assert.IsNotNull(card.FindName("DownloadProgressText"));
        Assert.IsNotNull(card.FindName("DownloadActivityRing"));
        Assert.IsNotNull(card.FindName("DiscardDownloadButton"));
    }
}
