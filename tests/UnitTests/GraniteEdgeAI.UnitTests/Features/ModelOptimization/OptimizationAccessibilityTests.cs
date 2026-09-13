using GraniteEdgeAI.Features.ModelOptimization;
using GraniteEdgeAI.Features.ModelOptimization.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual;
using GraniteEdgeAI.UnitTests.Features.ModelOptimization.Fixtures;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace GraniteEdgeAI.UnitTests.Features.ModelOptimization;

[TestClass]
public sealed class OptimizationAccessibilityTests
{
    [UITestMethod]
    public async Task InteractiveControlsHaveNamesFocusAndFullWidthTargets()
    {
        OptimizationPage page = new();
        page.ApplyPresentation(OptimizationFixtureCatalog.All.Single(
            item => item.Id == "confirmation").Presentation);
        await using WinUiRenderHost host =
            await WinUiRenderHost.ShowAsync(page, 900, 700);
        Expander details = (Expander)page.FindName("OptimizationTechnicalDetailsExpander");
        Expander terminalDetails =
            (Expander)page.FindName("OptimizationTerminalDetailsExpander");

        Assert.AreEqual("Technical details", AutomationProperties.GetName(details));
        Assert.AreEqual(HorizontalAlignment.Stretch, details.HorizontalAlignment);
        Assert.IsFalse(details.IsTabStop);
        Assert.AreEqual("Technical details",
            AutomationProperties.GetName(terminalDetails));
        Assert.AreEqual(HorizontalAlignment.Stretch,
            terminalDetails.HorizontalAlignment);
        Assert.IsFalse(terminalDetails.IsTabStop);

        Assert.AreEqual(Visibility.Visible,
            ((Grid)page.FindName("OptimizationConfirmingPanel")).Visibility);
        AssertNativeExpanderHeader(details);

        page.ApplyPresentation(OptimizationFixtureCatalog.All.Single(
            item => item.Id == "success-persistent").Presentation);
        _ = await host.CaptureAsync();
        Assert.AreEqual(Visibility.Visible,
            ((Grid)page.FindName("OptimizationTerminalPanel")).Visibility);
        Assert.AreEqual(Visibility.Collapsed, terminalDetails.Visibility);

        Button import = (Button)page.FindName("BtnOptimizationTerminalBack");
        Assert.AreEqual("Import another model", AutomationProperties.GetName(import));
        Assert.AreEqual("OptimizationAction.ImportAnotherModel",
            AutomationProperties.GetAutomationId(import));

        page.ApplyPresentation(OptimizationFixtureCatalog.All.Single(
            item => item.Id == "failed").Presentation);
        Assert.AreEqual(Visibility.Visible, terminalDetails.Visibility);
        AssertNativeExpanderHeader(terminalDetails);

        Assert.AreEqual("OptimizationExport.Progress",
            AutomationProperties.GetAutomationId(
                (ProgressBar)page.FindName("OptimizationExportProgress")));
        Assert.AreEqual("OptimizationExport.Cancel",
            AutomationProperties.GetAutomationId(
                (Button)page.FindName("BtnCancelExport")));
        Assert.AreEqual("OptimizationExport.Retry",
            AutomationProperties.GetAutomationId(
                (Button)page.FindName("BtnRetryExport")));
        Assert.AreEqual(AutomationLiveSetting.Polite,
            AutomationProperties.GetLiveSetting(
                (TextBlock)page.FindName("OptimizationExportStatus")));
        ResourceDictionary theme = page.Resources.MergedDictionaries.Single();
        Assert.IsTrue(theme.ThemeDictionaries.ContainsKey("HighContrast"));
    }

    [UITestMethod]
    public void GgufRuntimeSuccessKeepsTruthfulNamedActionsWhileExportIsUnbound()
    {
        OptimizationPage page = new();
        page.ApplyPresentation(OptimizationFixtureCatalog.All.Single(
            item => item.Id == "success-runtime-profile").Presentation);

        Button import = (Button)page.FindName("BtnOptimizationTerminalBack");
        Button save = (Button)page.FindName("BtnOptimizationAlternative");
        Button chat = (Button)page.FindName("BtnOptimizationPrimary");
        Assert.AreEqual("Import another model",
            AutomationProperties.GetName(import));
        Assert.AreEqual("Save model and settings",
            AutomationProperties.GetName(save));
        Assert.AreEqual("Chat with this model",
            AutomationProperties.GetName(chat));
        Assert.IsFalse(save.IsEnabled);
        Assert.AreEqual(Visibility.Collapsed,
            ((FrameworkElement)page.FindName("OptimizationExportPanel")).Visibility);
        Assert.AreEqual(AutomationLiveSetting.Polite,
            AutomationProperties.GetLiveSetting(
                (TextBlock)page.FindName("OptimizationExportStatus")));
    }

    [UITestMethod]
    public void OpenVinoRuntimeSuccessRetainsEnabledDoneActionWhileExportIsUnbound()
    {
        OptimizationPresentationState seed = OptimizationFixtureCatalog.All.Single(
            item => item.Id == "success-runtime-profile").Presentation;
        OptimizationPage page = new();
        page.ApplyPresentation(OptimizationPresentationFactory.Success(
            seed.Preference!, seed.Configuration, route: OptimizationRoute.OpenVino));

        Button alternative =
            (Button)page.FindName("BtnOptimizationAlternative");
        Assert.AreEqual("Done", alternative.Content);
        Assert.AreEqual(OptimizationCommand.Done, alternative.Tag);
        Assert.IsTrue(alternative.IsEnabled);
        Assert.AreEqual(Visibility.Collapsed,
            ((FrameworkElement)page.FindName("OptimizationExportPanel")).Visibility);
    }

    private static void AssertNativeExpanderHeader(Expander expander)
    {
        expander.IsExpanded = true;
        expander.UpdateLayout();
        expander.ApplyTemplate();
        FrameworkElement? header = FindDescendantByAutomationId(
            expander, "ExpanderToggleButton", new HashSet<DependencyObject>());
        Assert.IsNotNull(header);
        Assert.IsInstanceOfType<ToggleButton>(header);
        Assert.IsTrue(((ToggleButton)header!).UseSystemFocusVisuals);
        AutomationPeer? peer = FrameworkElementAutomationPeer.CreatePeerForElement(header);
        Assert.IsNotNull(peer);
        Assert.AreEqual("ExpanderToggleButton", peer!.GetAutomationId());
        _ = peer.GetAutomationControlType();
        AutomationPeer? expanderPeer =
            FrameworkElementAutomationPeer.CreatePeerForElement(expander);
        Assert.IsNotNull(expanderPeer);
        int visitedPeerCount = 0;
        AssertAutomationPeerTreeIsAcyclic(
            expanderPeer!, new HashSet<AutomationPeer>(), 0,
            ref visitedPeerCount);
    }

    private static FrameworkElement? FindDescendantByAutomationId(
        DependencyObject root,
        string automationId,
        HashSet<DependencyObject> visited)
    {
        Assert.IsTrue(visited.Add(root), "The expander visual tree must be acyclic.");
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int index = 0; index < count; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is FrameworkElement element
                && string.Equals(AutomationProperties.GetAutomationId(element),
                    automationId, System.StringComparison.Ordinal))
            {
                return element;
            }
            FrameworkElement? nested = FindDescendantByAutomationId(
                child, automationId, visited);
            if (nested is not null) return nested;
        }
        return null;
    }

    private static void AssertAutomationPeerTreeIsAcyclic(
        AutomationPeer peer,
        HashSet<AutomationPeer> ancestors,
        int depth,
        ref int visitedCount)
    {
        Assert.IsTrue(depth <= 32, "The expander UIA tree exceeded its depth bound.");
        Assert.IsTrue(ancestors.Add(peer), "The expander UIA tree contains a cycle.");
        visitedCount++;
        Assert.IsTrue(visitedCount <= 256,
            "The expander UIA tree exceeded its node bound.");
        foreach (AutomationPeer child in peer.GetChildren() ?? [])
            AssertAutomationPeerTreeIsAcyclic(
                child, ancestors, depth + 1, ref visitedCount);
        ancestors.Remove(peer);
    }
}
