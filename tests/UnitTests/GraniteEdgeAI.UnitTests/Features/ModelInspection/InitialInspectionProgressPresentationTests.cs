using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.Reflection;

namespace GraniteEdgeAI.UnitTests;

/// <summary>
/// Protects the user-visible boundary between core model inspection and later
/// hardware/backend verification.
/// </summary>
[TestClass]
public sealed class InitialInspectionProgressPresentationTests
{
    public TestContext TestContext { get; set; } = null!;
    [UITestMethod]
    [TestCategory("ProgressCompletion")]
    public async Task BothRoutesCatchCompletedRowUpWhileNextStageStarts()
    {
        foreach (bool openVino in new[] { false, true })
        foreach (int completedIndex in new[] { 0, 3 })
        foreach (bool motion in new[] { true, false })
        {
            Microsoft.UI.Xaml.Controls.UserControl view = openVino
                ? new GraniteEdgeAI.Features.ModelInspection.Views.ModelInspectionOpenVinoPreviewView()
                : new GraniteEdgeAI.Features.ModelInspection.Views.ModelInspectionGgufPreviewView();
            var projection = new GraniteEdgeAI.Features.ModelInspection.Views.ModelInspectionPreviewProjection(view, openVino);
            projection.SetMotionEnabled(motion);
            var content = new InspectionContentCardPresentation { Mode = InspectionContentCardMode.Progress };
            for (int i = 0; i < completedIndex; i++)
            {
                content.ProgressRows.Items[i].Status = InspectionContentStatus.Passed;
                content.ProgressRows.Items[i].StatusText = "Passed";
                content.ProgressRows.Items[i].AutomationName = $"{content.ProgressRows.Items[i].Title}. Passed.";
            }
            var row = content.ProgressRows.Items[completedIndex];
            row.Status = InspectionContentStatus.Active; row.IsActive = true; row.StageFraction = .1;
            row.StatusText = "Checking";
            row.AutomationName = $"{row.Title}. Checking.";
            projection.ApplyContent(content);
            await using var host = await GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual.WinUiRenderHost.ShowAsync(view, 800, 600);
            var status = (Microsoft.UI.Xaml.Controls.TextBlock)view.FindName($"InspectionStage{completedIndex + 1}Status");
            StringAssert.Contains(status.Text, "Checking");
            row.Status = InspectionContentStatus.Passed; row.IsActive = false; row.StatusText = "Passed";
            row.AutomationName = $"{row.Title}. Passed.";
            var next = content.ProgressRows.Items[completedIndex + 1];
            next.Status = InspectionContentStatus.Active; next.IsActive = true; next.StatusText = "Checking";
            next.AutomationName = $"{next.Title}. Checking.";
            projection.ApplyContent(content);
            void AssertTerminal()
            {
                Assert.AreEqual("Passed", status.Text, $"route={openVino}, stage={completedIndex + 1}, motion={motion}");
                var rowHost = (FrameworkElement)view.FindName($"InspectionStage{completedIndex + 1}");
                Assert.AreEqual("Passed", Microsoft.UI.Xaml.Automation.AutomationProperties.GetItemStatus(rowHost));
                StringAssert.Contains(Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(rowHost), "Passed");
                Assert.AreEqual(Visibility.Visible, ((FrameworkElement)view.FindName($"InspectionStage{completedIndex + 1}Glyph")).Visibility);
                StringAssert.Contains(((Microsoft.UI.Xaml.Controls.TextBlock)view.FindName($"InspectionStage{completedIndex + 2}Status")).Text, "Checking");
                Assert.IsTrue(next.IsActive);
                Assert.IsTrue(((Microsoft.UI.Xaml.Controls.ProgressBar)view.FindName("InspectionOverallProgress")).Value < 5);
            }
            AssertTerminal();
            var tick = projection.GetType().GetMethod("UpdateEstimatedProgressValues", BindingFlags.Instance | BindingFlags.NonPublic)!;
            for (int i = 0; i < 4; i++) { tick.Invoke(projection, null); AssertTerminal(); }
            if (openVino && completedIndex == 3 && motion)
            {
                foreach (ElementTheme theme in new[] { ElementTheme.Light, ElementTheme.Dark })
                {
                    view.RequestedTheme = theme;
                    var frame = await host.CaptureAsync();
                    TestContext.AddResultFile(await frame.SavePngAsync($"openvino-step4-passed-step5-active-{theme}.png"));
                    AssertTerminal();
                }
            }
            row.Status = InspectionContentStatus.Warning; row.StatusText = "Warning";
            row.AutomationName = $"{row.Title}. Warning.";
            projection.ApplyContent(content);
            tick.Invoke(projection, null);
            Assert.AreEqual("Warning", status.Text, "A non-success outcome must never become Passed.");
            projection.CancelProgressMotion();
        }
    }
    [UITestMethod]
    [TestCategory("EstimatedProgress")]
    public async Task ActiveEstimateResumesAfterAncestorRevealButExplicitStopDoesNot()
    {
        var view = new GraniteEdgeAI.Features.ModelInspection.Views.ModelInspectionGgufPreviewView();
        var projection = new GraniteEdgeAI.Features.ModelInspection.Views.ModelInspectionPreviewProjection(view, false);
        var content = new InspectionContentCardPresentation { Mode = InspectionContentCardMode.Progress };
        content.ProgressRows.Items[0].Status = InspectionContentStatus.Active;
        content.ProgressRows.Items[0].IsActive = true;
        projection.ApplyContent(content);
        var parent = new Microsoft.UI.Xaml.Controls.Grid();
        parent.Children.Add(view);
        await using var host = await GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual.WinUiRenderHost.ShowAsync(parent, 800, 600);
        var bar = (Microsoft.UI.Xaml.Controls.ProgressBar)view.FindName("InspectionOverallProgress");
        parent.Visibility = Visibility.Collapsed;
        await Task.Delay(100);
        double paused = bar.Value;
        parent.Visibility = Visibility.Visible;
        await Task.Delay(600);
        if (new Windows.UI.ViewManagement.UISettings().AnimationsEnabled) Assert.IsTrue(bar.Value > paused);
        projection.CancelProgressMotion();
        parent.Visibility = Visibility.Collapsed;
        parent.Visibility = Visibility.Visible;
        await Task.Delay(250);
        Assert.AreEqual(0d, bar.Value);
    }

    [UITestMethod]
    [TestCategory("ChatTextInteractionsMeasured")]
    public void BothRoutesUseOneTruthfulTotalBarAndInlineStepStatus()
    {
        foreach (bool openVino in new[] { false, true })
        {
            Microsoft.UI.Xaml.Controls.UserControl view = openVino
                ? new GraniteEdgeAI.Features.ModelInspection.Views.ModelInspectionOpenVinoPreviewView()
                : new GraniteEdgeAI.Features.ModelInspection.Views.ModelInspectionGgufPreviewView();
            var projection = new GraniteEdgeAI.Features.ModelInspection.Views.ModelInspectionPreviewProjection(view, openVino);
            projection.SetMotionEnabled(false);
            var content = new InspectionContentCardPresentation { Mode = InspectionContentCardMode.Progress };
            var active = content.ProgressRows.Items[0];
            active.Status = InspectionContentStatus.Active;
            active.IsActive = true;
            active.StageFraction = .58;
            projection.ApplyContent(content);
            var bar = (Microsoft.UI.Xaml.Controls.ProgressBar)view.FindName("InspectionOverallProgress");
            Assert.AreEqual("Overall model inspection progress", Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(bar));
            var text = (Microsoft.UI.Xaml.Controls.TextBlock)view.FindName("InspectionStage1Status");
            Assert.IsNull(view.FindName("InspectionCurrentProgress"));
            Assert.IsNull(view.FindName("InspectionCurrentProgressText"));
            Assert.IsFalse(bar.IsIndeterminate);
            Assert.AreEqual(.58d, bar.Value, .00001);
            Assert.AreEqual("Checking\n58%", text.Text);
            Assert.AreEqual("Checking · 58%", Microsoft.UI.Xaml.Automation.AutomationProperties.GetItemStatus(
                (DependencyObject)view.FindName("InspectionStage1")));
            active.StageFraction = null;
            projection.ApplyContent(content);
            Assert.IsFalse(bar.IsIndeterminate);
            Assert.AreEqual("Checking\nEstimated 58%", text.Text);
            Assert.AreEqual("Estimated 11%", ((Microsoft.UI.Xaml.Controls.TextBlock)view.FindName("InspectionOverallPercentage")).Text);
            Assert.AreEqual(.58d, bar.Value, .00001, "Unknown samples must retain the last accepted endpoint.");
            active.Status = InspectionContentStatus.Passed;
            active.IsActive = false;
            content.ProgressRows.Items[1].Status = InspectionContentStatus.Active;
            content.ProgressRows.Items[1].IsActive = true;
            projection.ApplyContent(content);
            Assert.AreEqual(1d, bar.Value);
            var nextRun = new InspectionContentCardPresentation { Mode = InspectionContentCardMode.Progress };
            nextRun.ProgressRows.Items[0].Status = InspectionContentStatus.Active;
            nextRun.ProgressRows.Items[0].IsActive = true;
            projection.ApplyContent(nextRun);
            Assert.AreEqual(0d, bar.Value, "A new run cannot retain the previous fraction.");
            projection.SetMotionEnabled(true);
            Assert.IsFalse(bar.IsIndeterminate);
            Assert.AreEqual(Visibility.Visible, bar.Visibility);
            projection.CancelProgressMotion();
            Assert.IsFalse(bar.IsIndeterminate);
            Assert.AreEqual(0d, bar.Value);
            foreach (var row in nextRun.ProgressRows.Items) { row.Status = InspectionContentStatus.Passed; row.IsActive = false; }
            projection.ApplyContent(nextRun);
            Assert.IsTrue(bar.Value < 5, "Completed stage events do not replace the authoritative terminal outcome.");
            Assert.AreEqual("Estimated 99%", ((Microsoft.UI.Xaml.Controls.TextBlock)view.FindName("InspectionOverallPercentage")).Text);
            projection.CancelProgressMotion();
        }
    }

    [UITestMethod]
    [TestCategory("InspectionSingleBar")]
    public async Task ModelTotalInterpolatesOnlyToAcceptedEndpointAndStopsOnUnload()
    {
        foreach (bool openVino in new[] { false, true })
        {
            Microsoft.UI.Xaml.Controls.UserControl view = openVino
                ? new GraniteEdgeAI.Features.ModelInspection.Views.ModelInspectionOpenVinoPreviewView()
                : new GraniteEdgeAI.Features.ModelInspection.Views.ModelInspectionGgufPreviewView();
            var projection = new GraniteEdgeAI.Features.ModelInspection.Views.ModelInspectionPreviewProjection(view, openVino);
            var content = new InspectionContentCardPresentation { Mode = InspectionContentCardMode.Progress };
            var active = content.ProgressRows.Items[0];
            active.Status = InspectionContentStatus.Active;
            active.IsActive = true;
            active.StageFraction = 0;
            projection.ApplyContent(content);
            await using var host = await GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual.WinUiRenderHost.ShowAsync(view, 800, 600);
            var bar = (Microsoft.UI.Xaml.Controls.ProgressBar)view.FindName("InspectionOverallProgress");
            active.StageFraction = .8;
            projection.ApplyContent(content);
            var values = new List<double>();
            for (int i = 0; i < 16; i++) { values.Add(bar.Value); await Task.Delay(20); }
            Assert.IsTrue(values.All(value => value >= 0 && value <= .8));
            Assert.IsTrue(values.Zip(values.Skip(1)).All(pair => pair.First <= pair.Second));
            if (new Windows.UI.ViewManagement.UISettings().AnimationsEnabled)
                Assert.IsTrue(values.Any(value => value > 0 && value < .8), "Accepted progress should interpolate visibly.");
            Assert.AreEqual(.8, bar.Value, .00001);
            active.Status = InspectionContentStatus.Passed;
            active.IsActive = false;
            active = content.ProgressRows.Items[1];
            active.Status = InspectionContentStatus.Active;
            active.IsActive = true;
            active.StageFraction = 0; // keep this interpolation test on a measured endpoint; opaque motion has separate coverage
            projection.ApplyContent(content);
            values.Clear();
            for (int i = 0; i < 16; i++) { values.Add(bar.Value); await Task.Delay(20); }
            Assert.IsTrue(values.All(value => value >= .8 && value <= 1));
            Assert.IsTrue(values.Zip(values.Skip(1)).All(pair => pair.First <= pair.Second));
            if (new Windows.UI.ViewManagement.UISettings().AnimationsEnabled)
                Assert.IsTrue(values.Any(value => value > .8 && value < 1), "An accepted stage transition must not jump.");
            Assert.AreEqual(1d, bar.Value);
            active.StageFraction = .58;
            projection.ApplyContent(content);
            projection.SetMotionEnabled(false);
            Assert.AreEqual(1.58d, bar.Value, .00001);
            active.StageFraction = null;
            projection.ApplyContent(content);
            Assert.AreEqual(1.58d, bar.Value, .00001);
            await host.DisposeAsync();
            await Task.Delay(250);
            Assert.AreEqual(0d, bar.Value);
        }
    }
    /// <summary>
    /// Verifies the exact five rows and their approved order.
    /// </summary>
    [UITestMethod]
    [TestCategory("WinUI")]
    public void Create_ReturnsFiveApprovedCoreInspectionStages()
    {
        InspectionContentCardPresentation presentation =
            InitialInspectionProgressPresentationFactory.Create();

        string[] actualTitles = presentation.ProgressRows.Items
            .Select(item => item.Title)
            .ToArray();

        string[] expectedTitles =
        [
            "Check model package",
            "Read model configuration",
            "Validate tokenizer and chat setup",
            "Validate model structure",
            "Confirm core runtime compatibility"
        ];

        CollectionAssert.AreEqual(
            expectedTitles,
            actualTitles);
    }

    /// <summary>
    /// Verifies that the pre-run state does not claim worker activity.
    /// </summary>
    [UITestMethod]
    [TestCategory("WinUI")]
    public void Create_LeavesAllFiveStagesWaiting()
    {
        InspectionContentCardPresentation presentation =
            InitialInspectionProgressPresentationFactory.Create();

        Assert.AreEqual(
            0,
            presentation.ProgressRows.Items.Count(item => item.IsActive));

        foreach (InspectionContentItemPresentation waitingStage in
                 presentation.ProgressRows.Items)
        {
            Assert.IsFalse(waitingStage.IsActive);
            Assert.AreEqual(
                InspectionContentStatus.Waiting,
                waitingStage.Status);
            Assert.AreEqual(
                "Waiting",
                waitingStage.StatusText);
        }

        PropertyInfo? startupProperty = typeof(InspectionContentCardPresentation)
            .GetProperty("Startup", BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(
            startupProperty,
            "The initial card must expose an explicitly hidden startup model.");
        object? startup = startupProperty.GetValue(presentation);
        Assert.IsNotNull(startup);
        Type startupType = startup.GetType();
        Assert.AreEqual(
            Visibility.Collapsed,
            startupType.GetProperty("Visibility")!.GetValue(startup));
        Assert.AreEqual(
            string.Empty,
            startupType.GetProperty("Summary")!.GetValue(startup));
        Assert.AreEqual(
            string.Empty,
            startupType.GetProperty("AutomationName")!.GetValue(startup));
    }

    /// <summary>
    /// Verifies the tracker geometry and initial completed-stage count.
    /// </summary>
    [UITestMethod]
    [TestCategory("WinUI")]
    public void Create_UsesFourConnectorsAndZeroCompletedChecks()
    {
        InspectionContentCardPresentation presentation =
            InitialInspectionProgressPresentationFactory.Create();

        Assert.AreEqual(
            "0 of 5 checks complete",
            presentation.ProgressRows.ProgressSummary);
        Assert.IsTrue(
            presentation.ProgressRows.Items.Take(4)
                .All(item => item.ShowConnector));
        Assert.IsFalse(
            presentation.ProgressRows.Items[4].ShowConnector);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Create_HidesAllDetailsBeforeTheRunStarts()
    {
        InspectionContentCardPresentation presentation =
            InitialInspectionProgressPresentationFactory.Create();

        foreach (InspectionContentItemPresentation waitingStage in
                 presentation.ProgressRows.Items)
        {
            Assert.AreEqual(
                Visibility.Collapsed,
                waitingStage.DetailVisibility);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Create_UsesTitleAndStatusForStageAutomationNames()
    {
        InspectionContentCardPresentation presentation =
            InitialInspectionProgressPresentationFactory.Create();

        foreach (InspectionContentItemPresentation item in
                 presentation.ProgressRows.Items)
        {
            Assert.AreEqual(
                $"{item.Title}. {item.StatusText}.",
                item.AutomationName);
        }
    }

    /// <summary>
    /// Prevents backend- and hardware-specific validation from leaking into the
    /// pre-Hardware-Fit Model Inspection tracker.
    /// </summary>
    [UITestMethod]
    [TestCategory("WinUI")]
    public void Create_DoesNotPresentBackendVerificationAsModelInspection()
    {
        InspectionContentCardPresentation presentation =
            InitialInspectionProgressPresentationFactory.Create();

        string[] forbiddenTerms =
        [
            "vulkan",
            "turboquant",
            "gpu",
            "hardware fit"
        ];

        foreach (InspectionContentItemPresentation item in
                 presentation.ProgressRows.Items)
        {
            string normalisedTitle = item.Title.ToLowerInvariant();

            foreach (string forbiddenTerm in forbiddenTerms)
            {
                Assert.IsFalse(
                    normalisedTitle.Contains(
                        forbiddenTerm,
                        StringComparison.Ordinal),
                    $"Stage '{item.Title}' must not include backend-specific " +
                    $"term '{forbiddenTerm}'.");
            }
        }
    }
}
