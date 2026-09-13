using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls;

[TestClass]
[DoNotParallelize]
public sealed class ModelInspectionDisclosureTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public void AcceptedClaim_AllowsReverseBeforePresentationDrain_AndRollback()
    {
        var disclosure = new InspectionDisclosure
        {
            HeaderContent = new TextBlock { Text = "Inspection details" },
            ViewportContent = new TextBlock { Text = "Safe details" }
        };
        List<bool> requests = [];
        disclosure.ToggleRequested += (_, args) => requests.Add(args.IsExpanded);

        disclosure.RequestTargetState(isExpanded: true);
        disclosure.ClaimTargetState(isExpanded: true);
        disclosure.RequestTargetState(isExpanded: false);
        disclosure.RollbackTargetStateClaim(isExpanded: true);
        disclosure.RequestTargetState(isExpanded: true);

        CollectionAssert.AreEqual(new[] { true, false, true }, requests);
        Assert.IsFalse(disclosure.IsExpanded);
        Assert.AreEqual(Visibility.Collapsed, disclosure.ViewportTarget.Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void PageOwnedCards_DoNotCompleteDisclosureDuringPresentationAssignment()
    {
        var model = new InspectionModelCard
        {
            IsDisclosureStateExternallyOwned = true
        };
        model.Presentation = CreateExpandedModelPresentation();

        var content = new InspectionContentCard
        {
            IsDisclosureStateExternallyOwned = true
        };
        content.Presentation = CreateExpandedContentPresentation();

        Assert.IsFalse(model.ActiveDisclosure!.IsExpanded);
        Assert.IsFalse(content.ActiveDisclosure!.IsExpanded);

        model.PrepareDisclosureTarget(isExpanded: true);
        content.PrepareDisclosureTarget(isExpanded: true);

        Assert.IsFalse(model.ActiveDisclosure.IsExpanded);
        Assert.IsFalse(content.ActiveDisclosure.IsExpanded);
        Assert.AreEqual(Visibility.Visible, model.ActiveDisclosure.ViewportTarget.Visibility);
        Assert.AreEqual(Visibility.Visible, content.ActiveDisclosure.ViewportTarget.Visibility);

        model.CompleteDisclosureTarget(isExpanded: true);
        content.CompleteDisclosureTarget(isExpanded: true);

        Assert.IsTrue(model.ActiveDisclosure.IsExpanded);
        Assert.IsTrue(content.ActiveDisclosure.IsExpanded);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ProgressBridge_AnimatesOnlyRealizedChangedRowTargets()
    {
        var rows = new InspectionProgressRows();
        rows.Reset(new ModelInspectionRenderKey(1, 0));
        var control = new InspectionContentCard
        {
            Presentation = InitialInspectionProgressPresentationFactory.Create(rows)
        };
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        control.Loaded += (_, _) => loaded.TrySetResult(true);
        var window = new Window { Content = control };

        try
        {
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            control.UpdateLayout();
            var repeater = (ItemsRepeater)control.FindName("ProgressItemsRepeater");
            var firstElement = repeater.TryGetElement(0) as FrameworkElement;
            Assert.IsNotNull(firstElement);
            Assert.AreEqual(0, repeater.GetElementIndex(firstElement));
            Assert.IsNull(firstElement.DataContext,
                "Compiled x:Bind row templates are associated by repeater index.");
            Assert.IsNotNull(firstElement.FindName("ProgressStatusMotionTarget"));
            Assert.IsNotNull(firstElement.FindName("ProgressDetailMotionTarget"));
            InspectionProgressRowsApplyResult changes = rows.Apply(
                CreateProgressUpdate(revision: 1));
            control.UpdateLayout();
            var driver = new RecordingAnimationDriver();
            ModelInspectionVisualOperationKey key = new(
                new ModelInspectionRenderKey(1, 1),
                interactionRevision: 0);

            control.AnimateProgressChanges(
                changes,
                driver,
                key,
                candidate => candidate == key);

            Assert.HasCount(1, driver.StageStatusTargets);
            Assert.HasCount(1, driver.ActiveDetailTargets);
            Assert.AreEqual(key, driver.LastOperationKey);
            Assert.IsTrue(driver.StageStatusTargets[0].XamlRoot is not null);
            Assert.IsTrue(driver.ActiveDetailTargets[0].XamlRoot is not null);
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ProgressBridge_UnrealizedRowsStartNoMotion()
    {
        var rows = new InspectionProgressRows();
        rows.Reset(new ModelInspectionRenderKey(1, 0));
        var control = new InspectionContentCard
        {
            Presentation = InitialInspectionProgressPresentationFactory.Create(rows)
        };
        InspectionProgressRowsApplyResult changes = rows.Apply(
            CreateProgressUpdate(revision: 1));
        var driver = new RecordingAnimationDriver();
        ModelInspectionVisualOperationKey key = new(
            new ModelInspectionRenderKey(1, 1),
            interactionRevision: 0);

        control.AnimateProgressChanges(
            changes,
            driver,
            key,
            candidate => candidate == key);

        Assert.IsEmpty(driver.StageStatusTargets);
        Assert.IsEmpty(driver.ActiveDetailTargets);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task OutcomeFocusTarget_IsProgrammaticallyFocusableButNotATabStop()
    {
        var control = new InspectionOutcomeCard
        {
            Presentation = new InspectionOutcomePresentation
            {
                Kind = InspectionOutcomePresentationKind.OperationalFailure,
                Tone = InspectionOutcomeTone.Error,
                Title = "Inspection interrupted",
                Message = "Model inspection could not be completed.",
                AutomationName =
                    "Inspection interrupted. Model inspection could not be completed."
            }
        };
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        control.Loaded += (_, _) => loaded.TrySetResult(true);
        var window = new Window { Content = control };

        try
        {
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            control.UpdateLayout();

            Assert.IsInstanceOfType<Control>(control.FocusTarget);
            var focusControl = (Control)control.FocusTarget;
            Assert.IsFalse(focusControl.IsTabStop);
            Assert.IsTrue(focusControl.IsLoaded);
            Assert.AreEqual(Visibility.Visible, focusControl.Visibility);
            Assert.IsTrue(focusControl.IsEnabled);
            Assert.IsGreaterThan(0d, focusControl.ActualWidth);
            Assert.IsGreaterThan(0d, focusControl.ActualHeight);
            Assert.AreEqual(
                AccessibilityView.Content,
                AutomationProperties.GetAccessibilityView(focusControl));
            Assert.AreEqual(
                AutomationHeadingLevel.Level2,
                AutomationProperties.GetHeadingLevel(focusControl));
            AutomationPeer focusPeer = FrameworkElementAutomationPeer
                .CreatePeerForElement(focusControl);
            Assert.IsNotNull(focusPeer);
            Assert.AreEqual("Inspection interrupted", focusPeer.GetName());
            Assert.IsTrue(control.FocusOutcome());
            Assert.IsFalse(focusControl.IsTabStop);
            Assert.AreSame(
                control.FocusTarget,
                FocusManager.GetFocusedElement(control.XamlRoot));
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    private static InspectionProgressRowsUpdate CreateProgressUpdate(
        long revision) =>
        new(
            new ModelInspectionProgressRegionKey(
                ModelInspectionStage.CheckModelPackage,
                ModelInspectionStageStatus.Active,
                completedStageCount: 0,
                stageCount: 5,
                stageFraction: null,
                detail: "Checking the model package."),
            new ModelInspectionRenderKey(1, revision),
            "0 of 5 checks complete");

    private static InspectionModelCardPresentation CreateExpandedModelPresentation() =>
        new()
        {
            DisplayMode = InspectionModelCardMode.Detailed,
            ModelName = "Granite",
            FormatShortName = "GGUF",
            OverviewFormatBadgeText = "GGUF",
            Publisher = "Not reported",
            FormatName = "GGUF",
            Quantisation = "Q4_K_M",
            ParameterCount = "3B",
            ModelType = "Not reported",
            DeclaredContext = "4,096 tokens",
            FileSize = "4 KB",
            InspectionChecksSummary = "All 5 inspection checks passed",
            InspectionChecks = Enumerable.Range(1, 5)
                .Select(index => new InspectionCheckPresentation
                {
                    Title = $"Check {index}",
                    Detail = "Passed",
                    Status = InspectionCheckStatus.Passed,
                    StatusText = "Passed",
                    AutomationName = $"Check {index}. Passed."
                })
                .ToArray(),
            InspectionDetailsVisibility = Visibility.Visible,
            IsInspectionDetailsExpanded = true
        };

    private static InspectionContentCardPresentation CreateExpandedContentPresentation() =>
        new()
        {
            Mode = InspectionContentCardMode.Warnings,
            SectionTitle = "Inspection warning",
            Items =
            [
                new InspectionContentItemPresentation
                {
                    Title = "Chat template",
                    Detail = "Not reported",
                    Status = InspectionContentStatus.Warning,
                    StatusText = "Warning",
                    AutomationName = "Chat template. Warning."
                }
            ],
            DisclosureVisibility = Visibility.Visible,
            DisclosureSummary = "Inspection report",
            ExpandedItems =
            [
                new InspectionContentItemPresentation
                {
                    Title = "Chat template",
                    Detail = "Not reported",
                    Status = InspectionContentStatus.Warning,
                    StatusText = "Warning",
                    AutomationName = "Chat template. Warning."
                }
            ],
            IsExpanded = true
        };

    private sealed class RecordingAnimationDriver :
        IModelInspectionAnimationDriver
    {
        internal List<UIElement> StageStatusTargets { get; } = [];

        internal List<UIElement> ActiveDetailTargets { get; } = [];

        internal ModelInspectionVisualOperationKey? LastOperationKey { get; private set; }

        public void StartStageStatus(
            UIElement target,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed)
        {
            StageStatusTargets.Add(target);
            LastOperationKey = key;
        }

        public void StartActiveDetail(
            UIElement target,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed)
        {
            ActiveDetailTargets.Add(target);
            LastOperationKey = key;
        }

        public void StartDisclosure(
            UIElement chevron,
            FrameworkElement viewport,
            IReadOnlyList<UIElement> followingElements,
            bool isExpanded,
            IReadOnlyList<double> previousTopOffsets,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed) =>
            LastOperationKey = key;

        public void StartTerminal(
            UIElement outgoing,
            UIElement incoming,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed) =>
            LastOperationKey = key;

        public void CancelAll()
        {
        }

        public void Dispose()
        {
        }
    }
}
