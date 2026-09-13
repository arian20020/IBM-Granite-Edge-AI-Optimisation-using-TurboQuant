using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.Controls;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.Factories;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.State;
using GraniteEdgeAI.Presentation.Progress;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.Reflection;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection;

[TestClass]
[TestCategory("HardwareInspectionGate8Acceptance")]
public sealed class HardwareInspectionProgressCardTests
{
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    public void FastCompletedStage_KeepsCopyInDisplayedState(int index)
    {
        var stage = (HardwareInspectionStage)index;
        var copy = HardwareInspectionCopyCatalog.Stage(stage);
        var completed = new HardwareInspectionStageRow(stage,
            HardwareInspectionStageRowState.Complete, copy.Title,
            copy.CompletedSentence, copy.Title);
        var row = new HardwareInspectionProgressRowViewData(index + 1, completed);

        row.Update(index + 1, completed, SerializedProgressStageState.Waiting, 0, true);
        Assert.AreEqual(string.Empty, row.Sentence);
        row.Update(index + 1, completed, SerializedProgressStageState.Active, .12, true);
        Assert.AreEqual(copy.ActiveExplanation, row.Sentence);
        StringAssert.Contains(row.DisplayStatus, "Checking");
        row.Update(index + 1, completed, SerializedProgressStageState.Completed, 1, true);
        Assert.AreEqual(copy.CompletedSentence, row.Sentence);
        Assert.AreEqual("Complete", row.DisplayStatus);
        row.ApplyCancelledOutcome(isInterruptedStage: false);
        Assert.AreEqual(copy.CompletedSentence, row.Sentence);
        Assert.AreEqual(copy.CompletedSentence, completed.Sentence);

        var waiting = new HardwareInspectionStageRow(stage,
            HardwareInspectionStageRowState.Waiting, copy.Title,
            copy.WaitingSentence, copy.Title);
        row.Update(index + 1, waiting);
        Assert.AreEqual(string.Empty, row.Sentence);
        var active = new HardwareInspectionStageRow(stage,
            HardwareInspectionStageRowState.Active, copy.Title,
            copy.ActiveExplanation, copy.Title);
        row.Update(index + 1, active);
        Assert.AreEqual(copy.ActiveExplanation, row.Sentence);
        row.Update(index + 1, active, SerializedProgressStageState.Active, .5, false);
        Assert.AreEqual(copy.ActiveExplanation, row.Sentence);
    }

    public TestContext TestContext { get; set; } = null!;
    [UITestMethod]
    [TestCategory("ProgressCompletion")]
    public async Task CompletedGroupCatchesUpWhileOtherGroupsRemainActive()
    {
        var card = new HardwareInspectionProgressCard();
        var factory = new HardwareInspectionPresentationFactory();
        object owner = new();
        var groups = new Dictionary<HardwareInspectionStage, (int Completed, int Total)>();
        card.Apply(factory.CreateActive(HardwareInspectionStage.CheckingLocalInferenceRuntimes, groups, owner));
        await using var host = await GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual.WinUiRenderHost.ShowAsync(card, 800, 600);
        StringAssert.Contains(card.Rows[1].DisplayStatus, "Checking");
        groups[HardwareInspectionStage.ReadingProcessorInformation] = (1, 1);
        card.Apply(factory.CreateActive(HardwareInspectionStage.CheckingLocalInferenceRuntimes, groups, owner));
        void AssertTerminal()
        {
            Assert.AreEqual("Complete", card.Rows[1].DisplayStatus);
            Assert.AreEqual("Complete", card.Rows[1].Status);
            StringAssert.Contains(card.Rows[1].AccessibleName, "Complete");
            Assert.AreEqual(Visibility.Visible, card.Rows[1].CompletedVisibility);
            StringAssert.Contains(card.Rows[2].DisplayStatus, "Checking");
            Assert.IsTrue(((ProgressBar)card.FindName("OverallProgressBar")).Value < 7);
        }
        AssertTerminal();
        var tick = typeof(HardwareInspectionProgressCard).GetMethod("RefreshEstimatedValues", BindingFlags.Instance | BindingFlags.NonPublic)!;
        for (int i = 0; i < 4; i++) { tick.Invoke(card, null); AssertTerminal(); }
    }
    [UITestMethod]
    [TestCategory("EstimatedProgress")]
    public void SevenSettledProbesDoNotCompleteNormalisationOrReport()
    {
        var groups = new Dictionary<HardwareInspectionStage, (int Completed, int Total)>
        {
            [HardwareInspectionStage.ReadingProcessorInformation] = (1, 1),
            [HardwareInspectionStage.ReadingSystemMemory] = (2, 2),
            [HardwareInspectionStage.DetectingGraphicsHardware] = (2, 2),
            [HardwareInspectionStage.CheckingLocalInferenceRuntimes] = (2, 2),
        };
        var state = new HardwareInspectionPresentationFactory().CreateActive(HardwareInspectionStage.CheckingLocalInferenceRuntimes, groups, new object());
        var card = new HardwareInspectionProgressCard();
        card.Apply(state);
        Assert.AreEqual(5, state.CompletedStageCount);
        Assert.AreEqual(2, state.StageRows.Count(row => row.State == HardwareInspectionStageRowState.Waiting));
        Assert.AreEqual(5d, ((ProgressBar)card.FindName("OverallProgressBar")).Value);
        Assert.AreEqual("Estimated 71%", ((TextBlock)card.FindName("OverallPercentage")).Text);
    }

    [TestMethod]
    [TestCategory("EstimatedProgress")]
    public void SchedulingParallelGroupsDoesNotCompleteEarlierChecks()
    {
        var state = new HardwareInspectionPresentationFactory().CreateActive(HardwareInspectionStage.CheckingLocalInferenceRuntimes);
        Assert.AreEqual(1, state.CompletedStageCount, "Only acquisition is complete while collection groups are running.");
        Assert.AreEqual(4, state.StageRows.Count(row => row.State == HardwareInspectionStageRowState.Active));
    }

    [UITestMethod]
    [TestCategory("ChatTextInteractionsMeasured")]
    public void SingleWorkflowBarKeepsMeasuredChecksInActiveRow()
    {
        var card = new HardwareInspectionProgressCard();
        var factory = new HardwareInspectionPresentationFactory();
        object owner = new();
        var groups = new Dictionary<HardwareInspectionStage, (int Completed, int Total)>
        {
            [HardwareInspectionStage.ReadingProcessorInformation] = (1, 1),
            [HardwareInspectionStage.ReadingSystemMemory] = (1, 2),
            [HardwareInspectionStage.DetectingGraphicsHardware] = (0, 2),
            [HardwareInspectionStage.CheckingLocalInferenceRuntimes] = (1, 2),
        };
        card.Apply(factory.CreateActive(HardwareInspectionStage.CheckingLocalInferenceRuntimes, groups, owner));
        var bar = (ProgressBar)card.FindName("OverallProgressBar");
        Assert.AreEqual("Overall hardware inspection progress", AutomationProperties.GetName(bar));
        Assert.IsNull(card.FindName("MeasuredChecksProgress"));
        Assert.IsNull(card.FindName("MeasuredChecksText"));
        Assert.IsFalse(bar.IsIndeterminate);
        Assert.AreEqual(3d, bar.Value, .00001, "Two completed stages and two real half-complete groups.");
        Assert.AreEqual("Checking\n50%", card.Rows[4].DisplayStatus);
        Assert.AreEqual("Checking · 50%", card.Rows[4].Status);
        foreach (var laterStage in new[] { HardwareInspectionStage.NormalisingHardwareInformation, HardwareInspectionStage.CreatingHardwareReport })
        {
            card.Apply(factory.CreateActive(laterStage, groups, owner).WithMeasuredChecks(7, 7));
            var row = card.Rows[(int)laterStage];
            Assert.AreEqual("Checking\nEstimated 0%", row.DisplayStatus);
            StringAssert.Contains(row.AccessibleName, "Checking · Estimated 0%");
            Assert.IsFalse(row.AccessibleName.Contains("100%"));
        }
    }
    [UITestMethod]
    [TestCategory("InspectionSingleBar")]
    public async Task WorkflowEndpointInterpolatesWithoutTreatingEvidenceAsWholeFlow()
    {
        var card = new HardwareInspectionProgressCard();
        var factory = new HardwareInspectionPresentationFactory();
        object owner = new();
        var groups = new Dictionary<HardwareInspectionStage, (int Completed, int Total)>();
        card.Apply(factory.CreateActive(HardwareInspectionStage.StartingHardwareInspection, groups, owner));
        await using var host = await GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual.WinUiRenderHost.ShowAsync(card, 800, 600);
        var bar = (ProgressBar)card.FindName("OverallProgressBar");
        card.Apply(factory.CreateActive(HardwareInspectionStage.NormalisingHardwareInformation, groups, owner).WithMeasuredChecks(7, 7));
        var values = new List<double>();
        for (int i = 0; i < 16; i++) { values.Add(bar.Value); await Task.Delay(20); }
        Assert.IsTrue(values.All(value => value >= 0 && value < 5.1));
        Assert.IsTrue(values.Zip(values.Skip(1)).All(pair => pair.First <= pair.Second));
        if (new Windows.UI.ViewManagement.UISettings().AnimationsEnabled)
            Assert.IsTrue(values.Any(value => value > 0 && value < 5));
        Assert.IsTrue(bar.Value >= 5 && bar.Value < 5.1);
        Assert.AreEqual("Checking\nEstimated 0%", card.Rows[5].DisplayStatus);
        card.Apply(factory.CreateActive(HardwareInspectionStage.StartingHardwareInspection));
        Assert.AreEqual(0d, bar.Value, "A restarted workflow must reset its completed stages.");
        await host.DisposeAsync();
        await Task.Delay(250);
        Assert.AreEqual(0d, bar.Value);
    }

    private readonly HardwareInspectionPresentationFactory _factory = new();

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Apply_InitialStageRendersExactApprovedFrame()
    {
        HardwareInspectionProgressCard card = new();
        card.Apply(_factory.CreateActive(HardwareInspectionStage.StartingHardwareInspection));

        Assert.AreEqual("Inspection in progress", Text(card, "KickerTextBlock").Text);
        Assert.AreEqual("Starting hardware inspection", Text(card, "TitleTextBlock").Text);
        Assert.AreEqual(
            "Preparing the approved local inspection tools and a safe run context.",
            Text(card, "BodyTextBlock").Text);
        Assert.AreEqual("0 of 7 stages complete", Text(card, "CountTextBlock").Text);
        Assert.AreEqual("checks complete", Text(card, "CountLabelTextBlock").Text);
        Grid header = (Grid)card.FindName("ProgressHeaderGrid");
        Assert.IsNotNull(header);
        Assert.AreEqual(1, Grid.GetColumn(Text(card, "CountTextBlock")));
        Assert.AreEqual(7, card.Rows.Count);
        Assert.AreEqual(1, card.Rows.Count(row => row.ActiveVisibility == Visibility.Visible));
        Assert.AreEqual(0, card.Rows.Count(row => row.CompletedVisibility == Visibility.Visible));
        Assert.AreEqual(6, card.Rows.Count(row => row.WaitingVisibility == Visibility.Visible));
        Assert.AreEqual("Checking · Estimated 0%", card.Rows[0].Status);
        ItemsControl rows = (ItemsControl)card.FindName("ProgressRowsItemsControl");
        Border rowSurface = (Border)rows.ItemTemplate.LoadContent();
        FontIcon completedGlyph = (FontIcon)rowSurface.FindName("CompletedGlyphIcon");
        Assert.AreEqual("\uE73E", completedGlyph.Glyph);
        Assert.AreEqual(HorizontalAlignment.Center, completedGlyph.HorizontalAlignment);
        Assert.AreEqual(VerticalAlignment.Center, completedGlyph.VerticalAlignment);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Apply_LaterStageReplacesRowsWithoutRetainingStaleState()
    {
        HardwareInspectionProgressCard card = new();
        card.Apply(_factory.CreateActive(HardwareInspectionStage.StartingHardwareInspection));
        card.Apply(_factory.CreateActive(HardwareInspectionStage.NormalisingHardwareInformation));

        Assert.AreEqual("Normalising hardware information", Text(card, "TitleTextBlock").Text);
        Assert.AreEqual("5 of 7 stages complete", Text(card, "CountTextBlock").Text);
        Assert.AreEqual(7, card.Rows.Count);
        Assert.AreEqual(5, card.Rows.Count(row => row.CompletedVisibility == Visibility.Visible));
        Assert.AreEqual(1, card.Rows.Count(row => row.ActiveVisibility == Visibility.Visible));
        Assert.AreEqual(1, card.Rows.Count(row => row.WaitingVisibility == Visibility.Visible));
        Assert.AreEqual("Checking · Estimated 0%", card.Rows[5].Status);
        Assert.AreEqual("Waiting", card.Rows[6].Status);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Apply_RejectsNonActivePresentation()
    {
        HardwareInspectionProgressCard card = new();

        Assert.Throws<ArgumentException>(() => card.Apply(
            _factory.CreateTerminal(HardwareInspectionOutcome.Completed)));
    }

    [UITestMethod]
    [TestCategory("EstimatedProgress")]
    public void Stopping_FreezesExactDisplayedFrameUntilCancelledResetsRows()
    {
        HardwareInspectionProgressCard card = new();
        object owner = new();
        var groups = new Dictionary<HardwareInspectionStage, (int Completed, int Total)>
        {
            [HardwareInspectionStage.ReadingProcessorInformation] = (1, 1),
            [HardwareInspectionStage.ReadingSystemMemory] = (1, 2),
        };
        card.Apply(_factory.CreateActive(
            HardwareInspectionStage.ReadingSystemMemory,
            groups,
            owner));
        ProgressBar bar = (ProgressBar)card.FindName("OverallProgressBar");
        TextBlock percentage = Text(card, "OverallPercentage");
        double frozenValue = bar.Value;
        string frozenPercentage = percentage.Text;
        string[] frozenStatuses = card.Rows.Select(row => row.Status).ToArray();

        card.ApplyStopping(_factory.CreateStopping());
        Assert.AreEqual(HardwareInspectionPresentationKind.Stopping,
            card.CurrentState?.Kind);
        Assert.AreEqual(frozenValue, bar.Value);
        Assert.AreEqual(frozenPercentage, percentage.Text);
        CollectionAssert.AreEqual(frozenStatuses,
            card.Rows.Select(row => row.Status).ToArray());
        Assert.IsFalse(card.Rows.Any(row => row.IsActive));
        StringAssert.Contains(AutomationProperties.GetName(card), "stopping");

        for (int index = 0; index < 4; index++)
        {
            InvokePrivate(card, "RefreshEstimatedValues");
        }
        Assert.AreEqual(frozenValue, bar.Value);
        Assert.AreEqual(frozenPercentage, percentage.Text);
        CollectionAssert.AreEqual(frozenStatuses,
            card.Rows.Select(row => row.Status).ToArray());

        card.ApplyStopping(_factory.CreateTerminal(HardwareInspectionOutcome.Cancelled));
        Assert.AreEqual(
            "Hardware inspection cancelled. No hardware report was created.",
            AutomationProperties.GetName(card));
        Assert.AreEqual(1,
            card.Rows.Count(row => row.Status == "Cancelled"));
        Assert.IsFalse(card.Rows.Any(row =>
            row.Status.Contains("Checking", StringComparison.Ordinal)
            || row.DisplayStatus.Contains('%')));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task PendingAutomationProjection_DoesNotTouchDetachedRows_AndReloadReprojects()
    {
        HardwareInspectionProgressCard card = new();
        HardwareInspectionPresentationState state =
            _factory.CreateActive(HardwareInspectionStage.CreatingHardwareReport);
        var firstLoaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var unloaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        card.Loaded += (_, _) => firstLoaded.TrySetResult(true);
        card.Unloaded += (_, _) => unloaded.TrySetResult(true);
        Window window = new() { Content = card };
        try
        {
            window.Activate();
            await firstLoaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            long firstGeneration = AutomationProjectionGeneration(card);
            Assert.IsTrue(ManagedLoaded(card));
            card.Apply(state);

            window.Content = null;
            await unloaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.IsFalse(ManagedLoaded(card));
            Assert.IsTrue(AutomationProjectionGeneration(card) > firstGeneration);
            InvokePrivate(card, "RefreshRealizedAutomationProjection", firstGeneration);
            InvokePrivate(card, "ProgressRowsItemsControl_LayoutUpdated", null, new object());
            Assert.IsFalse(AutomationProjectionPending(card));

            card.Apply(state);
            InvokePrivate(
                card,
                "RefreshRealizedAutomationProjection",
                AutomationProjectionGeneration(card));
            Assert.IsFalse(AutomationProjectionPending(card));

            var reloaded = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            card.Loaded += (_, _) => reloaded.TrySetResult(true);
            window.Content = card;
            await reloaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.IsTrue(ManagedLoaded(card));
            Assert.IsTrue(AutomationProjectionGeneration(card) > firstGeneration);
            card.UpdateLayout();
            await Task.Yield();
            card.UpdateLayout();

            ListView rows = (ListView)card.FindName("ProgressRowsItemsControl");
            ListViewItem realized = Assert.IsInstanceOfType<ListViewItem>(
                rows.ContainerFromIndex(0));
            Assert.AreEqual(card.Rows[0].AccessibleName, AutomationProperties.GetName(realized));
            Assert.AreEqual(card.Rows[0].Status, AutomationProperties.GetItemStatus(realized));
            Assert.AreEqual(1, AutomationProperties.GetPositionInSet(realized));
            Assert.AreEqual(card.Rows.Count, AutomationProperties.GetSizeOfSet(realized));
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void AutomationProjection_CoalescesPerGeneration_AndRejectsOldCallbacks()
    {
        HardwareInspectionProgressCard card = new();
        Assert.IsFalse(ManagedLoaded(card));
        Assert.IsFalse(AutomationProjectionPending(card));

        InvokePrivate(card, "HardwareInspectionProgressCard_Loaded", card, null);
        long firstGeneration = AutomationProjectionGeneration(card);
        Assert.IsTrue(ManagedLoaded(card));
        Assert.IsTrue(AutomationProjectionPending(card));
        Assert.AreEqual(firstGeneration, PendingAutomationProjectionGeneration(card));

        InvokePrivate(card, "RequestAutomationProjectionRefresh");
        Assert.IsTrue(AutomationProjectionPending(card));
        Assert.AreEqual(firstGeneration, PendingAutomationProjectionGeneration(card));

        InvokePrivate(card, "HardwareInspectionProgressCard_Unloaded", card, null);
        Assert.IsFalse(ManagedLoaded(card));
        Assert.IsFalse(AutomationProjectionPending(card));
        Assert.AreEqual(-1L, PendingAutomationProjectionGeneration(card));
        long detachedGeneration = AutomationProjectionGeneration(card);
        Assert.IsTrue(detachedGeneration > firstGeneration);

        InvokePrivate(card, "HardwareInspectionProgressCard_Loaded", card, null);
        long reloadedGeneration = AutomationProjectionGeneration(card);
        Assert.IsTrue(reloadedGeneration > detachedGeneration);
        Assert.IsTrue(AutomationProjectionPending(card));
        Assert.AreEqual(reloadedGeneration, PendingAutomationProjectionGeneration(card));

        InvokePrivate(card, "RefreshRealizedAutomationProjection", firstGeneration);
        Assert.IsTrue(AutomationProjectionPending(card));
        Assert.AreEqual(reloadedGeneration, PendingAutomationProjectionGeneration(card));

        InvokePrivate(card, "ClearPendingAutomationProjection", reloadedGeneration);
        Assert.IsFalse(AutomationProjectionPending(card));
        Assert.AreEqual(-1L, PendingAutomationProjectionGeneration(card));
    }

    private static bool AutomationProjectionPending(HardwareInspectionProgressCard card) =>
        (bool)typeof(HardwareInspectionProgressCard).GetField(
            "automationProjectionPending",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(card)!;

    private static bool ManagedLoaded(HardwareInspectionProgressCard card) =>
        (bool)typeof(HardwareInspectionProgressCard).GetField(
            "managedLoaded",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(card)!;

    private static long AutomationProjectionGeneration(HardwareInspectionProgressCard card) =>
        (long)typeof(HardwareInspectionProgressCard).GetField(
            "automationProjectionGeneration",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(card)!;

    private static long PendingAutomationProjectionGeneration(
        HardwareInspectionProgressCard card) =>
        (long)typeof(HardwareInspectionProgressCard).GetField(
            "pendingAutomationProjectionGeneration",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(card)!;

    private static void InvokePrivate(
        HardwareInspectionProgressCard card,
        string name,
        params object?[] arguments) =>
        typeof(HardwareInspectionProgressCard).GetMethod(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(card, arguments);

    private static TextBlock Text(HardwareInspectionProgressCard card, string name) =>
        (TextBlock)card.FindName(name);
}
