using GraniteEdgeAI.Features.ModelInspection.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using Windows.System;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls;

[TestClass]
[DoNotParallelize]
public sealed class InspectionDisclosureTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task FocusedPeer_OwnsExpandCollapseNameAndPageOwnedRequest()
    {
        var disclosure = CreateDisclosure();
        AutomationProperties.SetName(disclosure, "Inspection warning details");
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        disclosure.Loaded += (_, _) => loaded.TrySetResult(true);
        var window = new Window { Content = disclosure };

        try
        {
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.IsTrue(disclosure.Focus(FocusState.Keyboard));
            Assert.AreSame(
                disclosure,
                FocusManager.GetFocusedElement(disclosure.XamlRoot));
            AutomationPeer peer = FrameworkElementAutomationPeer
                .CreatePeerForElement(disclosure);
            Assert.IsNotNull(peer);
            IExpandCollapseProvider provider =
                Assert.IsInstanceOfType<IExpandCollapseProvider>(
                    peer.GetPattern(PatternInterface.ExpandCollapse));
            Assert.AreEqual("Inspection warning details", peer.GetName());
            var button = (Button)disclosure.FindName("DisclosureToggleButton");
            Assert.IsFalse(button.IsTabStop);
            Assert.AreEqual(
                AccessibilityView.Raw,
                AutomationProperties.GetAccessibilityView(button));
            List<bool> requests = [];
            disclosure.ToggleRequested += (_, args) => requests.Add(args.IsExpanded);

            provider.Expand();

            CollectionAssert.AreEqual(new[] { true }, requests);
            Assert.AreEqual(
                ExpandCollapseState.Collapsed,
                provider.ExpandCollapseState,
                "A request must not mutate page-owned disclosure state.");
            Assert.AreEqual(Visibility.Collapsed, disclosure.ViewportTarget.Visibility);
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void PreparedDirection_AllowsImmediateOppositeClickAndUiaRequests()
    {
        var disclosure = CreateDisclosure();
        var button = (Button)disclosure.FindName("DisclosureToggleButton");
        IInvokeProvider invoke = Assert.IsInstanceOfType<IInvokeProvider>(
            new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke));
        IExpandCollapseProvider provider = GetExpandCollapseProvider(disclosure);
        List<bool> requests = [];
        disclosure.ToggleRequested += (_, args) => requests.Add(args.IsExpanded);

        provider.Expand();
        disclosure.PrepareTargetState(isExpanded: true);
        invoke.Invoke();
        provider.Collapse();

        CollectionAssert.AreEqual(new[] { true, false, false }, requests);

        disclosure.PrepareTargetState(isExpanded: false);
        disclosure.CompleteTargetState(isExpanded: true);
        Assert.AreEqual(
            ExpandCollapseState.Collapsed,
            provider.ExpandCollapseState,
            "The stale expansion must not complete after the opposite request was prepared.");

        disclosure.PrepareTargetState(isExpanded: true);
        disclosure.CompleteTargetState(isExpanded: true);
        provider.Collapse();
        disclosure.PrepareTargetState(isExpanded: false);
        provider.Expand();

        CollectionAssert.AreEqual(
            new[] { true, false, false, false, true },
            requests);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task EnterAndSpace_RequireDisclosureFocusAndIgnoreAutoRepeat()
    {
        var reportAction = new Button { Content = "Report action" };
        var disclosure = new InspectionDisclosure
        {
            HeaderContent = new TextBlock { Text = "Inspection details" },
            ViewportContent = reportAction
        };
        List<bool> requests = [];
        disclosure.ToggleRequested += (_, args) => requests.Add(args.IsExpanded);
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        disclosure.Loaded += (_, _) => loaded.TrySetResult(true);
        var window = new Window { Content = disclosure };

        try
        {
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.IsTrue(disclosure.Focus(FocusState.Keyboard));
            Assert.IsTrue(disclosure.HandleKeyboardActivation(
                VirtualKey.Enter,
                repeatCount: 1,
                wasKeyDown: false));
            disclosure.PrepareTargetState(isExpanded: true);
            Assert.IsTrue(disclosure.HandleKeyboardActivation(
                VirtualKey.Space,
                repeatCount: 1,
                wasKeyDown: false));
            Assert.IsFalse(disclosure.HandleKeyboardActivation(
                VirtualKey.Space,
                repeatCount: 2,
                wasKeyDown: true));

            disclosure.PrepareTargetState(isExpanded: true);
            disclosure.CompleteTargetState(isExpanded: true);
            await Task.Yield();
            disclosure.UpdateLayout();
            Assert.IsTrue(reportAction.Focus(FocusState.Programmatic));
            Assert.IsFalse(disclosure.HandleKeyboardActivation(
                VirtualKey.Space,
                repeatCount: 1,
                wasKeyDown: false));
            Assert.IsFalse(disclosure.HandleKeyboardActivation(
                VirtualKey.Enter,
                repeatCount: 1,
                wasKeyDown: false));

            Assert.IsTrue(disclosure.Focus(FocusState.Keyboard));
            Assert.IsFalse(disclosure.HandleKeyboardActivation(
                VirtualKey.Space,
                repeatCount: 1,
                wasKeyDown: true));

            CollectionAssert.AreEqual(new[] { true, false }, requests);
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void AutomationPeer_ExposesAbsoluteExpandAndCollapseRequests()
    {
        var disclosure = CreateDisclosure();
        List<bool> requests = [];
        disclosure.ToggleRequested += (_, args) => requests.Add(args.IsExpanded);
        IExpandCollapseProvider provider = GetExpandCollapseProvider(disclosure);

        provider.Expand();
        disclosure.PrepareTargetState(isExpanded: true);
        disclosure.CompleteTargetState(isExpanded: true);
        provider.Collapse();

        CollectionAssert.AreEqual(new[] { true, false }, requests);
        Assert.AreEqual(ExpandCollapseState.Expanded, provider.ExpandCollapseState);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void PrepareAndComplete_RetainTargetsAndRejectOppositeCompletion()
    {
        var disclosure = CreateDisclosure();
        UIElement chevron = disclosure.ChevronTarget;
        FrameworkElement viewport = disclosure.ViewportTarget;

        disclosure.PrepareTargetState(isExpanded: true);

        Assert.AreSame(chevron, disclosure.ChevronTarget);
        Assert.AreSame(viewport, disclosure.ViewportTarget);
        Assert.AreEqual(Visibility.Visible, viewport.Visibility);
        Assert.IsFalse(viewport.IsHitTestVisible);

        disclosure.PrepareTargetState(isExpanded: false);
        disclosure.CompleteTargetState(isExpanded: true);

        Assert.AreEqual(
            Visibility.Visible,
            viewport.Visibility,
            "An opposite-direction stale completion must not remove the retained viewport.");

        disclosure.CompleteTargetState(isExpanded: false);

        Assert.AreEqual(Visibility.Collapsed, viewport.Visibility);
        Assert.AreEqual(0d, viewport.Opacity, 0.001);
        Assert.AreEqual(
            AccessibilityView.Raw,
            AutomationProperties.GetAccessibilityView(viewport));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task PreparedCollapse_SuppressesFocusableViewportSubtreeUntilExpansion()
    {
        var report = new ScrollViewer
        {
            IsTabStop = true,
            Content = new Button { Content = "Report action" }
        };
        AutomationProperties.SetAccessibilityView(report, AccessibilityView.Content);
        var disclosure = new InspectionDisclosure
        {
            HeaderContent = new TextBlock { Text = "Inspection details" },
            ViewportContent = report
        };
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        disclosure.Loaded += (_, _) => loaded.TrySetResult(true);
        var window = new Window { Content = disclosure };

        try
        {
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            disclosure.PrepareTargetState(isExpanded: true);
            disclosure.CompleteTargetState(isExpanded: true);
            Assert.IsTrue(report.IsEnabled);
            Assert.IsTrue(report.IsTabStop);
            Assert.AreEqual(
                AccessibilityView.Content,
                AutomationProperties.GetAccessibilityView(
                    disclosure.ViewportTarget));

            disclosure.PrepareTargetState(isExpanded: false);

            Assert.AreEqual(Visibility.Visible, disclosure.ViewportTarget.Visibility);
            Assert.IsFalse(disclosure.ViewportTarget.IsHitTestVisible);
            Assert.IsFalse(report.IsEnabled);
            Assert.IsFalse(report.IsTabStop);
            Assert.AreEqual(
                AccessibilityView.Raw,
                AutomationProperties.GetAccessibilityView(report));

            disclosure.CompleteTargetState(isExpanded: false);
            Assert.AreEqual(Visibility.Collapsed, disclosure.ViewportTarget.Visibility);

            disclosure.PrepareTargetState(isExpanded: true);
            Assert.IsFalse(report.IsEnabled);
            Assert.IsFalse(report.IsTabStop);
            Assert.AreEqual(
                AccessibilityView.Raw,
                AutomationProperties.GetAccessibilityView(report));

            disclosure.CompleteTargetState(isExpanded: true);
            Assert.IsTrue(report.IsEnabled);
            Assert.IsTrue(report.IsTabStop);
            Assert.AreEqual(
                AccessibilityView.Content,
                AutomationProperties.GetAccessibilityView(
                    disclosure.ViewportTarget));
            Assert.AreEqual(
                AccessibilityView.Content,
                AutomationProperties.GetAccessibilityView(report));
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Header_RestoresSemanticTopDivider()
    {
        var disclosure = CreateDisclosure();
        var button = (Button)disclosure.FindName("DisclosureToggleButton");

        Assert.AreEqual(1d, button.BorderThickness.Top, 0.001);
        Assert.IsNotNull(button.BorderBrush);
    }

    private static InspectionDisclosure CreateDisclosure() =>
        new()
        {
            HeaderContent = new TextBlock { Text = "Inspection details" },
            ViewportContent = new TextBlock { Text = "Safe report" }
        };

    private static IExpandCollapseProvider GetExpandCollapseProvider(
        InspectionDisclosure disclosure)
    {
        AutomationPeer peer =
            FrameworkElementAutomationPeer.CreatePeerForElement(disclosure);
        Assert.IsNotNull(peer);
        return Assert.IsInstanceOfType<IExpandCollapseProvider>(
            peer.GetPattern(PatternInterface.ExpandCollapse));
    }
}
