using GraniteEdgeAI.Features.ModelOptimization;
using GraniteEdgeAI.Features.ModelOptimization.Export;
using GraniteEdgeAI.Features.ModelOptimization.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.UnitTests.Features.ModelHardwareCompatibility;
using GraniteEdgeAI.UnitTests.Features.ModelOptimization.Fixtures;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using Microsoft.UI.Xaml.Controls;

namespace GraniteEdgeAI.UnitTests.Features.ModelOptimization;

[TestClass]
public sealed class OptimizationVisualContractTests
{
    [UITestMethod]
    public void AllSevenStageRingsRemainCenteredWithoutMarginOffset()
    {
        OptimizationPage page = new();
        ProgressRing[] rings = ProgressRings(page);
        Assert.AreEqual(7, rings.Length);
        foreach (ProgressRing ring in rings)
        {
            Assert.AreEqual(new Thickness(0), ring.Margin, ring.Name);
            Assert.AreEqual(20d, ring.Width, ring.Name);
            Assert.AreEqual(20d, ring.Height, ring.Name);
            Assert.AreEqual(HorizontalAlignment.Center, ring.HorizontalAlignment, ring.Name);
            Assert.AreEqual(VerticalAlignment.Center, ring.VerticalAlignment, ring.Name);
            Assert.AreEqual(AccessibilityView.Raw,
                AutomationProperties.GetAccessibilityView(ring), ring.Name);
        }
    }

    [UITestMethod]
    [TestCategory("ProgressCompletion")]
    public async Task LiveOptimisationCompletedRowCatchesUpWithoutDelayingNextStage()
    {
        var page = new OptimizationPage();
        var current = OptimizationFixtureCatalog.All.Single(item => item.Id == "progress-optimise").Presentation;
        var next = OptimizationFixtureCatalog.All.Single(item => item.Id == "progress-validate").Presentation;
        page.ApplyPresentation(current);
        await using var host = await WinUiRenderHost.ShowAsync(page, 1000, 650);
        StringAssert.Contains(((TextBlock)page.FindName("OptimizationStage3Status")).Text, "Active");
        Assert.AreEqual(current.OptimizationPlanId, next.OptimizationPlanId);
        page.ApplyPresentation(next);
        void AssertTerminal()
        {
            Assert.AreEqual("Complete", ((TextBlock)page.FindName("OptimizationStage3Status")).Text);
            var row = (FrameworkElement)page.FindName("OptimizationStage3");
            Assert.AreEqual("Complete", AutomationProperties.GetItemStatus(row));
            StringAssert.Contains(AutomationProperties.GetName(row), "Complete");
            StringAssert.Contains(((TextBlock)page.FindName("OptimizationStage4Status")).Text, "Active");
            Assert.IsTrue(((ProgressBar)page.FindName("OptimizationOverallProgress")).Value < 7);
        }
        AssertTerminal();
        var tick = typeof(OptimizationPage).GetMethod("UpdateOperationEstimate", BindingFlags.Instance | BindingFlags.NonPublic)!;
        for (int i = 0; i < 4; i++) { tick.Invoke(page, null); AssertTerminal(); }
    }
    [UITestMethod]
    [TestCategory("EstimatedProgress")]
    public void LiveOptimisationPageLabelsStageAggregateAsEstimated()
    {
        var page = new OptimizationPage();
        page.ApplyPresentation(OptimizationFixtureCatalog.All.Single(item => item.Id == "progress-optimise").Presentation);
        StringAssert.Contains(((TextBlock)page.FindName("OptimizationOverallPercentage")).Text, "Estimated");
        StringAssert.Contains(((TextBlock)page.FindName("OptimizationStage3Status")).Text, "Estimated");
        Assert.IsTrue(((ProgressBar)page.FindName("OptimizationOverallProgress")).Value < 7);
    }

    public TestContext TestContext { get; set; } = null!;

    private static readonly string[] ProgressRingNames =
    [
        "OptimizationStage1OrbitPresenter",
        "OptimizationStage2OrbitPresenter",
        "OptimizationStage3OrbitPresenter",
        "OptimizationStage4OrbitPresenter",
        "OptimizationStage5OrbitPresenter",
        "OptimizationStage6OrbitPresenter",
        "OptimizationStage7OrbitPresenter"
    ];

    [UITestMethod]
    public void GalleryCoversEveryFixtureOnTheModernLightCanvas()
    {
        OptimizationFixtureGalleryPage gallery = new();

        Assert.AreEqual(13, gallery.FixtureCount);
        Assert.IsNotNull(gallery.PreviewPresentation);
        Assert.AreEqual(ElementTheme.Light, gallery.RequestedTheme);
    }

    [UITestMethod]
    public void EveryFixtureRendersThroughTheCanonicalPage()
    {
        OptimizationPage page = new();

        foreach (OptimizationFixture fixture in OptimizationFixtureCatalog.All)
        {
            page.ApplyPresentation(fixture.Presentation);
            Assert.AreSame(fixture.Presentation, page.Presentation, fixture.Id);
            Assert.AreEqual(fixture.Presentation.Configuration.Weights,
                ((TextBlock)page.FindName("OptimizationWeights")).Text, fixture.Id);
            Assert.AreEqual(fixture.Presentation.Configuration.Backend,
                ((TextBlock)page.FindName("OptimizationBackend")).Text, fixture.Id);
            Assert.IsTrue(((TextBlock)page.FindName("OptimizationIdentityDetail"))
                .Text.Contains(fixture.Presentation.Configuration.Device), fixture.Id);

            if (fixture.Presentation.Kind == OptimizationPageStateKind.Confirming)
            {
                Assert.AreEqual(Visibility.Visible,
                    ((Grid)page.FindName("OptimizationConfirmingPanel")).Visibility,
                    fixture.Id);
                Assert.AreEqual(fixture.Presentation.Title,
                    ((TextBlock)page.FindName("OptimizationConfirmingHeading")).Text,
                    fixture.Id);
            }
            else if (fixture.Presentation.Kind == OptimizationPageStateKind.Running)
            {
                Assert.AreEqual(Visibility.Visible,
                    ((Grid)page.FindName("OptimizationProgressPanel")).Visibility,
                    fixture.Id);
            }
            else
            {
                Assert.AreEqual(Visibility.Visible,
                    ((Grid)page.FindName("OptimizationTerminalPanel")).Visibility,
                    fixture.Id);
                Assert.AreEqual(fixture.Presentation.Title,
                    ((TextBlock)page.FindName("OptimizationTerminalHeading")).Text,
                    fixture.Id);
                string[] visibleActions =
                    new[]
                    {
                        (Button)page.FindName("BtnOptimizationTerminalBack"),
                        (Button)page.FindName("BtnOptimizationAlternative"),
                        (Button)page.FindName("BtnOptimizationPrimary")
                    }
                    .Where(button => button.Visibility == Visibility.Visible)
                    .Select(button => button.Content?.ToString() ?? string.Empty)
                    .ToArray();
                OptimizationActionPresentation? back = fixture.Presentation.Actions
                    .FirstOrDefault(action => action.Command is
                        OptimizationCommand.BackToCompatibility
                        or OptimizationCommand.ImportAnotherModel);
                OptimizationActionPresentation? alternative = fixture.Presentation.Actions
                    .FirstOrDefault(action => !action.IsPrimary
                        && action.Command is not OptimizationCommand.BackToCompatibility
                        and not OptimizationCommand.ImportAnotherModel);
                OptimizationActionPresentation? primary = fixture.Presentation.Actions
                    .FirstOrDefault(action => action.IsPrimary
                        && action.Command != OptimizationCommand.BackToCompatibility);
                string[] expectedActions = new[] { back, alternative, primary }
                    .Where(action => action is not null)
                    .Select(action => action!.Text)
                    .ToArray();
                CollectionAssert.AreEqual(
                    expectedActions,
                    visibleActions,
                    fixture.Id);
            }
        }

        Assert.AreEqual(13, OptimizationFixtureCatalog.All.Select(item => item.Id).Distinct().Count());
        Assert.IsNull(page.FindName("ConfigurationCard"));
        Assert.IsNull(page.FindName("DestinationCard"));
        Assert.IsNotNull(page.FindName("OptimizationExportPanel"));
    }

    [UITestMethod]
    public void SuccessUsesThreeActionsAndHidesEmptyDetailsAndExportReservation()
    {
        OptimizationPage page = new();

        foreach (string id in new[] { "success-persistent", "success-runtime-profile" })
        {
            page.ApplyPresentation(OptimizationFixtureCatalog.All.Single(
                item => item.Id == id).Presentation);
            Button import = (Button)page.FindName("BtnOptimizationTerminalBack");
            Button secondary = (Button)page.FindName("BtnOptimizationAlternative");
            Button chat = (Button)page.FindName("BtnOptimizationPrimary");

            Assert.AreEqual("Import another model", import.Content);
            Assert.AreEqual("OptimizationAction.ImportAnotherModel",
                AutomationProperties.GetAutomationId(import));
            Assert.AreEqual(Visibility.Visible, secondary.Visibility);
            Assert.AreEqual("Chat with this model", chat.Content);
            Assert.AreEqual(Visibility.Collapsed,
                ((Expander)page.FindName("OptimizationTerminalDetailsExpander")).Visibility);
            Assert.AreEqual(Visibility.Collapsed,
                ((StackPanel)page.FindName("OptimizationExportActions")).Visibility);
        }

        page.ApplyPresentation(OptimizationFixtureCatalog.All.Single(
            item => item.Id == "failed").Presentation);
        Assert.AreEqual("OptimizationAction.BackToCompatibility",
            AutomationProperties.GetAutomationId(
                (Button)page.FindName("BtnOptimizationTerminalBack")));
        Assert.AreEqual(Visibility.Visible,
            ((Expander)page.FindName("OptimizationTerminalDetailsExpander")).Visibility);
    }

    [UITestMethod]
    public void ExportSuccessCopyIsSpecificAndEveryOtherStateRestoresSaveInstructions()
    {
        OptimizationPage page = new();
        page.ApplyPresentation(OptimizationFixtureCatalog.All.Single(
            item => item.Id == "success-persistent").Presentation);
        MethodInfo apply = typeof(OptimizationPage).GetMethod(
            "ApplyExportState", BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, types: [typeof(OptimizationExportViewState)], modifiers: null)!;
        TextBlock heading = (TextBlock)page.FindName("OptimizationExportHeading");
        TextBlock detail = (TextBlock)page.FindName("OptimizationExportDetail");

        apply.Invoke(page,
        [
            new OptimizationExportViewState(
                OptimizationExportStateKind.Succeeded, "Saved successfully.")
        ]);
        Assert.AreEqual("Model saved successfully", heading.Text);
        Assert.AreEqual(
            "Your optimised model has been saved. Your original model remains unchanged.",
            detail.Text);

        foreach (OptimizationExportStateKind kind in Enum.GetValues<OptimizationExportStateKind>()
            .Where(kind => kind != OptimizationExportStateKind.Succeeded))
        {
            apply.Invoke(page, [new OptimizationExportViewState(kind, kind.ToString())]);
            Assert.AreEqual("Save the validated model", heading.Text, kind.ToString());
            Assert.AreEqual(
                "Choose where to save this verified result. Your original model remains unchanged.",
                detail.Text,
                kind.ToString());
        }

        apply.Invoke(page,
        [
            new OptimizationExportViewState(
                OptimizationExportStateKind.Succeeded, "Saved successfully.")
        ]);
        apply.Invoke(page,
        [
            new OptimizationExportViewState(
                OptimizationExportStateKind.Ready, "Ready to save.")
        ]);
        Assert.AreEqual("Save the validated model", heading.Text);
        Assert.AreEqual(
            "Choose where to save this verified result. Your original model remains unchanged.",
            detail.Text);
    }

    [UITestMethod]
    public async Task GgufRuntimeBundle_ReadyAndSavedStatesAttachPngEvidence()
    {
        (VerifiedGgufRuntimeBundleExportTarget target,
            OptimizationExecutionPlan plan) = RuntimeTarget();
        OptimizationCandidate originalCandidate = plan.Candidate;
        RouteConfiguration originalConfiguration = plan.Candidate.Configuration;
        OptimizationExecutionPayload originalPayload = plan.ExecutionPayload;
        string originalDescriptor = plan.Candidate.CanonicalDescriptor;
        string originalConfigurationSha256 = plan.ConfigurationSha256;
        OptimizationPresentationState presentation =
            OptimizationPresentationFactory.Success(
                plan.Preference,
                OptimizationConfigurationProjection.From(plan),
                target.OptimizationPlanId,
                target.ConfigurationSha256,
                OptimizationRoute.Gguf);
        Assert.AreEqual("llama.cpp", presentation.Configuration.Backend);
        Assert.AreEqual("CPU", presentation.Configuration.Device);
        Assert.AreSame(originalCandidate, plan.Candidate);
        Assert.AreSame(originalConfiguration, plan.Candidate.Configuration);
        Assert.AreSame(originalPayload, plan.ExecutionPayload);
        Assert.AreEqual(originalDescriptor, plan.Candidate.CanonicalDescriptor);
        Assert.AreEqual(originalConfigurationSha256, plan.ConfigurationSha256);
        Assert.IsTrue(plan.MatchesExecutionPayload(originalPayload));
        OptimizationPage page = new();
        page.ApplyPresentation(presentation);
        Assert.AreEqual(
            $"{presentation.PreferenceLabel} · llama.cpp · CPU",
            ((TextBlock)page.FindName("OptimizationIdentityDetail")).Text);
        Assert.IsTrue(page.BindVerifiedRuntimeBundleExport(
            target, new SuccessfulRuntimeBundleExportService()));

        await using WinUiRenderHost host =
            await WinUiRenderHost.ShowAsync(page, 900, 760);
        AssertExportCopy(
            page,
            "Save model and runtime settings",
            "Choose where to save the unchanged model weights and verified runtime settings.",
            "Verified model and runtime settings export.");
        RenderedFrame ready = await host.CaptureAsync();
        string readyAttachment = await ready.SavePngAsync("gguf-runtime-ready.png");
        TestContext.AddResultFile(readyAttachment);

        Assert.IsTrue(await page.TryStartExportAsync());
        AssertExportCopy(
            page,
            "Model and settings saved successfully",
            "The unchanged model weights and verified runtime settings were saved together.",
            "Verified model and runtime settings export.");
        RenderedFrame saved = await host.CaptureAsync();
        string savedAttachment = await saved.SavePngAsync("gguf-runtime-saved.png");
        TestContext.AddResultFile(savedAttachment);
    }

    [UITestMethod]
    public async Task OpenVinoPersistent_ReadyAndSavedStatesAttachPngEvidence()
    {
        CompatibilityViewModelTests.RealExactPlanningFixture fixture =
            CompatibilityViewModelTests.RealExactFixture();
        Assert.IsTrue(fixture.Authority.TryGetOptimizationAuthority(
            OptimizationRoute.OpenVino,
            out IOptimizationExecutionPayloadComposer? composer,
            out OptimizationIssuanceAuthority? issuanceAuthority));
        OptimizationExecutionPlan plan = fixture.Evaluation.PlanningSession!.Issue(
            OptimizationPreferenceSelection.Manual(50),
            composer!,
            issuanceAuthority!,
            fixture.TimeProvider);
        Assert.IsTrue(plan.ProducesPersistentArtifact,
            "The OpenVINO screenshot must use a real issued persistent plan.");
        OptimizationConfigurationPresentation configuration =
            OptimizationConfigurationProjection.From(plan);
        OptimizationPresentationState presentation =
            OptimizationPresentationFactory.Success(
                plan.Preference,
                configuration,
                plan.OptimizationPlanId,
                plan.ConfigurationSha256,
                OptimizationRoute.OpenVino);
        var target = new VerifiedPersistentExportTarget(
            OptimizationRoute.OpenVino,
            plan.OptimizationPlanId,
            plan.ConfigurationSha256,
            plan.Binding.ModelSha256,
            sourceUnchanged: true,
            "openvino-output",
            new string('d', 64),
            4096);
        OptimizationPage page = new();
        page.ApplyPresentation(presentation);
        Assert.AreEqual(
            "Balanced · OpenVINO · Cpu",
            ((TextBlock)page.FindName("OptimizationIdentityDetail")).Text);
        Assert.IsTrue(page.BindVerifiedExport(
            target, new SuccessfulPersistentExportService()));

        await using WinUiRenderHost host =
            await WinUiRenderHost.ShowAsync(page, 900, 760);
        AssertExportCopy(
            page,
            "Save the validated model",
            "Choose where to save this verified result. Your original model remains unchanged.",
            "Verified model export.");
        RenderedFrame ready = await host.CaptureAsync();
        string readyAttachment = await ready.SavePngAsync(
            "openvino-persistent-ready.png");
        TestContext.AddResultFile(readyAttachment);

        Assert.IsTrue(await page.TryStartExportAsync());
        AssertExportCopy(
            page,
            "Model saved successfully",
            "Your optimised model has been saved. Your original model remains unchanged.",
            "Verified model export.");
        RenderedFrame saved = await host.CaptureAsync();
        string savedAttachment = await saved.SavePngAsync(
            "openvino-persistent-saved.png");
        TestContext.AddResultFile(savedAttachment);
    }

    [UITestMethod]
    public void GgufRuntimeBundleWithoutVerifiedBindingKeepsSaveDisabledAndPanelCollapsed()
    {
        OptimizationPresentationState seed = OptimizationFixtureCatalog.All.Single(
            item => item.Id == "success-runtime-profile").Presentation;
        OptimizationPage page = new();
        page.ApplyPresentation(seed);

        Assert.AreEqual(Visibility.Collapsed,
            ((FrameworkElement)page.FindName("OptimizationExportPanel")).Visibility);
        Button save = (Button)page.FindName("BtnOptimizationAlternative");
        Assert.AreEqual("Save model and settings", save.Content);
        Assert.IsFalse(save.IsEnabled);
    }

    [UITestMethod]
    public void GgufRuntimeBundleRejectsMismatchedTargetWithoutExposingExport()
    {
        (VerifiedGgufRuntimeBundleExportTarget target,
            OptimizationExecutionPlan plan) = RuntimeTarget();
        Guid planId = Guid.Parse("77777777-7777-4777-8777-777777777777");
        OptimizationPage page = new();
        page.ApplyPresentation(OptimizationPresentationFactory.Success(
            plan.Preference, OptimizationConfigurationProjection.From(plan), planId,
            target.ConfigurationSha256,
            OptimizationRoute.Gguf));

        Assert.IsFalse(page.BindVerifiedRuntimeBundleExport(
            target,
            new SuccessfulRuntimeBundleExportService()));
        Assert.AreEqual(Visibility.Collapsed,
            ((FrameworkElement)page.FindName("OptimizationExportPanel")).Visibility);
        Assert.IsFalse(((Button)page.FindName("BtnOptimizationAlternative")).IsEnabled);
        Assert.AreEqual(OptimizationExportStateKind.Unbound,
            page.ExportState.Kind);
    }

    [UITestMethod]
    public void EveryRunningFixtureActivatesOnlyItsAuthoritativeNativeProgressRing()
    {
        OptimizationPage page = new();

        foreach (OptimizationFixture fixture in OptimizationFixtureCatalog.All
            .Where(item => item.Presentation.Kind == OptimizationPageStateKind.Running))
        {
            page.ApplyPresentation(fixture.Presentation);
            ProgressRing[] rings = ProgressRings(page);
            int expectedActive = fixture.Presentation.ProgressRows
                .ToList()
                .FindIndex(row => row.Status == OptimizationStageStatus.Active);

            Assert.AreEqual(7, rings.Length, fixture.Id);
            Assert.AreEqual(1, rings.Count(ring => ring.IsActive), fixture.Id);
            Assert.AreEqual(1, rings.Count(ring => ring.Visibility == Visibility.Visible), fixture.Id);
            for (int index = 0; index < rings.Length; index++)
            {
                Assert.AreEqual(index == expectedActive, rings[index].IsActive, fixture.Id);
                Assert.AreEqual(
                    index == expectedActive ? Visibility.Visible : Visibility.Collapsed,
                    rings[index].Visibility,
                    fixture.Id);
                Assert.AreEqual(20d, rings[index].Width, fixture.Id);
                Assert.AreEqual(20d, rings[index].Height, fixture.Id);
                Assert.AreSame(
                    page.Resources["OptimizationAccentBrush"],
                    rings[index].Foreground,
                    fixture.Id);
                Assert.AreEqual(
                    AccessibilityView.Raw,
                    AutomationProperties.GetAccessibilityView(rings[index]),
                    fixture.Id);
            }
        }
    }

    [UITestMethod]
    public void ProgressTransitionsStopThePriorRingAndStopAllOutsideRunning()
    {
        OptimizationPage page = new();
        OptimizationPresentationState preflight = RunningFixture("progress-preflight");
        OptimizationPresentationState optimise = RunningFixture("progress-optimise");

        page.ApplyPresentation(preflight);
        AssertActiveRing(page, 0);

        page.ApplyPresentation(optimise);
        AssertActiveRing(page, 2);

        page.ApplyPresentation(OptimizationFixtureCatalog.All.Single(item => item.Id == "confirmation").Presentation);
        AssertAllRingsStopped(page);

        page.ApplyPresentation(optimise);
        AssertActiveRing(page, 2);
        page.ApplyPresentation(OptimizationFixtureCatalog.All.Single(item => item.Id == "failed").Presentation);
        AssertAllRingsStopped(page);
    }

    [UITestMethod]
    public void RejectedRunningCardinalityStopsEveryProgressRing()
    {
        OptimizationPage page = new();
        OptimizationPresentationState valid = RunningFixture("progress-preflight");

        page.ApplyPresentation(valid);
        AssertActiveRing(page, 0);

        page.ApplyPresentation(WithStatuses(valid,
            Enumerable.Repeat(OptimizationStageStatus.Waiting, 7).ToArray()));
        AssertAllRingsStopped(page);
        Assert.AreEqual("Optimisation progress unavailable",
            ((TextBlock)page.FindName("OptimizationProgressHeading")).Text);

        page.ApplyPresentation(WithStatuses(valid,
        [
            OptimizationStageStatus.Active,
            OptimizationStageStatus.Active,
            OptimizationStageStatus.Waiting,
            OptimizationStageStatus.Waiting,
            OptimizationStageStatus.Waiting,
            OptimizationStageStatus.Waiting,
            OptimizationStageStatus.Waiting
        ]));
        AssertAllRingsStopped(page);
        Assert.AreEqual("Optimisation progress unavailable",
            ((TextBlock)page.FindName("OptimizationProgressHeading")).Text);
    }

    [UITestMethod]
    public async Task RetiringThePageStopsEveryProgressRing()
    {
        OptimizationPage page = new();
        page.ApplyPresentation(RunningFixture("progress-publish"));
        AssertActiveRing(page, 6);

        await page.RetireForNavigationAsync();

        AssertAllRingsStopped(page);
    }

    private static void AssertExportCopy(
        OptimizationPage page,
        string heading,
        string detail,
        string accessiblePrefix)
    {
        Assert.AreEqual(Visibility.Visible,
            ((FrameworkElement)page.FindName("OptimizationExportPanel")).Visibility);
        Assert.AreEqual(heading,
            ((TextBlock)page.FindName("OptimizationExportHeading")).Text);
        Assert.AreEqual(detail,
            ((TextBlock)page.FindName("OptimizationExportDetail")).Text);
        Assert.IsTrue(
            AutomationProperties.GetName(
                (FrameworkElement)page.FindName("OptimizationExportPanel"))
                .StartsWith(accessiblePrefix, StringComparison.Ordinal));
    }

    private static (VerifiedGgufRuntimeBundleExportTarget Target,
        OptimizationExecutionPlan Plan) RuntimeTarget()
    {
        (OptimizationExecutionPlan plan, OptimizationExecutionResult result) =
            GgufRuntimeProfileBundleExportServiceTests.RuntimeProfileResult();
        return (VerifiedGgufRuntimeBundleExportTarget.FromExecution(plan, result), plan);
    }

    private sealed class SuccessfulRuntimeBundleExportService
        : IGgufRuntimeBundleExportService
    {
        public Task<OptimizationExportResult> ExportAsync(
            VerifiedGgufRuntimeBundleExportTarget target,
            IProgress<OptimizationExportProgress> progress,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(OptimizationExportResult.Succeeded(
                new GgufRuntimeBundleExportReceipt(
                    target, new string('1', 64), 256,
                    checked(target.SourceLengthBytes + 512))));
        }
    }

    private sealed class SuccessfulPersistentExportService
        : IOptimizationExportService
    {
        public Task<OptimizationExportResult> ExportAsync(
            VerifiedPersistentExportTarget target,
            IProgress<OptimizationExportProgress> progress,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(OptimizationExportResult.Succeeded(
                new OptimizationExportReceipt(target, "published-output")));
        }
    }

    private static OptimizationPresentationState RunningFixture(string id) =>
        OptimizationFixtureCatalog.All.Single(item => item.Id == id).Presentation;

    private static OptimizationPresentationState WithStatuses(
        OptimizationPresentationState source,
        IReadOnlyList<OptimizationStageStatus> statuses) =>
        new(
            source.Kind,
            source.Title,
            source.Summary,
            source.Tone,
            source.Preference,
            source.PreferenceLabel,
            source.Configuration,
            source.ProgressRows.Select((row, index) => row with { Status = statuses[index] }),
            source.Actions,
            source.CanCancel,
            source.SupportCode,
            source.OptimizationPlanId,
            source.ConfigurationSha256);

    private static ProgressRing[] ProgressRings(OptimizationPage page) =>
        ProgressRingNames.Select(name =>
        {
            object element = page.FindName(name);
            Assert.IsInstanceOfType<ProgressRing>(element, name);
            return (ProgressRing)element;
        }).ToArray();

    private static void AssertActiveRing(OptimizationPage page, int activeIndex)
    {
        ProgressRing[] rings = ProgressRings(page);
        for (int index = 0; index < rings.Length; index++)
        {
            Assert.AreEqual(index == activeIndex, rings[index].IsActive, ProgressRingNames[index]);
            Assert.AreEqual(
                index == activeIndex ? Visibility.Visible : Visibility.Collapsed,
                rings[index].Visibility,
                ProgressRingNames[index]);
        }
    }

    private static void AssertAllRingsStopped(OptimizationPage page)
    {
        foreach (ProgressRing ring in ProgressRings(page))
        {
            Assert.IsFalse(ring.IsActive);
            Assert.AreEqual(Visibility.Collapsed, ring.Visibility);
        }
    }
}
