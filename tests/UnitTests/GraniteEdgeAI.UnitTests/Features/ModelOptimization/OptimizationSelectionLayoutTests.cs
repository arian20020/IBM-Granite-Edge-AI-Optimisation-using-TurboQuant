using GraniteEdgeAI.Features.ModelImport.ModelDownload;
using GraniteEdgeAI.Features.ModelOptimization;
using GraniteEdgeAI.Features.ModelOptimization.Controls;
using GraniteEdgeAI.Features.ModelOptimization.DebugFixtures;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests.Features.ModelOptimization;

[TestClass]
public sealed class OptimizationSelectionLayoutTests
{
    [UITestMethod]
    public void SelectionUsesSeparateAutomaticAndExactPreferenceControl()
    {
        OptimizationPage page = new();
        page.ApplyPresentation(OptimizationFixtureCatalog.All[0].Presentation);
        OptimizationPreferenceCard preferenceCard =
            (OptimizationPreferenceCard)page.FindName("PreferenceCard");
        OptimizationConfigurationCard configurationCard =
            (OptimizationConfigurationCard)page.FindName("ConfigurationCard");

        Assert.AreEqual("Automatic", ((TextBlock)preferenceCard.FindName("AutomaticTitle")).Text);
        Assert.AreEqual(
            "Maximum efficiency",
            ((TextBlock)preferenceCard.FindName("MinimumPreferenceLabel")).Text);
        Assert.AreEqual(
            "Maximum capability",
            ((TextBlock)preferenceCard.FindName("MaximumPreferenceLabel")).Text);
        Assert.IsInstanceOfType<ModelPreferenceSlider>(
            preferenceCard.FindName("PreferenceSlider"));
        Assert.AreEqual(
            "No",
            ((TextBlock)configurationCard.FindName("ConfigurationNewModelCopyValue")).Text);
    }
}
