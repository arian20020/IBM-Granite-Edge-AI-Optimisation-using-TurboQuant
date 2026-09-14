using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.Services;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using GraniteEdgeAI.Features.ModelInspection.Views;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Windows.System;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual;

[TestClass]
[DoNotParallelize]
public sealed class ModelInspectionAccessibilityTests
{
    private static readonly TimeSpan UiTimeout = TimeSpan.FromSeconds(10);

    [TestMethod]
    public void CanonicalPresentationFixture_RemainsX64CpuVocabOnlyWithoutGpu()
    {
        ModelInspectionRuntimeIdentity runtime =
            PresentationTestData.CreateEvidence().Runtime;

        Assert.AreEqual("X64", runtime.ProcessArchitecture);
        Assert.AreEqual("VocabOnly", runtime.InspectionMode);
        Assert.IsFalse(runtime.UsesCuda);
        Assert.IsFalse(runtime.UsesVulkan);
        Assert.AreEqual(0, runtime.GpuLayerCount);
        Assert.AreEqual("llama.dll", runtime.NativeLibraryName);
        StringAssert.Contains(runtime.RuntimeProfile, "cpu-win-x64-vocab-only");
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task InProcessKeyboardActivation_DisclosureRetainsFocusAndPeerState()
    {
        var disclosure = new InspectionDisclosure
        {
            HeaderContent = new TextBlock { Text = "Inspection details" },
            ViewportContent = new Button { Content = "Report action" }
        };
        AutomationProperties.SetName(disclosure, "Inspection details");
        List<bool> requestedStates = [];
        disclosure.ToggleRequested += (_, args) =>
            requestedStates.Add(args.IsExpanded);

        await using WinUiRenderHost host = await WinUiRenderHost.ShowAsync(
            disclosure,
            width: 500,
            height: 300);
        await host.CaptureAsync();

        Assert.IsTrue(disclosure.Focus(FocusState.Keyboard));
        Assert.AreSame(
            disclosure,
            FocusManager.GetFocusedElement(disclosure.XamlRoot));
        Assert.IsTrue(disclosure.HandleKeyboardActivation(
            VirtualKey.Enter,
            repeatCount: 1,
            wasKeyDown: false));
        CollectionAssert.AreEqual(new[] { true }, requestedStates);

        disclosure.PrepareTargetState(isExpanded: true);
        disclosure.CompleteTargetState(isExpanded: true);
        await host.CaptureAsync();
        Assert.AreSame(
            disclosure,
            FocusManager.GetFocusedElement(disclosure.XamlRoot));

        AutomationPeer peer = FrameworkElementAutomationPeer
            .CreatePeerForElement(disclosure);
        Assert.IsNotNull(peer);
        IExpandCollapseProvider provider =
            Assert.IsInstanceOfType<IExpandCollapseProvider>(
                peer.GetPattern(PatternInterface.ExpandCollapse));
        Assert.AreEqual(
            ExpandCollapseState.Expanded,
            provider.ExpandCollapseState);

        Assert.IsTrue(disclosure.HandleKeyboardActivation(
            VirtualKey.Space,
            repeatCount: 1,
            wasKeyDown: false));
        disclosure.PrepareTargetState(isExpanded: false);
        disclosure.CompleteTargetState(isExpanded: false);
        CollectionAssert.AreEqual(new[] { true, false }, requestedStates);
        Assert.AreSame(
            disclosure,
            FocusManager.GetFocusedElement(disclosure.XamlRoot));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task RenderedTerminal_RecoveryAndStatusAreNotColorOnly()
    {
        ModelInspectionPage page = ModelInspectionVisualTestScenario.CreatePage();
        ModelInspectionVisualTestScenario.Apply(page,
            ModelInspectionVisualTestScenario.CreatePresentation(ModelInspectionFigmaState.OperationalFailure));
        await using WinUiRenderHost host = await WinUiRenderHost.ShowAsync(page, 1000, 700);
        await host.CaptureAsync();
        ModelInspectionPreviewProjection preview = Projection(page);
        FrameworkElement outcome = preview.OutcomeSurface;
        TextBlock title = Assert.IsInstanceOfType<TextBlock>(preview.OutcomeFocusTarget);
        Border iconContainer = Element<Border>(preview.Element, "FailureOutcomeGlyphSurface");
        FontIcon glyph = ModelInspectionRenderedStateTests.Descendants(iconContainer).OfType<FontIcon>().Single();
        Assert.AreEqual(Visibility.Visible, iconContainer.Visibility);
        Assert.AreEqual(Visibility.Visible, glyph.Visibility);
        Assert.IsFalse(string.IsNullOrWhiteSpace(glyph.Glyph));
        Assert.AreEqual(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView(glyph));
        Assert.AreEqual(preview.OutcomePresentation.Title, title.Text);
        Assert.AreEqual(AutomationHeadingLevel.Level2, AutomationProperties.GetHeadingLevel(title));
        Assert.AreEqual(preview.OutcomePresentation.AutomationName, AutomationProperties.GetName(outcome));
        Button recovery = Element<Button>(preview.Element, "BtnChooseAnotherModelFailure");
        Assert.AreEqual("Choose another model", recovery.Content);
        Assert.AreEqual("Choose another model", AutomationProperties.GetName(recovery));
        Assert.IsTrue(recovery.IsEnabled);
        Assert.IsGreaterThanOrEqualTo(44d, recovery.ActualHeight);
        Assert.IsTrue(recovery.Focus(FocusState.Keyboard));
        Assert.AreSame(recovery, FocusManager.GetFocusedElement(page.XamlRoot));
        Assert.HasCount(1, ModelInspectionRenderedStateTests.Descendants(outcome).OfType<Button>()
            .Where(button => button.Visibility == Visibility.Visible && button.ActualHeight > 0).ToArray());
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ExpandedEvidence_ExposesWarningAndKeyboardDisclosureWithPageScroll()
    {
        ModelInspectionPage page = ModelInspectionVisualTestScenario.CreatePage();
        ModelInspectionVisualTestScenario.Apply(page,
            ModelInspectionVisualTestScenario.CreatePresentation(ModelInspectionFigmaState.ReadyWithWarningsExpanded));
        ModelInspectionPreviewProjection preview = Projection(page);
        preview.SetDisclosureState(true);
        await using WinUiRenderHost host = await WinUiRenderHost.ShowAsync(page, 760, 420);
        await host.CaptureAsync();
        Expander disclosure = Assert.IsInstanceOfType<Expander>(preview.ActiveDisclosure);
        Assert.IsTrue(disclosure.IsExpanded);
        AutomationPeer peer = FrameworkElementAutomationPeer.CreatePeerForElement(disclosure);
        Assert.IsNotNull(peer);
        var provider = Assert.IsInstanceOfType<IExpandCollapseProvider>(peer.GetPattern(PatternInterface.ExpandCollapse));
        Assert.AreEqual(ExpandCollapseState.Expanded, provider.ExpandCollapseState);
        Border evidence = Element<Border>(preview.Element, "WarningEvidenceBody");
        string name = AutomationProperties.GetName(evidence);
        Assert.IsFalse(string.IsNullOrWhiteSpace(name));
        Assert.IsTrue(name.Length <= 512, name);
        Assert.IsTrue(name.EndsWith(".", StringComparison.Ordinal), name);
        TextBlock[] text = ModelInspectionRenderedStateTests.Descendants(evidence).OfType<TextBlock>().ToArray();
        Assert.IsNotEmpty(text);
        Assert.IsTrue(text.All(row => !string.IsNullOrWhiteSpace(row.Text)));
        ScrollViewer scroll = Element<ScrollViewer>(page, "InspectionScrollViewer");
        Assert.AreEqual(ScrollMode.Enabled, scroll.VerticalScrollMode);
        Assert.IsGreaterThan(0d, scroll.ScrollableHeight);
        var header = ModelInspectionRenderedStateTests.Descendants(disclosure)
            .OfType<Microsoft.UI.Xaml.Controls.Primitives.ToggleButton>()
            .Single(button => button.Name == "ExpanderHeader");
        Assert.IsTrue(header.IsTabStop);
        Assert.IsTrue(header.Focus(FocusState.Keyboard));
        Assert.AreSame(header, FocusManager.GetFocusedElement(page.XamlRoot));
        provider.Collapse();
        await host.CaptureAsync();
        Assert.AreEqual(ExpandCollapseState.Collapsed, provider.ExpandCollapseState);
        Assert.AreSame(header, FocusManager.GetFocusedElement(page.XamlRoot));
        provider.Expand();
        await host.CaptureAsync();
        Assert.AreEqual(ExpandCollapseState.Expanded, provider.ExpandCollapseState);
        Assert.AreSame(header, FocusManager.GetFocusedElement(page.XamlRoot));
        Assert.IsTrue(scroll.ChangeView(null, scroll.ScrollableHeight, null, true));
        await host.CaptureAsync();
        Assert.AreEqual(scroll.ScrollableHeight, scroll.VerticalOffset, 1d);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    [TestCategory("DeferredInspectionPresentation")]
    public async Task FractionOnlyProgress_DoesNotRepeatTheSamePoliteAnnouncement()
    {
        var service = new ControlledInspectionService();
        ControlledCall call = service.QueueCall();
        var dispatcher = new ManualRenderDispatcher();
        var driver = new RecordingAnimationDriver();
        var settings = new RecordingMotionSettings(animationsEnabled: false);
        ModelInspectionPage page = CreateInjectedPage(
            service,
            dispatcher,
            driver,
            settings);
        await using WinUiRenderHost host = await WinUiRenderHost.ShowAsync(
            page,
            width: 888,
            height: 700);
        await host.CaptureAsync();
        DependencyObject initialFocus = Assert.IsInstanceOfType<DependencyObject>(
            FocusManager.GetFocusedElement(page.XamlRoot));
        Assert.IsTrue(ModelInspectionRenderedStateTests.Descendants(page).Contains(initialFocus));
        ModelInspectionPreviewProjection preview = Projection(page);
        int beforeInspectionAnnouncementCount = preview.ProgressAnnouncementCount;
        Task run = page.StartInspectionIfReadyAsync()!;

        try
        {
            dispatcher.RunAll();
            await host.CaptureAsync();
            int startupAnnouncementCount = preview.ProgressAnnouncementCount;
            Assert.AreEqual(beforeInspectionAnnouncementCount + 1, startupAnnouncementCount,
                "Starting an inspection must announce once. History: " +
                string.Join(" | ", preview.ProgressAnnouncementHistory));
            Assert.AreSame(
                initialFocus,
                FocusManager.GetFocusedElement(page.XamlRoot),
                "Startup must preserve effective focus already inside the page.");
            call.Report(ActiveProgress(stageFraction: 0.25));
            dispatcher.RunAll();
            await host.CaptureAsync();
            Assert.AreSame(
                initialFocus,
                FocusManager.GetFocusedElement(page.XamlRoot),
                "The first genuine stage must preserve existing page focus.");

            FrameworkElement content = preview.ContentSurface;
            int meaningfulAnnouncementCount =
                preview.ProgressAnnouncementCount;
            string meaningfulAnnouncement = AutomationProperties.GetName(content);
            var initialProgressRegionKey =
                page.CurrentPresentation!.RegionKeys.Progress;
            InspectionContentItemPresentation rowBefore =
                page.CurrentPresentation.ContentCard.Items[1];
            string rowAutomationName = rowBefore.AutomationName;
            int statusAnimationCount = driver.StageStatusStartCount;
            int detailAnimationCount = driver.ActiveDetailStartCount;
            Assert.AreEqual(
                AutomationLiveSetting.Polite,
                AutomationProperties.GetLiveSetting(content));
            Assert.AreEqual(
                startupAnnouncementCount + 1,
                meaningfulAnnouncementCount,
                "The first genuine stage must announce once. History: " +
                string.Join(" | ", preview.ProgressAnnouncementHistory));
            Assert.IsFalse(string.IsNullOrWhiteSpace(meaningfulAnnouncement));

            Button cancel = preview.CancelActionButton;
            Assert.IsTrue(cancel.Focus(FocusState.Keyboard));
            Assert.AreSame(cancel, FocusManager.GetFocusedElement(page.XamlRoot));

            call.Report(ActiveProgress(stageFraction: 0.75));
            dispatcher.RunAll();
            await host.CaptureAsync();

            Assert.AreSame(
                cancel,
                FocusManager.GetFocusedElement(page.XamlRoot),
                "A later fraction must not steal the user's current focus.");

            Assert.AreEqual(
                meaningfulAnnouncementCount,
                preview.ProgressAnnouncementCount,
                "A fraction-only visual update must not repeat identical speech.");
            Assert.AreEqual(
                meaningfulAnnouncement,
                AutomationProperties.GetName(content));
            Assert.AreNotEqual(
                initialProgressRegionKey,
                page.CurrentPresentation!.RegionKeys.Progress,
                "Fraction-only progress must still update the visual progress region.");
            Assert.AreSame(
                rowBefore,
                page.CurrentPresentation.ContentCard.Items[1]);
            Assert.AreEqual(
                0.75d,
                page.CurrentPresentation.ContentCard.Items[1].StageFraction);
            Assert.AreEqual(
                rowAutomationName,
                page.CurrentPresentation.ContentCard.Items[1].AutomationName,
                "A fraction-only update must not change accessible row text.");
            Assert.AreEqual(
                statusAnimationCount,
                driver.StageStatusStartCount,
                "A fraction-only update must not start marker motion.");
            Assert.AreEqual(
                detailAnimationCount,
                driver.ActiveDetailStartCount,
                "A fraction-only update must not start detail motion.");

            const string ChangedDetail =
                "Validated the current model configuration evidence.";
            call.Report(ActiveProgress(
                stageFraction: 0.3,
                userMessage: ChangedDetail));
            dispatcher.RunAll();
            await host.CaptureAsync();
            Assert.AreEqual(
                meaningfulAnnouncementCount + 1,
                preview.ProgressAnnouncementCount,
                "A meaningful detail change must announce exactly once.");
            Assert.AreEqual(ChangedDetail, AutomationProperties.GetName(content));

            call.Report(new ModelInspectionProgress(
                ModelInspectionStage.ValidateTokenizerAndChatSetup,
                ModelInspectionStageStatus.Active,
                completedStageCount: 2,
                totalStageCount: 5,
                stageFraction: 0.3,
                userMessage: ChangedDetail));
            dispatcher.RunAll();
            Assert.AreSame(
                cancel,
                FocusManager.GetFocusedElement(page.XamlRoot),
                "A later genuine stage must not steal the user's current focus.");
            Assert.AreEqual(
                meaningfulAnnouncementCount + 2,
                preview.ProgressAnnouncementCount,
                "A meaningful stage change must announce exactly once.");
        }
        finally
        {
            call.TryComplete(ModelInspectionExecutionResult.Cancelled(
                cooperative: true));
            try
            {
                await run.WaitAsync(UiTimeout);
                dispatcher.RunAll();
            }
            finally
            {
                InvokeNavigation(page, "OnNavigatedFrom", parameter: null);
            }
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task InProcessLiveRegionHook_TerminalIsOncePerAttemptAndNewAttemptRepeats()
    {
        var service = new ControlledInspectionService();
        ModelInspectionExecutionResult failure = ModelInspectionExecutionResult.OperationalFailure(
            PresentationTestData.CreateFailure());
        service.QueueCall(failure);
        var dispatcher = new ManualRenderDispatcher();
        ModelInspectionPage page = CreateInjectedPage(service, dispatcher,
            new RecordingAnimationDriver(), new RecordingMotionSettings(false));
        try
        {
            Task run = page.StartInspectionIfReadyAsync()!;
            dispatcher.RunAll();
            await run.WaitAsync(UiTimeout);
            dispatcher.RunAll();
            await using WinUiRenderHost host = await WinUiRenderHost.ShowAsync(page, 1000, 700);
            await host.CaptureAsync();
            var preview = Projection(page);
            FrameworkElement outcome = preview.OutcomeSurface;
            Assert.AreEqual(AutomationLiveSetting.Assertive, AutomationProperties.GetLiveSetting(outcome));
            Assert.AreEqual(1, preview.OutcomeAnnouncementCount);
            Assert.IsGreaterThan(0d, outcome.ActualHeight);
            var terminal = page.CurrentPresentation!;
            var coordinator = Coordinator(page);
            coordinator.RequestRender(new ModelInspectionViewSnapshot(
                new ModelInspectionRenderKey(terminal.RenderKey.AttemptGeneration,
                    terminal.RenderKey.PresentationRevision + 1),
                true, false, ActiveProgress(0.5), null));
            dispatcher.RunAll();
            await host.CaptureAsync();
            Assert.AreEqual(Visibility.Collapsed, outcome.Visibility);
            Assert.IsGreaterThan(0d, preview.ContentSurface.ActualHeight);
            Assert.IsGreaterThanOrEqualTo(44d, preview.CancelActionButton.ActualHeight);
            Assert.AreEqual(1, preview.OutcomeAnnouncementCount);
            coordinator.RequestRender(new ModelInspectionViewSnapshot(
                new ModelInspectionRenderKey(terminal.RenderKey.AttemptGeneration,
                    terminal.RenderKey.PresentationRevision + 2),
                false, false, null, failure));
            dispatcher.RunAll();
            await host.CaptureAsync();
            Assert.AreEqual(Visibility.Visible, outcome.Visibility);
            Assert.AreEqual(1, preview.OutcomeAnnouncementCount,
                "Showing the same outcome within an attempt must remain deduplicated.");
            coordinator.RequestRender(new ModelInspectionViewSnapshot(
                new ModelInspectionRenderKey(terminal.RenderKey.AttemptGeneration + 1, 1),
                true, false, ActiveProgress(0.5), null));
            dispatcher.RunAll();
            coordinator.RequestRender(new ModelInspectionViewSnapshot(
                new ModelInspectionRenderKey(terminal.RenderKey.AttemptGeneration + 1, 2),
                false, false, null, failure));
            dispatcher.RunAll();
            await host.CaptureAsync();
            Assert.AreEqual(2, preview.OutcomeAnnouncementCount,
                "A new inspection attempt may announce the same outcome once.");
        }
        finally
        {
            InvokeNavigation(page, "OnNavigatedFrom", null);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void HighContrastDictionary_DefinesEverySemanticBrushAsASystemResolvedBrush()
    {
        ResourceDictionary theme = ModelInspectionTheme();
        ResourceDictionary light = Assert.IsInstanceOfType<ResourceDictionary>(
            theme.ThemeDictionaries["Light"]);
        ResourceDictionary highContrast =
            Assert.IsInstanceOfType<ResourceDictionary>(
                theme.ThemeDictionaries["HighContrast"]);
        string[] lightKeys = light.Keys
            .Select(key => key.ToString()!)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        string[] highContrastKeys = highContrast.Keys
            .Select(key => key.ToString()!)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        Assert.IsNotEmpty(lightKeys);
        CollectionAssert.AreEqual(lightKeys, highContrastKeys);
        foreach (string key in highContrastKeys)
        {
            Assert.IsInstanceOfType<SolidColorBrush>(highContrast[key], key);
        }

        ModelInspectionPage page = ModelInspectionVisualTestScenario.CreatePage();
        ModelInspectionVisualTestScenario.Apply(page,
            ModelInspectionVisualTestScenario.CreatePresentation(ModelInspectionFigmaState.OperationalFailure));
        foreach (Button button in ModelInspectionRenderedStateTests.Descendants(page).OfType<Button>())
        {
            Assert.IsTrue(button.UseSystemFocusVisuals, button.Name);
        }
        Assert.IsTrue(Element<ScrollViewer>(page, "InspectionScrollViewer").UseSystemFocusVisuals);
        Assert.IsTrue(Element<Button>(Projection(page).Element,
            "BtnChooseAnotherModelFailure").UseSystemFocusVisuals);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task NarrowStandardScale_WrapsRequiredTextAndKeepsTargetsReachable()
    {
        ModelInspectionPage page = ModelInspectionVisualTestScenario.CreatePage();
        ModelInspectionVisualTestScenario.Apply(
            page,
            ModelInspectionVisualTestScenario.CreatePresentation(
                ModelInspectionFigmaState.InvalidExpanded));

        await using WinUiRenderHost host = await WinUiRenderHost.ShowAsync(
            page,
            width: 360,
            height: 700);
        await host.CaptureAsync();
        page.UpdateLayout();

        FrameworkElement contentHost = Element<FrameworkElement>(
            page,
            "InspectionContentStack");
        Assert.IsGreaterThan(0d, contentHost.ActualWidth);
        Assert.IsTrue(contentHost.ActualWidth <= 328d + 1d);
        var contentOrigin = contentHost.TransformToVisual(page)
            .TransformPoint(new Windows.Foundation.Point());
        Assert.AreEqual(180d, contentOrigin.X + contentHost.ActualWidth / 2d, 1d);
        Button[] visibleButtons = ModelInspectionRenderedStateTests
            .Descendants(page)
            .OfType<Button>()
            .Where(button =>
                button.Visibility == Visibility.Visible &&
                button.ActualWidth > 0d &&
                button.ActualHeight > 0d)
            .ToArray();
        Assert.IsNotEmpty(visibleButtons);
        foreach (Button button in visibleButtons)
        {
            Assert.IsGreaterThanOrEqualTo(44d, button.ActualWidth, button.Name);
            Assert.IsGreaterThanOrEqualTo(44d, button.ActualHeight, button.Name);
        }

        TextBlock[] requiredLongText = ModelInspectionRenderedStateTests
            .Descendants(page)
            .OfType<TextBlock>()
            .Where(text =>
                text.Visibility == Visibility.Visible &&
                text.ActualWidth > 0d &&
                text.ActualHeight > 0d &&
                text.Text.Length >= 40 &&
                !(text.FontFamily?.Source ?? string.Empty).StartsWith(
                    "Segoe Fluent Icons",
                    StringComparison.Ordinal))
            .ToArray();
        Assert.IsNotEmpty(requiredLongText);
        foreach (TextBlock text in requiredLongText)
        {
            Assert.AreNotEqual(TextWrapping.NoWrap, text.TextWrapping, text.Text);
            Assert.IsTrue(
                text.ActualHeight + 1d >= text.DesiredSize.Height,
                text.Text);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task InjectedReducedMotionPolicy_PreservesTerminalSemanticsAndEndpoints()
    {
        MotionEvidence animated = await RunTerminalMotionEvidenceAsync(
            animationsEnabled: true);
        MotionEvidence reduced = await RunTerminalMotionEvidenceAsync(
            animationsEnabled: false);

        Assert.IsGreaterThan(0, animated.AnimationStartCount);
        Assert.AreEqual(0, reduced.AnimationStartCount);
        Assert.AreEqual(animated.State, reduced.State);
        Assert.AreEqual(animated.OutcomeKind, reduced.OutcomeKind);
        Assert.AreEqual(animated.ContentMode, reduced.ContentMode);
        Assert.AreEqual(animated.OutgoingVisibility, reduced.OutgoingVisibility);
        Assert.AreEqual(animated.ProgressLiveSetting, reduced.ProgressLiveSetting);
        Assert.AreEqual(animated.OutcomeLiveSetting, reduced.OutcomeLiveSetting);
        Assert.AreEqual(animated.OutcomeAnnouncementCount,
            reduced.OutcomeAnnouncementCount);
        Assert.AreEqual(animated.OutcomeAutomationName,
            reduced.OutcomeAutomationName);
        Assert.AreEqual(animated.Geometry.Count, reduced.Geometry.Count);
        for (int index = 0; index < animated.Geometry.Count; index++)
        {
            Assert.AreEqual(
                animated.Geometry[index],
                reduced.Geometry[index],
                1d,
                $"Reduced motion changed terminal geometry at index {index}.");
        }
    }

    private static async Task<MotionEvidence> RunTerminalMotionEvidenceAsync(
        bool animationsEnabled)
    {
        var service = new ControlledInspectionService();
        ControlledCall call = service.QueueCall();
        var dispatcher = new ManualRenderDispatcher();
        var driver = new RecordingAnimationDriver();
        ModelInspectionPage page = CreateInjectedPage(
            service,
            dispatcher,
            driver,
            new RecordingMotionSettings(animationsEnabled));
        Task run = page.StartInspectionIfReadyAsync()!;

        try
        {
            dispatcher.RunAll();
            call.Complete(ModelInspectionExecutionResult.OperationalFailure(
                PresentationTestData.CreateFailure()));
            await run.WaitAsync(UiTimeout);
            dispatcher.RunAll();
            if (driver.TerminalCompletionCount > 0)
            {
                driver.CompleteLastTerminal();
            }

            Assert.IsNotNull(page.CurrentPresentation);
            ModelInspectionPagePresentation presentation =
                page.CurrentPresentation;
            ModelInspectionPreviewProjection preview = Projection(page);
            FrameworkElement content = preview.ContentSurface;
            FrameworkElement outcome = preview.OutcomeSurface;
            FrameworkElement actions = Element<FrameworkElement>(preview.Element, "FailureNextStepBand");
            await using WinUiRenderHost host = await WinUiRenderHost.ShowAsync(
                page,
                width: 888,
                height: 700);
            await host.CaptureAsync();
            page.UpdateLayout();
            double[] geometry =
            [
                outcome.ActualWidth,
                outcome.ActualHeight,
                content.ActualWidth,
                content.ActualHeight,
                actions.ActualWidth,
                actions.ActualHeight
            ];
            return new MotionEvidence(
                presentation.State,
                presentation.OutcomeCard.Kind,
                presentation.ContentCard.Mode,
                content.Visibility,
                AutomationProperties.GetLiveSetting(content),
                AutomationProperties.GetLiveSetting(outcome),
                preview.OutcomeAnnouncementCount,
                AutomationProperties.GetName(outcome),
                driver.TotalStartCount,
                geometry);
        }
        finally
        {
            call.TryComplete(ModelInspectionExecutionResult.Cancelled(
                cooperative: true));
            InvokeNavigation(page, "OnNavigatedFrom", parameter: null);
        }
    }

    private static ModelInspectionProgress ActiveProgress(
        double stageFraction,
        string userMessage = "Reading model configuration.") =>
        new(
            ModelInspectionStage.ReadModelConfiguration,
            ModelInspectionStageStatus.Active,
            completedStageCount: 1,
            totalStageCount: 5,
            stageFraction,
            userMessage);

    private static ModelInspectionPage CreateInjectedPage(
        IModelInspectionService service,
        ManualRenderDispatcher dispatcher,
        RecordingAnimationDriver driver,
        RecordingMotionSettings settings)
    {
        var page = new ModelInspectionPage(
            service,
            () => dispatcher,
            () => driver,
            () => settings,
            () => new ImmediateMilestoneScheduler())
        {
            RequestedTheme = ElementTheme.Light
        };
        InvokeNavigation(
            page,
            "OnNavigatedTo",
            PresentationTestData.CreateRequest());
        return page;
    }

    private static void InvokeNavigation(
        ModelInspectionPage page,
        string methodName,
        object? parameter)
    {
        NavigationEventArgs navigation = CreateNavigationEventArgs(parameter);
        MethodInfo? method = typeof(ModelInspectionPage).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);

        try
        {
            method.Invoke(page, [navigation]);
        }
        catch (TargetInvocationException error)
            when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
        }
    }

    private static NavigationEventArgs CreateNavigationEventArgs(
        object? parameter)
    {
        NavigationEventArgs? captured = null;
        var frame = new Frame();
        frame.Navigated += (_, eventArguments) => captured = eventArguments;
        Assert.IsTrue(frame.Navigate(typeof(Page), parameter));
        Assert.IsNotNull(captured);
        return captured;
    }

    private static ResourceDictionary ModelInspectionTheme() =>
        Application.Current.Resources.MergedDictionaries.Single(dictionary =>
            dictionary.Source?.OriginalString.EndsWith(
                "/Features/ModelInspection/Presentation/ModelInspectionTheme.xaml",
                StringComparison.OrdinalIgnoreCase) == true);

    private static ModelInspectionRenderCoordinator Coordinator(
        ModelInspectionPage page)
    {
        FieldInfo? field = typeof(ModelInspectionPage).GetField(
            "_coordinator",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        return Assert.IsInstanceOfType<ModelInspectionRenderCoordinator>(
            field.GetValue(page));
    }

    private static T Element<T>(FrameworkElement root, string name)
        where T : DependencyObject =>
        ModelInspectionRenderedStateTests.Element<T>(root, name);

    private static ModelInspectionPreviewProjection Projection(ModelInspectionPage page) =>
        ModelInspectionVisualTestScenario.Projection(page);

    private sealed class ControlledInspectionService : IModelInspectionService
    {
        private readonly Queue<ControlledCall> _calls = [];

        internal ControlledCall QueueCall(
            ModelInspectionExecutionResult? completed = null)
        {
            var call = new ControlledCall();
            if (completed is not null)
            {
                call.Complete(completed);
            }

            _calls.Enqueue(call);
            return call;
        }

        public Task<ModelInspectionExecutionResult> InspectAsync(
            ModelInspectionRequest request,
            IProgress<ModelInspectionProgress>? progress,
            CancellationToken cancellationToken)
        {
            if (_calls.Count == 0)
            {
                throw new InvalidOperationException(
                    "No controlled accessibility call was queued.");
            }

            ControlledCall call = _calls.Dequeue();
            call.Bind(progress);
            return call.Completion.Task;
        }
    }

    private sealed class ControlledCall
    {
        internal TaskCompletionSource<ModelInspectionExecutionResult> Completion
            { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private IProgress<ModelInspectionProgress>? Progress { get; set; }

        internal void Bind(IProgress<ModelInspectionProgress>? progress) =>
            Progress = progress;

        internal void Report(ModelInspectionProgress progress)
        {
            Assert.IsNotNull(Progress);
            Progress.Report(progress);
        }

        internal void Complete(ModelInspectionExecutionResult result) =>
            Completion.SetResult(result);

        internal void TryComplete(ModelInspectionExecutionResult result) =>
            Completion.TrySetResult(result);
    }

    private sealed class ImmediateMilestoneScheduler :
        IModelInspectionMilestoneScheduler
    {
        private bool disposed;

        public TimeSpan Elapsed => TimeSpan.Zero;

        public IDisposable Schedule(TimeSpan delay, Action callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            if (delay <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(delay));
            }

            ObjectDisposedException.ThrowIf(disposed, this);
            callback();
            return EmptyRegistration.Instance;
        }

        public void Dispose()
        {
            disposed = true;
        }

        private sealed class EmptyRegistration : IDisposable
        {
            internal static EmptyRegistration Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }

    private sealed class ManualRenderDispatcher : IModelInspectionRenderDispatcher
    {
        private readonly Queue<Action> _callbacks = [];

        public bool TryEnqueue(Action callback)
        {
            _callbacks.Enqueue(callback);
            return true;
        }

        internal void RunAll()
        {
            while (_callbacks.Count > 0)
            {
                _callbacks.Dequeue()();
            }
        }
    }

    private sealed class RecordingMotionSettings : IModelInspectionMotionSettings
    {
        internal RecordingMotionSettings(bool animationsEnabled)
        {
            AnimationsEnabled = animationsEnabled;
        }

        public bool AnimationsEnabled { get; }

        public event EventHandler? AnimationsEnabledChanged
        {
            add { }
            remove { }
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecordingAnimationDriver :
        IModelInspectionAnimationDriver
    {
        private readonly List<(
            ModelInspectionVisualOperationKey Key,
            Action<ModelInspectionVisualOperationKey> Completed)> _terminal = [];

        internal int StageStatusStartCount { get; private set; }

        internal int ActiveDetailStartCount { get; private set; }

        internal int DisclosureStartCount { get; private set; }

        internal int TerminalCompletionCount => _terminal.Count;

        internal int TotalStartCount =>
            StageStatusStartCount +
            ActiveDetailStartCount +
            DisclosureStartCount +
            _terminal.Count;

        public void StartStageStatus(
            UIElement target,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed) =>
            StageStatusStartCount++;

        public void StartActiveDetail(
            UIElement target,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed) =>
            ActiveDetailStartCount++;

        public void StartDisclosure(
            UIElement chevron,
            FrameworkElement viewport,
            IReadOnlyList<UIElement> followingElements,
            bool isExpanded,
            IReadOnlyList<double> previousTopOffsets,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed) =>
            DisclosureStartCount++;

        public void StartTerminal(
            UIElement outgoing,
            UIElement incoming,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed) =>
            _terminal.Add((key, completed));

        internal void CompleteLastTerminal()
        {
            var pending = _terminal[^1];
            pending.Completed(pending.Key);
        }

        public void CancelAll()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed record MotionEvidence(
        ModelInspectionFigmaState State,
        InspectionOutcomePresentationKind OutcomeKind,
        InspectionContentCardMode ContentMode,
        Visibility OutgoingVisibility,
        AutomationLiveSetting ProgressLiveSetting,
        AutomationLiveSetting OutcomeLiveSetting,
        int OutcomeAnnouncementCount,
        string OutcomeAutomationName,
        int AnimationStartCount,
        IReadOnlyList<double> Geometry);
}
