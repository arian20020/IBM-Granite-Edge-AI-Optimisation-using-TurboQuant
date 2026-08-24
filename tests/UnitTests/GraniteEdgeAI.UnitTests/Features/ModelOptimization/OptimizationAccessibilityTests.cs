using GraniteEdgeAI.Features.ModelOptimization;
using GraniteEdgeAI.Features.ModelOptimization.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.Linq;

namespace GraniteEdgeAI.UnitTests.Features.ModelOptimization;

[TestClass]
public sealed class OptimizationAccessibilityTests
{
    [UITestMethod]
    public void InteractiveControlsHaveNamesFocusAndFullWidthTargets()
    {
        OptimizationPage page = new();
        OptimizationPreferenceCard preference = (OptimizationPreferenceCard)page.FindName("PreferenceCard");
        OptimizationConfigurationCard configuration = (OptimizationConfigurationCard)page.FindName("ConfigurationCard");
        Slider slider = (Slider)preference.FindName("PreferenceSlider");
        Expander details = (Expander)configuration.FindName("TechnicalDetailsDisclosure");

        Assert.AreEqual("Model preference", AutomationProperties.GetName(slider));
        Assert.IsTrue(slider.UseSystemFocusVisuals);
        Assert.IsTrue(slider.MinHeight >= 44);
        Assert.AreEqual("Technical configuration details", AutomationProperties.GetName(details));
        Assert.AreEqual(HorizontalAlignment.Stretch, details.HorizontalAlignment);
        Assert.IsTrue(details.UseSystemFocusVisuals);

        ResourceDictionary theme = page.Resources.MergedDictionaries.Single();
        Assert.IsTrue(theme.ThemeDictionaries.ContainsKey("HighContrast"));
    }
}
