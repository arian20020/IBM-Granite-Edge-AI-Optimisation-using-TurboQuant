using GraniteEdgeAI.Features.ModelInspection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection;

[TestClass]
public sealed class OpenVinoPromptSurfaceTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public void SharedPageOwnsOneAccessibleRouteNeutralPromptSurface()
    {
        ModelInspectionPage page = new();
        Border surface = Assert.IsInstanceOfType<Border>(
            page.FindName("PromptSurface"));
        TextBox prompt = Assert.IsInstanceOfType<TextBox>(
            page.FindName("PromptInput"));
        Button send = Assert.IsInstanceOfType<Button>(page.FindName("PromptSendButton"));
        Button stop = Assert.IsInstanceOfType<Button>(page.FindName("PromptStopButton"));
        Button cancel = Assert.IsInstanceOfType<Button>(page.FindName("PromptCancelButton"));
        TextBlock response = Assert.IsInstanceOfType<TextBlock>(
            page.FindName("PromptResponseText"));
        TextBlock capability = Assert.IsInstanceOfType<TextBlock>(
            page.FindName("PromptCapabilitySummary"));
        TextBlock executionEvidence = Assert.IsInstanceOfType<TextBlock>(
            page.FindName("PromptExecutionEvidenceText"));
        TextBlock buildEvidence = Assert.IsInstanceOfType<TextBlock>(
            page.FindName("PromptBuildEvidenceText"));

        Assert.AreEqual(Visibility.Collapsed, surface.Visibility);
        Assert.AreEqual("Prompt for the local model", AutomationProperties.GetName(prompt));
        Assert.AreEqual("Send prompt", AutomationProperties.GetName(send));
        Assert.AreEqual("Stop generation", AutomationProperties.GetName(stop));
        Assert.AreEqual("Cancel local session", AutomationProperties.GetName(cancel));
        Assert.AreEqual(AutomationLiveSetting.Polite,
            AutomationProperties.GetLiveSetting(response));
        Assert.AreEqual(string.Empty, capability.Text);
        Assert.AreEqual(
            "Requested and actual local execution device",
            AutomationProperties.GetName(executionEvidence));
        Assert.AreEqual(
            "Verified local runtime build evidence",
            AutomationProperties.GetName(buildEvidence));
        Assert.IsTrue(prompt.AcceptsReturn);
        Assert.AreEqual(TextWrapping.Wrap, prompt.TextWrapping);
        Assert.IsTrue(prompt.UseSystemFocusVisuals);
        Assert.IsTrue(send.UseSystemFocusVisuals);
        Assert.IsTrue(stop.UseSystemFocusVisuals);
        Assert.IsTrue(cancel.UseSystemFocusVisuals);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void PromptSurfaceReusesInspectionCardThemeAndScalesNaturally()
    {
        ModelInspectionPage page = new();
        Border surface = Assert.IsInstanceOfType<Border>(page.FindName("PromptSurface"));
        TextBox prompt = Assert.IsInstanceOfType<TextBox>(page.FindName("PromptInput"));

        Assert.AreEqual(
            Application.Current.Resources["InspectionCardCornerRadius"],
            surface.CornerRadius);
        Assert.AreEqual(
            Application.Current.Resources["InspectionCardPadding"],
            surface.Padding);
        Assert.IsTrue(prompt.IsTextScaleFactorEnabled);
        Assert.IsTrue(double.IsNaN(prompt.Height));
    }
}
