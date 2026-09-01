using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.OpenVinoRoute;
using GraniteEdgeAI.Features.Prompting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.Reflection;
using System.Threading;
using Windows.System;

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

    [UITestMethod]
    [TestCategory("WinUI")]
    public void PromptKeyboardGestureSendsOnEnterAndKeepsShiftEnterForNewlines()
    {
        MethodInfo? method = typeof(ModelInspectionPage).GetMethod(
            "IsPromptSendKey",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(method);

        Assert.IsTrue(InvokePromptSendKey(method, VirtualKey.Enter, false));
        Assert.IsFalse(InvokePromptSendKey(method, VirtualKey.Enter, true));
        Assert.IsFalse(InvokePromptSendKey(method, VirtualKey.Space, false));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void InspectionStartupFailureUsesInspectionCardsAndNeverPromptSurface()
    {
        ModelInspectionPage page = new();
        MethodInfo? method = typeof(ModelInspectionPage).GetMethod(
            "ApplyOpenVinoInspectionFailurePresentation",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);

        method.Invoke(page, new object[]
        {
            "runtime_load_failed",
            "The verified OpenVINO worker is unavailable.",
            "Repair or reinstall the app, then retry."
        });

        Border promptSurface = Assert.IsInstanceOfType<Border>(
            page.FindName("PromptSurface"));
        InspectionContentCard content = Assert.IsInstanceOfType<InspectionContentCard>(
            page.FindName("InspectionContentCardControl"));
        InspectionActionCard action = Assert.IsInstanceOfType<InspectionActionCard>(
            page.FindName("InspectionActionCardControl"));

        Assert.AreEqual(Visibility.Collapsed, promptSurface.Visibility);
        Assert.AreEqual(InspectionContentCardMode.OperationalFailure,
            content.Presentation.Mode);
        Assert.AreEqual("runtime_load_failed", content.Presentation.DiagnosticCode);
        Assert.AreEqual(Visibility.Visible,
            content.Presentation.DiagnosticCodeVisibility);
        Assert.AreEqual(InspectionActionCardMode.Result, action.Presentation.Mode);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task RetiredOpenVinoInspectionCannotPublishTerminalResult()
    {
        ModelInspectionPage page = new();
        MethodInfo inspecting = RequirePrivateMethod(
            "ApplyOpenVinoInspectingPresentation");
        MethodInfo tryPublish = RequirePrivateMethod(
            "TryApplyOpenVinoInspectionResult");
        FieldInfo lifetime = RequirePrivateField("_openVinoLifetime");
        FieldInfo cancellation = RequirePrivateField("_openVinoCancellation");
        using CancellationTokenSource cancellationSource = new();

        inspecting.Invoke(page, new object[] { "granite-openvino" });
        lifetime.SetValue(page, 7L);
        cancellation.SetValue(page, cancellationSource);
        await page.RetireOpenVinoInspectionAsync();

        OpenVinoRouteInspectionResult staleResult = new(
            OpenVinoRouteInspectionOutcome.DependencyUnavailable,
            HandoffLease: null,
            Failure: new PromptFailure(
                "runtime_dependency_missing",
                "The local OpenVINO operation could not continue.",
                "Retry model inspection."),
            Configuration: null);
        bool published = Assert.IsInstanceOfType<bool>(tryPublish.Invoke(
            page,
            new object[] { 7L, staleResult }));

        InspectionContentCard content = Assert.IsInstanceOfType<InspectionContentCard>(
            page.FindName("InspectionContentCardControl"));
        Assert.IsFalse(published);
        Assert.AreEqual(InspectionContentCardMode.Progress,
            content.Presentation.Mode);
        Assert.AreEqual(Visibility.Collapsed,
            ((Border)page.FindName("PromptSurface")).Visibility);
    }

    private static bool InvokePromptSendKey(
        MethodInfo method,
        VirtualKey key,
        bool isShiftPressed) =>
        Assert.IsInstanceOfType<bool>(method.Invoke(
            null,
            new object[] { key, isShiftPressed }));

    private static MethodInfo RequirePrivateMethod(string name)
    {
        MethodInfo? method = typeof(ModelInspectionPage).GetMethod(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        return method;
    }

    private static FieldInfo RequirePrivateField(string name)
    {
        FieldInfo? field = typeof(ModelInspectionPage).GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        return field;
    }
}
