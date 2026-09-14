using GraniteEdgeAI.Features.ModelInspection.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using Windows.Foundation;
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
    public void AcceptedNonvisualClaim_DrivesRapidReverseWithoutChangingPeerState()
    {
        var disclosure = CreateDisclosure();
        IExpandCollapseProvider provider = GetExpandCollapseProvider(disclosure);
        List<bool> requests = [];
        disclosure.ToggleRequested += (_, args) => requests.Add(args.IsExpanded);

        provider.Expand();
        disclosure.ClaimTargetState(isExpanded: true);
        disclosure.HandleKeyboardActivation(
            VirtualKey.Space,
            repeatCount: 1,
            wasKeyDown: false);

        CollectionAssert.AreEqual(new[] { true }, requests,
            "The unfocused keyboard path remains ignored in this direct fixture.");
        disclosure.RequestTargetState(isExpanded: false);
        CollectionAssert.AreEqual(new[] { true, false }, requests);
        Assert.AreEqual(ExpandCollapseState.Collapsed, provider.ExpandCollapseState);
        Assert.IsFalse(disclosure.IsExpanded);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void RejectedClaimRollback_RestoresCompletedDirection()
    {
        var disclosure = CreateDisclosure();
        List<bool> requests = [];
        disclosure.ToggleRequested += (_, args) => requests.Add(args.IsExpanded);

        disclosure.ClaimTargetState(isExpanded: true);
        disclosure.RollbackTargetStateClaim(isExpanded: true);
        disclosure.RequestTargetState(isExpanded: true);

        CollectionAssert.AreEqual(new[] { true }, requests);
        Assert.IsFalse(disclosure.IsExpanded);
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
    public async Task CollapsePreparation_ReflowsHostButRetainsRenderableExtent()
    {
        var disclosure = CreateDisclosure();
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        disclosure.Loaded += (_, _) => loaded.TrySetResult(true);
        var window = new Window { Content = disclosure };

        try
        {
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            disclosure.PrepareTargetState(isExpanded: true);
            disclosure.UpdateLayout();
            disclosure.CompleteTargetState(isExpanded: true);
            disclosure.UpdateLayout();
            double expandedExtent = disclosure.ViewportTarget.ActualHeight;
            Assert.IsGreaterThan(0d, expandedExtent);

            disclosure.PrepareTargetState(isExpanded: false);
            disclosure.UpdateLayout();

            Assert.AreEqual(0d, disclosure.ViewportLayoutTarget.ActualHeight, 0.01);
            Assert.AreEqual(expandedExtent, disclosure.ViewportTarget.ActualHeight, 0.01);
            Assert.AreEqual(Visibility.Visible, disclosure.ViewportTarget.Visibility);
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task InstantExpand_ClearsRetainedAnimatedCollapseClip()
    {
        var disclosure = CreateDisclosure();
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
            disclosure.UpdateLayout();

            var visual = ElementCompositionPreview.GetElementVisual(
                disclosure.ViewportTarget);
            var collapsedClip = visual.Compositor.CreateInsetClip();
            collapsedClip.BottomInset = (float)Math.Max(
                1d,
                disclosure.ViewportTarget.ActualHeight);
            visual.Clip = collapsedClip;

            disclosure.PrepareTargetState(isExpanded: false);
            disclosure.CompleteTargetState(isExpanded: false);
            disclosure.PrepareTargetState(isExpanded: true);
            disclosure.CompleteTargetState(isExpanded: true);
            disclosure.UpdateLayout();

            var remaining = visual.Clip as Microsoft.UI.Composition.InsetClip;
            Assert.IsTrue(
                remaining is null || remaining.BottomInset == 0f,
                "The instant expanded endpoint must not retain the animated collapse clip.");
            Assert.AreEqual(Visibility.Visible, disclosure.ViewportTarget.Visibility);
            Assert.AreEqual(1d, disclosure.ViewportTarget.Opacity, 0.001d);
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ChevronMotionTarget_HasSingleCompositionRotationOwner()
    {
        var disclosure = CreateDisclosure();
        var target = (FrameworkElement)disclosure.ChevronTarget;
        var visual = ElementCompositionPreview.GetElementVisual(target);

        Assert.IsNotInstanceOfType<RotateTransform>(target.RenderTransform);
        if (target.RenderTransform is MatrixTransform matrixTransform)
        {
            Assert.AreEqual(1d, matrixTransform.Matrix.M11, 0.001);
            Assert.AreEqual(0d, matrixTransform.Matrix.M12, 0.001);
            Assert.AreEqual(0d, matrixTransform.Matrix.M21, 0.001);
            Assert.AreEqual(1d, matrixTransform.Matrix.M22, 0.001);
            Assert.AreEqual(0d, matrixTransform.Matrix.OffsetX, 0.001);
            Assert.AreEqual(0d, matrixTransform.Matrix.OffsetY, 0.001);
        }
        else
        {
            Assert.IsNull(target.RenderTransform,
                "Only the platform identity MatrixTransform is permitted.");
        }
        Assert.AreEqual(0f, visual.RotationAngleInDegrees, 0.01f);

        disclosure.PrepareTargetState(isExpanded: true);
        disclosure.CompleteTargetState(isExpanded: true);
        Assert.AreEqual(180f, visual.RotationAngleInDegrees, 0.01f);

        disclosure.PrepareTargetState(isExpanded: false);
        disclosure.CompleteTargetState(isExpanded: false);
        Assert.AreEqual(0f, visual.RotationAngleInDegrees, 0.01f);
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
    public async Task Header_RestoresSemanticTopDivider()
    {
        var header = new TextBlock
        {
            Text = "Inspection details remain readable beside an absolute action",
            TextWrapping = TextWrapping.WrapWholeWords
        };
        var disclosure = new InspectionDisclosure
        {
            Width = 480d,
            HeaderMinHeight = 68d,
            HeaderContent = header,
            ViewportContent = new TextBlock { Text = "Safe report" }
        };
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        disclosure.Loaded += (_, _) => loaded.TrySetResult(true);
        var window = new Window { Content = disclosure };

        try
        {
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            disclosure.UpdateLayout();
            var surface = (Border)disclosure.FindName("DisclosureCardSurface");
            var button = (Button)disclosure.FindName("DisclosureToggleButton");
            var viewportHost = (Border)disclosure.FindName("DisclosureViewportHost");
            var divider = (Border)disclosure.FindName("DisclosureDivider");
            Grid headerLayout = Assert.IsInstanceOfType<Grid>(
                disclosure.FindName("DisclosureHeaderShell"));
            ContentPresenter contentPresenter =
                Assert.IsInstanceOfType<ContentPresenter>(
                    disclosure.FindName("DisclosureHeaderContentPresenter"));
            Grid actionHost = Assert.IsInstanceOfType<Grid>(
                disclosure.FindName("DisclosureHeaderActionHost"));

            Assert.AreEqual(new Thickness(1d), surface.BorderThickness);
            Assert.AreEqual(12d, surface.CornerRadius.TopLeft, 0.001d);
            Assert.IsNotNull(surface.Background);
            Assert.IsNotNull(surface.BorderBrush);
            Assert.AreEqual(new Thickness(0d), button.BorderThickness);
            Assert.AreEqual(new Thickness(0d), viewportHost.BorderThickness);
            Assert.AreEqual(1d, divider.Height, 0.001d);
            Assert.AreSame(surface.BorderBrush, divider.Background);
            Assert.AreSame(
                VisualTreeHelper.GetParent(button),
                VisualTreeHelper.GetParent(viewportHost));
            Assert.AreEqual(2, headerLayout.ColumnDefinitions.Count);
            Assert.AreEqual(new GridLength(44d), headerLayout.ColumnDefinitions[1].Width);
            Assert.AreEqual(44d, actionHost.ActualWidth, 0.01d);
            Assert.AreEqual(44d, actionHost.ActualHeight, 0.01d);
            Assert.IsGreaterThanOrEqualTo(68d, headerLayout.ActualHeight,
                "the normal disclosure header keeps the approved 68px target");
            Assert.AreEqual(
                VerticalCentre(headerLayout, actionHost),
                VerticalCentre(headerLayout, contentPresenter),
                1d,
                "header copy and the disclosure action share a vertical centre");

            double normalHeaderHeight = headerLayout.ActualHeight;

            header.FontSize *= 2d;
            disclosure.UpdateLayout();

            Assert.AreEqual(44d, actionHost.ActualWidth, 0.01d,
                "the disclosure action target stays fixed at 200% text");
            Assert.IsTrue(
                contentPresenter.ActualWidth + actionHost.ActualWidth <=
                    headerLayout.ActualWidth + 0.01d,
                "the wrapping header and absolute action cannot intersect");
            // DesiredSize describes the measure request, while a TextBlock's
            // rendered text can be shorter. Check the allocated layout space
            // and containment instead of requiring those heights to match.
            Rect headerSlot = Microsoft.UI.Xaml.Controls.Primitives.LayoutInformation
                .GetLayoutSlot(header);
            Assert.IsTrue(headerSlot.Height + 1d >= header.DesiredSize.Height,
                "scaled header must receive its requested layout height; " +
                $"slot={headerSlot.Height}; desired={header.DesiredSize.Height}");
            Assert.IsTrue(contentPresenter.ActualHeight + 1d >= header.DesiredSize.Height,
                "the presenter must reserve the scaled header's requested height");
            Rect headerBounds = header.TransformToVisual(contentPresenter)
                .TransformBounds(new Rect(0d, 0d, header.ActualWidth, header.ActualHeight));
            Assert.IsTrue(headerBounds.Top >= -1d &&
                headerBounds.Bottom <= contentPresenter.ActualHeight + 1d &&
                headerBounds.Left >= -1d &&
                headerBounds.Right <= contentPresenter.ActualWidth + 1d,
                "scaled header text must stay inside its allocated presenter");
            Assert.IsFalse(header.IsTextTrimmed, "scaled header text must remain untrimmed");
            Assert.IsGreaterThan(normalHeaderHeight, headerLayout.ActualHeight,
                "the disclosure header grows naturally at representative 200% text");
            Assert.AreEqual(
                VerticalCentre(headerLayout, actionHost),
                VerticalCentre(headerLayout, contentPresenter),
                1d,
                "scaled header copy and action remain vertically centred");
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    private static double VerticalCentre(
        FrameworkElement ancestor,
        FrameworkElement element)
    {
        Point origin = element.TransformToVisual(ancestor)
            .TransformPoint(new Point());
        return origin.Y + (element.ActualHeight / 2d);
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
