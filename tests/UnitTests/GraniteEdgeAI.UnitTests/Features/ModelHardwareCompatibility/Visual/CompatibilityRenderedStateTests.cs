// The fixture catalogue these render is compiled only in Debug, so the suite
// that renders it is too. a Release build has no ten screens to check because
// nine of them cannot be reached without the adapters that do not exist yet
#if DEBUG
using GraniteEdgeAI.Features.ModelHardwareCompatibility;
using GraniteEdgeAI.Features.ApplicationFaults;
using GraniteEdgeAI.Features.ApplicationComposition;
using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Domain;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.Factories;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.State;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.DebugFixtures;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Contracts;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.ViewModels;
using GraniteEdgeAI.Features.ModelImport.ModelDownload;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.OpenVino.WorkerClient;
using GraniteEdgeAI.UnitTests.Features.HardwareInspection;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual;
using GraniteEdgeAI.UnitTests.Features.ModelOptimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Windows.Foundation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.Reflection;

namespace GraniteEdgeAI.UnitTests.Features.ModelHardwareCompatibility.Visual;

/// <summary>
/// Renders every compatibility screen and checks what a person would actually
/// see.
///
/// The presentation tests already prove the wording is chosen correctly. They
/// cannot prove it arrives: a resource that resolves to nothing, a card left
/// hidden, a diagram drawn at zero — each of those passes every test that never
/// builds the tree. So these construct the real page, apply a real snapshot,
/// and read the controls back.
///
/// The claim they defend is narrow and important. The verdict a user reads must
/// never appear without the figures behind it, and the figures must never
/// appear on a screen that reached no verdict.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class CompatibilityRenderedStateTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public void NoEstimateOnlyCollapsesRetryAndRetainsImportAction()
    {
        var model = CompatibilityScreenModel.ForPresentation(
            CompatibilityScreenState.NotEstablished,
            [new CompatibilityFindingView(
                GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts.CompatibilityFindingCode.NoCandidateCouldBeEstimated,
                GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts.FindingSeverity.Blocking)],
            [], BaselineExclusionReason.None, false, false);
        CompatibilityPage page = CreatePage();
        page.Apply(CompatibilityPresentationFactory.From(model));
        Arrange(page, 1000, 900);
        Button retry = Element<Button>(page, "BtnCompatibilityBack");
        Assert.AreEqual(Visibility.Collapsed, retry.Visibility);
        Assert.IsFalse(retry.IsEnabled);
        Assert.IsNull(retry.Command);
        Assert.AreEqual(string.Empty, AutomationProperties.GetName(retry));
        Button import = Element<Button>(page, "BtnCompatibilitySecondaryForward");
        Assert.AreEqual(Visibility.Visible, import.Visibility);
        Assert.IsTrue(import.IsEnabled);
        Assert.AreEqual("Import another model", import.Content);
    }

    [UITestMethod]
    [TestCategory("ExactOptionConsentUi")]
    public async Task NativeExactChoiceRequiresBothAcknowledgementsBeforeNormalIssuance()
    {
        var fixture = CompatibilityViewModelTests.RealExactFixture();
        var page = new CompatibilityPage(
            (consent, token) => Task.FromResult(fixture.Authority.Evaluate(
                fixture.FreshResources, consent, fixture.Now, token)),
            fixture.Authority, _ => null, timeProvider: fixture.TimeProvider)
        { StartAutomatically = false };
        await using WinUiRenderHost host = await WinUiRenderHost.ShowAsync(page, 1000, 760);
        await page.ViewModel.StartAsync();
        Invoke(Element<Button>(page, "BtnCompatibilityPrimary"));
        var originalPreference = page.ViewModel.SelectedPreference;
        var disclosure = Element<Expander>(page, "CompatibilityExactOptionsExpander");
        disclosure.IsExpanded = true;
        Assert.AreEqual(originalPreference, page.ViewModel.SelectedPreference,
            "Disclosure alone must not change a setup or grant consent.");
        var selector = Element<ComboBox>(page, "CompatibilityExactOptionsSelector");
        var initial = Element<CheckBox>(page, "CompatibilityExperimentalConsent");
        var final = Element<CheckBox>(page, "CompatibilityExperimentalFinalConfirmation");
        var experimental = page.ViewModel.Presentation.Optimization!.ExactSafeModes.First(x => x.Mode.IsExperimental);
        var item = selector.Items.Cast<ComboBoxItem>().Single(x => Equals(x.Tag, experimental.CandidateIdentity));
        selector.SelectedItem = item;
        Assert.AreEqual(experimental.CandidateIdentity, page.ViewModel.SelectedPreference?.ExactCandidateIdentity);
        Assert.IsNull(page.ViewModel.CurrentOptimizationHandoff);
        Assert.IsFalse(initial.IsChecked == true);
        Assert.IsFalse(final.IsChecked == true);
        Assert.IsFalse(final.IsEnabled);
        Assert.IsFalse(Element<Button>(page, "BtnCompatibilityConfigurePrimary").IsEnabled);
        Assert.AreEqual(Visibility.Visible, Element<FrameworkElement>(page, "CompatibilitySelectedSetupSummary").Visibility,
            "A valid experimental exact preview is not a missing released-slider selection.");
        var toggle = Assert.IsInstanceOfType<IToggleProvider>(new CheckBoxAutomationPeer(initial).GetPattern(PatternInterface.Toggle));
        toggle.Toggle();
        DateTime deadline = DateTime.UtcNow.AddSeconds(2);
        while (!page.ViewModel.IsExperimentalConsentGranted && DateTime.UtcNow < deadline) await Task.Delay(10);
        Assert.IsTrue(page.ViewModel.IsExperimentalConsentGranted);
        Assert.IsTrue(final.IsEnabled);
        Assert.IsFalse(final.IsChecked == true);
        Assert.IsNull(page.ViewModel.CurrentOptimizationHandoff);
        Assert.IsFalse(Element<Button>(page, "BtnCompatibilityConfigurePrimary").IsEnabled);
        Assert.IsInstanceOfType<IToggleProvider>(new CheckBoxAutomationPeer(final).GetPattern(PatternInterface.Toggle)).Toggle();
        Assert.IsNotNull(page.ViewModel.CurrentOptimizationHandoff);
        Assert.AreEqual(experimental.CandidateIdentity,
            page.ViewModel.CurrentOptimizationHandoff.Plan.Preference.ExactCandidateIdentity);
        Assert.IsTrue(Element<Button>(page, "BtnCompatibilityConfigurePrimary").IsEnabled);
        toggle.Toggle();
        Assert.IsFalse(page.ViewModel.IsExperimentalFinalConfirmationGranted);
        Assert.IsNull(page.ViewModel.CurrentOptimizationHandoff);
        Assert.IsFalse(Element<Button>(page, "BtnCompatibilityConfigurePrimary").IsEnabled);
        toggle.Toggle();
        deadline = DateTime.UtcNow.AddSeconds(2);
        while (!page.ViewModel.IsExperimentalConsentGranted && DateTime.UtcNow < deadline) await Task.Delay(10);
        Assert.IsTrue(page.ViewModel.IsExperimentalConsentGranted);
        Assert.IsInstanceOfType<IToggleProvider>(new CheckBoxAutomationPeer(final).GetPattern(PatternInterface.Toggle)).Toggle();
        Assert.AreEqual(Visibility.Collapsed, Element<FrameworkElement>(page, "CompatibilitySafeSliderPanel").Visibility);
        foreach (ElementTheme theme in new[] { ElementTheme.Light, ElementTheme.Dark })
        {
            page.RequestedTheme = theme;
            page.UpdateLayout();
            RenderedFrame frame = await host.CaptureAsync();
            int stableFrames = 0;
            var captureDeadline = System.Diagnostics.Stopwatch.StartNew();
            while (stableFrames < 3 && captureDeadline.Elapsed < TimeSpan.FromSeconds(3))
            {
                RenderedFrame next = await host.CaptureAsync();
                stableFrames = frame.Bgra8Pixels.Span.SequenceEqual(next.Bgra8Pixels.Span)
                    ? stableFrames + 1 : 0;
                frame = next;
            }
            Assert.AreEqual(3, stableFrames, "Capture requires settled rendered expansion/theme frames.");
            Assert.IsTrue(disclosure.IsExpanded && initial.IsLoaded && final.IsLoaded);
            foreach (FrameworkElement control in new FrameworkElement[] { selector, initial, final })
            {
                Assert.IsTrue(control.ActualHeight >= 44 && control.ActualWidth > 0);
                Point position = control.TransformToVisual(page).TransformPoint(new Point());
                Assert.IsTrue(position.X >= -1 && position.X + control.ActualWidth <= page.ActualWidth + 1);
            }
            TestContext.AddResultFile(await frame.SavePngAsync($"exact-option-acknowledged-{theme}.png"));
        }
        int requests = 0;
        string? issuedIdentity = null;
        page.ViewModel.OptimizationRequested += (_, args) =>
        {
            issuedIdentity = args.Context.OptimizationHandoff.Plan.Preference.ExactCandidateIdentity;
            requests++;
        };
        Assert.IsTrue(page.ViewModel.StartOptimizationCommand.CanExecute(null),
            $"Before Invoke: consent={page.ViewModel.IsExperimentalConsentGranted}, final={page.ViewModel.IsExperimentalFinalConfirmationGranted}, status={page.ViewModel.Presentation.Optimization?.SelectionStatusText}");
        Assert.IsTrue(Element<Button>(page, "BtnCompatibilityConfigurePrimary").IsEnabled);
        Invoke(Element<Button>(page, "BtnCompatibilityConfigurePrimary"));
        deadline = DateTime.UtcNow.AddSeconds(2);
        while (requests == 0 && DateTime.UtcNow < deadline) await Task.Delay(10);
        Assert.AreEqual(1, requests, $"The native Start action must use the refreshed normal exact issuer. Loaded={page.IsLoaded}, busy={page.ViewModel.IsOptimizationStartBusy}, consent={page.ViewModel.IsExperimentalConsentGranted}, final={page.ViewModel.IsExperimentalFinalConfirmationGranted}, status={page.ViewModel.Presentation.Optimization?.SelectionStatusText}");
        Assert.AreEqual(experimental.CandidateIdentity, issuedIdentity);
        var released = page.ViewModel.Presentation.Optimization!.SafeSliderModes[0];
        selector.Focus(FocusState.Keyboard);
        selector.SelectedItem = selector.Items.Cast<ComboBoxItem>().Single(x => Equals(x.Tag, released.CandidateIdentity));
        Assert.AreEqual(released.CandidateIdentity, page.ViewModel.SelectedPreference!.ExactCandidateIdentity);
        Assert.AreEqual(0, page.ViewModel.Presentation.Optimization!.SafeSliderSelectedIndex);
        Assert.AreEqual(Visibility.Visible, Element<FrameworkElement>(page, "CompatibilitySafeSliderPanel").Visibility);
        Assert.AreEqual(Visibility.Collapsed, Element<FrameworkElement>(page, "CompatibilityExperimentalAcknowledgements").Visibility);
        Assert.IsTrue(Element<Button>(page, "BtnCompatibilityConfigurePrimary").IsEnabled);
        Assert.AreNotEqual(FocusState.Unfocused, selector.FocusState,
            "Restoring released choices must not discard the selector's keyboard focus.");
    }

    [UITestMethod]
    [TestCategory("RouteNeutralMemoryOverview")]
    public async Task OpenVinoCurrentFitWithoutQualifiedAlternativeRendersActualMemoryOnly()
    {
        const ulong gib = 1_073_741_824;
        var budget = SystemMemoryBudgetCalculator.Calculate(CurrentlyAvailableMemory.FromBytes(9 * gib / 2));
        var setup = CompatibilitySetupView.ForPresentation(RuntimeRouteId.OpenVinoGenAi,
            CompatibilityBackend.OpenVinoCpu, DeviceRouteId.Cpu, WeightQuantisation.F16, 4096,
            CompatibilityFitState.Safe, requiredBytes: 3 * gib, safeBudgetBytes: 4 * gib,
            headroomBytes: gib, uncertaintyAllowanceBytes: 268_435_456, isExperimental: false,
            requiresConversion: false, [],
            openVinoKvCache: GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino.OpenVinoKvCacheFormat.U8);
        var screen = CompatibilityScreenModel.ForPresentation(CompatibilityScreenState.EstimatedCompatible,
            [], [], BaselineExclusionReason.None, useCurrentModelAvailable: true, continueEnabled: true, setup: setup);
        var presentation = CompatibilityPresentationFactory.From(new CompatibilityEvaluation(screen, null, null)
        {
            MachineMemory = CompatibilityMachineMemory.Create(TotalPhysicalMemory.FromBytes(16 * gib),
                budget.Available, budget.Reserve, budget.Executable)
        });
        foreach (ElementTheme theme in new[] { ElementTheme.Light, ElementTheme.Dark })
        {
        var page = CreatePage();
        page.RequestedTheme = theme;
        page.Apply(CompatibilityPresentationFactory.From(screen));
        bool actionEnabled = Element<Button>(page, "BtnCompatibilityPrimary").IsEnabled;
        page.Apply(presentation);
        Assert.AreEqual("FREE RAM NOW", Element<TextBlock>(page, "CompatibilityMemoryNeededLabel").Text);
        Assert.AreEqual("CURRENT SETUP PEAK RAM", Element<TextBlock>(page, "CompatibilitySpareLabel").Text);
        Assert.AreEqual(Visibility.Visible, Element<Line>(page, "CompatibilityAvailableRamMarker").Visibility);
        Assert.AreEqual(Visibility.Collapsed, Element<Line>(page, "CompatibilityMinimumRamMarker").Visibility);
        Assert.IsFalse(Element<TextBlock>(page, "CompatibilityMinimumRamLabel").Text.Contains("smallest", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(actionEnabled, Element<Button>(page, "BtnCompatibilityPrimary").IsEnabled);
        await using WinUiRenderHost host = await WinUiRenderHost.ShowAsync(page, 1200, 700);
        RenderedFrame frame = await host.CaptureAsync();
        TestContext.AddResultFile(await frame.SavePngAsync($"openvino-current-memory-no-alternative-{theme}.png"));
        foreach (string name in new[] { "CompatibilityMemoryNeededLabel", "CompatibilitySpareLabel" })
        {
            TextBlock label = Element<TextBlock>(page, name);
            Point position = label.TransformToVisual(page).TransformPoint(new Point());
            Assert.IsTrue(label.ActualWidth > 0 && label.ActualHeight > 0, name);
            Assert.IsTrue(position.X >= -1 && position.X + label.ActualWidth <= page.ActualWidth + 1, name);
        }
        }
    }
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void CompatibilityPage_LocalStaticResourceReferencesResolve()
    {
        string pagePath = System.IO.Path.Combine(
            RepositoryTestPaths.FindRepositoryRoot(),
            "IBM Granite with TurboQuant (Intel)",
            "Features",
            "ModelHardwareCompatibility",
            "CompatibilityPage.xaml");
        string xaml = File.ReadAllText(pagePath);
        HashSet<string> localKeys = System.Text.RegularExpressions.Regex
            .Matches(xaml, "x:Key=\"(?<key>Compatibility[^\"]+)\"")
            .Select(match => match.Groups["key"].Value)
            .ToHashSet(StringComparer.Ordinal);
        string[] localReferences = System.Text.RegularExpressions.Regex
            .Matches(
                xaml,
                "\\{StaticResource\\s+(?<key>Compatibility[^},\\s]+)")
            .Select(match => match.Groups["key"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        foreach (string resource in localReferences)
        {
            Assert.IsTrue(
                localKeys.Contains(resource),
                $"CompatibilityPage references missing local StaticResource '{resource}'.");
        }
    }

    [TestMethod]
    public void CompatibilityPage_UsesPlainLanguageRamEndpointCopy()
    {
        string pagePath = System.IO.Path.Combine(
            RepositoryTestPaths.FindRepositoryRoot(),
            "IBM Granite with TurboQuant (Intel)",
            "Features",
            "ModelHardwareCompatibility",
            "CompatibilityPage.xaml");
        string xaml = File.ReadAllText(pagePath);

        StringAssert.Contains(xaml, "Text=\"Uses less memory\"");
        StringAssert.Contains(xaml, "Text=\"Uses more memory\"");
        Assert.IsFalse(xaml.Contains(
            "Text=\"LOWEST ESTIMATED RAM\"", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains(
            "Text=\"HIGHER ESTIMATED RAM\"", StringComparison.Ordinal));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Page_UsesApprovedLightTheme()
    {
        CompatibilityPage page = new() { StartAutomatically = false };

        Assert.AreEqual(ElementTheme.Default, page.RequestedTheme,
            "The page must follow the active system Light, Dark or High Contrast theme.");
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Page_CentresAReadableDesktopContentColumn()
    {
        CompatibilityPage page = new() { StartAutomatically = false };
        ScrollViewer contentHost = Element<ScrollViewer>(
            page, "CompatibilityContentScroll");
        FrameworkElement bay = Element<FrameworkElement>(page, "CompatibilityBay");
        TextBlock title = Element<TextBlock>(page, "CompatibilityConfigureHeading");

        Assert.AreEqual(HorizontalAlignment.Stretch, contentHost.HorizontalContentAlignment);
        Assert.AreEqual(HorizontalAlignment.Stretch, bay.HorizontalAlignment);
        Assert.AreEqual(960d, bay.MaxWidth);
        Assert.AreEqual(HorizontalAlignment.Center, title.HorizontalAlignment);
        Assert.AreEqual(TextAlignment.Center, title.TextAlignment);
        Assert.IsGreaterThanOrEqualTo(20d, title.FontSize);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Page_HasResponsiveResultActionsForNarrowWindows()
    {
        CompatibilityPage page = new() { StartAutomatically = false };
        InvokeResponsiveLayout(page, 560d);

        Assert.AreEqual(
            Orientation.Vertical,
            Element<StackPanel>(page, "CompatibilityTerminalActions").Orientation);
        Assert.AreEqual(
            Orientation.Vertical,
            Element<StackPanel>(page, "CompatibilityForwardActions").Orientation);
        Assert.AreEqual(
            HorizontalAlignment.Stretch,
            Element<Button>(page, "BtnCompatibilityPrimary").HorizontalAlignment);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void CompactViewport_ConstrainsContentWidthInsteadOfKeepingItsWideDesiredSize()
    {
        CompatibilityPage page = CreatePage();
        CompatibilityFixture? fixture = CompatibilityFixtureCatalogue.ById("CMP-022");
        Assert.IsNotNull(fixture);
        page.Apply(fixture.Presentation);

        Arrange(page, 600, 900);
        InvokeResponsiveLayout(page, 600d);
        page.UpdateLayout();

        FrameworkElement bay = Element<FrameworkElement>(page, "CompatibilityBay");
        ScrollViewer scroll = Element<ScrollViewer>(
            page, "CompatibilityContentScroll");
        Assert.IsGreaterThan(0d, bay.ActualWidth);
        Assert.IsLessThanOrEqualTo(scroll.ActualWidth, bay.ActualWidth,
            "compact content must stay within the realized viewport");
        Assert.IsLessThanOrEqualTo(600d, scroll.ActualWidth);
        Assert.AreEqual(ScrollBarVisibility.Disabled,
            scroll.HorizontalScrollBarVisibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void CompactConcludedScreens_StackActionsAndPreserveEssentialEvidenceText()
    {
        CompatibilityPage page = CreatePage();

        foreach (string fixtureId in new[] { "CMP-010", "CMP-011", "CMP-012", "CMP-030" })
        {
            CompatibilityFixture? fixture = CompatibilityFixtureCatalogue.ById(fixtureId);
            Assert.IsNotNull(fixture);
            page.Apply(fixture.Presentation);
            Arrange(page, 560, 900);
            InvokeResponsiveLayout(page, 560);

            Assert.AreEqual(
                Orientation.Vertical,
                Element<StackPanel>(page, "CompatibilityTerminalActions").Orientation,
                $"{fixtureId} left result actions on one squeezed row.");
            Assert.AreEqual(
                Orientation.Vertical,
                Element<StackPanel>(page, "CompatibilityForwardActions").Orientation,
                $"{fixtureId} left forward actions on one squeezed row.");

            IEnumerable<TextBlock> essentialText = new[]
            {
                Element<TextBlock>(page, "CompatibilityOutcomeHeading"),
                Element<TextBlock>(page, "CompatibilityOutcomeDetail"),
                Element<TextBlock>(page, "CompatibilityMemoryNeededLabel"),
                Element<TextBlock>(page, "CompatibilityMemoryNeeded"),
                Element<TextBlock>(page, "CompatibilitySafeMemoryLabel"),
                Element<TextBlock>(page, "CompatibilitySafeMemory"),
                Element<TextBlock>(page, "CompatibilitySpareLabel"),
                Element<TextBlock>(page, "CompatibilitySpareValue"),
                Element<TextBlock>(page, "CompatibilityContextLabel"),
                Element<TextBlock>(page, "CompatibilityContextValue")
            };

            foreach (TextBlock text in essentialText.Where(text =>
                         !string.IsNullOrWhiteSpace(text.Text)))
            {
                Assert.AreEqual(TextTrimming.None, text.TextTrimming,
                    $"{fixtureId} shortened essential evidence: {text.Text}");
                Assert.AreNotEqual(TextWrapping.NoWrap, text.TextWrapping,
                    $"{fixtureId} did not wrap essential evidence: {text.Text}");
            }
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Page_DoesNotDuplicateTheOnboardingShellStageIndicator()
    {
        CompatibilityPage page = new() { StartAutomatically = false };

        Assert.IsNull(page.FindName("StepperSteps"));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void NoFitScreen_ReportsRequiredMemoryAboveSafeLimit()
    {
        CompatibilityPage page = CreatePage();
        CompatibilityFixture? fixture = CompatibilityFixtureCatalogue.ById("CMP-030");
        Assert.IsNotNull(fixture);
        Assert.IsGreaterThan(
            fixture.Presentation.Budget.SafeLimitBytes,
            fixture.Presentation.Budget.RequiredBytes);
        page.Apply(fixture.Presentation);

        ulong expectedOverage = fixture.Presentation.Budget.RequiredBytes
            - fixture.Presentation.Budget.SafeLimitBytes;
        Assert.AreEqual(
            "OVER SAFE LIMIT",
            Element<TextBlock>(page, "CompatibilitySpareLabel").Text);
        Assert.AreEqual(
            CompatibilityBudget.Describe(expectedOverage),
            Element<TextBlock>(page, "CompatibilitySpareValue").Text,
            "A no-fit result must display the overage, not a zero spare value.");
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void NoFitMemoryDiagram_UsesTwoDirectlyLabelledSolidThresholdTails()
    {
        CompatibilityPage page = CreatePage();
        CompatibilityFixture? fixture = CompatibilityFixtureCatalogue.ById("CMP-030");
        Assert.IsNotNull(fixture);

        CompatibilityPresentation withThresholds = fixture.Presentation with
        {
            MemoryClarity = new CompatibilityMemoryClarityPresentation(
                AvailableSystemMemoryBytes: 5_368_709_120,
                SafetyReserveBytes: 536_870_912,
                SafeModelBudgetBytes: 4_831_838_208,
                CurrentRequiredBytes: 7_516_192_768,
                SmallestOptimizedRequiredBytes: 3_758_096_384,
                MinimumFreeToOptimizeBytes: 4_294_967_296,
                AdditionalFreeRequiredBytes: 0,
                ContextTokens: 4096)
        };
        page.Apply(withThresholds);

        TextBlock availableLabel = Element<TextBlock>(
            page, "CompatibilityAvailableRamLabel");
        TextBlock minimumLabel = Element<TextBlock>(
            page, "CompatibilityMinimumRamLabel");
        Line availableMarker = Element<Line>(
            page, "CompatibilityAvailableRamMarker");
        Line minimumMarker = Element<Line>(
            page, "CompatibilityMinimumRamMarker");
        TextBlock duplicateSummary = Element<TextBlock>(
            page, "CompatibilityBudgetSummary");

        Assert.IsTrue(
            availableLabel.Text.StartsWith(
                "RAM available to the model now",
                StringComparison.Ordinal));
        Assert.IsTrue(
            minimumLabel.Text.StartsWith(
                "Smallest evaluated setup needs",
                StringComparison.Ordinal));
        Assert.AreEqual(Visibility.Visible, availableMarker.Visibility);
        Assert.AreEqual(Visibility.Visible, minimumMarker.Visibility);
        Assert.AreEqual(0, availableMarker.StrokeDashArray?.Count ?? 0);
        Assert.AreEqual(0, minimumMarker.StrokeDashArray?.Count ?? 0);
        Assert.AreEqual(8d, availableMarker.Height);
        Assert.AreEqual(8d, minimumMarker.Height);
        Assert.AreEqual(Visibility.Collapsed, duplicateSummary.Visibility);
        Assert.IsFalse(availableLabel.Text.Contains("Dashed line:"));
        Assert.IsFalse(minimumLabel.Text.Contains("Dashed line:"));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void HardwareReport_IsOptionalAndProjectsExactTypedEvidence()
    {
        CompatibilityPage page = CreatePage();
        Expander disclosure = Element<Expander>(
            page, "CompatibilityHardwareFactsExpander");
        StackPanel host = Element<StackPanel>(
            page, "CompatibilityHardwareFactsHost");

        Assert.AreEqual(Visibility.Collapsed, disclosure.Visibility);
        Assert.AreEqual(0, host.Children.Count);

        HardwareInspectionPresentationState presentation =
            new HardwareInspectionPresentationFactory().CreateTerminal(
                HardwareInspectionOutcome.CompletedWithWarnings);
        var summary = new HardwareSummaryPresentation(
            [new HardwareFactPresentation(
                "Memory",
                "Available now",
                "5.0 GiB",
                "Captured for this inspection run")]);
        var details = new HardwareInspectionDetailsState(
            "Hardware details",
            "Seven checks were reviewed.",
            "Report assembled from the completed inspection.",
            "Report created",
            Enumerable.Range(1, 7).Select(index =>
                new HardwareInspectionDetailRow(
                    $"Check {index}",
                    $"Evidence {index}",
                    index == 1 ? "Review" : "Completed")),
            [new HardwareInspectionTechnicalGroup(
                "Support information",
                "Safe support metadata",
                [new HardwareInspectionTechnicalItem("Code", "hardware-review")])]);

        page.SetHardwareReport(presentation, summary, details);

        Assert.AreEqual(Visibility.Visible, disclosure.Visibility);
        Assert.IsFalse(disclosure.IsExpanded);
        Assert.AreEqual(5, host.Children.Count);
        string[] renderedText = Descendants(host)
            .OfType<TextBlock>()
            .Select(text => text.Text)
            .ToArray();
        CollectionAssert.Contains(renderedText, presentation.Title);
        CollectionAssert.Contains(renderedText, presentation.Body);
        CollectionAssert.Contains(renderedText, "NEEDS REVIEW");
        CollectionAssert.Contains(renderedText, "INFORMATION");
        Assert.AreEqual(2, renderedText.Count(text => text == "1"));
        StackPanel section = Assert.IsInstanceOfType<StackPanel>(host.Children[2]);
        Grid row = Assert.IsInstanceOfType<Grid>(section.Children[1]);
        Assert.AreEqual(
            "Available now: 5.0 GiB. Captured for this inspection run",
            AutomationProperties.GetName(row));
        Assert.AreEqual(
            "Captured for this inspection run",
            Assert.IsInstanceOfType<TextBlock>(row.Children[2]).Text);
        StackPanel checks = Assert.IsInstanceOfType<StackPanel>(host.Children[3]);
        Assert.AreEqual(8, checks.Children.Count);
        Assert.AreEqual(
            "Check 1. Evidence 1 Status: Review.",
            AutomationProperties.GetName(
                Assert.IsInstanceOfType<Grid>(checks.Children[1])));
        Assert.IsTrue(
            AutomationProperties.GetName(disclosure).Contains(
                "7 inspection checks",
                StringComparison.Ordinal));
        CollectionAssert.Contains(renderedText, "Safe support metadata");
        CollectionAssert.Contains(renderedText, "hardware-review");

        page.SetHardwareReport(null, null, null);

        Assert.AreEqual(Visibility.Collapsed, disclosure.Visibility);
        Assert.AreEqual(0, host.Children.Count);
        Assert.AreEqual("Hardware facts", AutomationProperties.GetName(disclosure));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ViewportCenteringHost_KeepsBayCenteredAndStableAcrossReportDisclosureForBothRoutes()
    {
        foreach (string fixtureId in new[] { "CMP-020", "CMP-023" })
        {
            foreach (double viewportWidth in new[] { 1200d, 700d })
            {
                CompatibilityPage page = CreatePage();
                CompatibilityFixture? fixture =
                    CompatibilityFixtureCatalogue.ById(fixtureId);
                Assert.IsNotNull(fixture);
                page.Apply(fixture.Presentation);
                page.SetHardwareReport(
                    new HardwareInspectionPresentationFactory().CreateTerminal(
                        HardwareInspectionOutcome.CompletedWithWarnings),
                    new HardwareSummaryPresentation(
                    [
                        new HardwareFactPresentation(
                            "Memory",
                            "Available now",
                            "5.0 GiB",
                            "Captured for this inspection run")
                    ]),
                    new HardwareInspectionDetailsState(
                        "Hardware details",
                        "Seven checks were reviewed.",
                        "Report assembled from the completed inspection.",
                        "Report created",
                        Enumerable.Range(1, 7).Select(index =>
                            new HardwareInspectionDetailRow(
                                $"Check {index}",
                                $"Evidence {index}",
                                index == 1 ? "Review" : "Completed")),
                        []));

                await using WinUiRenderHost renderHost =
                    await WinUiRenderHost.ShowAsync(
                        page,
                        checked((int)viewportWidth),
                        700);
                _ = await renderHost.CaptureAsync();
                Grid centeringHost = Element<Grid>(
                    page, "CompatibilityViewportCenteringHost");
                Border bay = Element<Border>(page, "CompatibilityBay");
                Expander report = Element<Expander>(
                    page, "CompatibilityHardwareFactsExpander");
                double collapsedWidth = bay.ActualWidth;
                Assert.IsGreaterThan(0d, centeringHost.ActualWidth,
                    $"{fixtureId} did not realize the viewport-centering host.");
                Assert.IsGreaterThan(0d, collapsedWidth,
                    $"{fixtureId} did not realize the compatibility bay.");
                Assert.AreEqual(viewportWidth, centeringHost.ActualWidth, 0.5,
                    $"{fixtureId} did not give the centering host the full viewport width.");
                Assert.AreEqual(
                    Math.Min(960d, centeringHost.ActualWidth),
                    collapsedWidth,
                    0.5,
                    $"{fixtureId} did not use the viewport-derived capped card width.");
                AssertBayCentered(centeringHost, bay, fixtureId, viewportWidth);

                report.IsExpanded = true;
                _ = await renderHost.CaptureAsync();

                Assert.AreEqual(collapsedWidth, bay.ActualWidth, 0.5,
                    $"{fixtureId} changed card width when its hardware report expanded.");
                AssertBayCentered(centeringHost, bay, fixtureId, viewportWidth);
                Assert.IsLessThanOrEqualTo(960d, bay.ActualWidth);
            }
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void RecoveryActionRow_LeavesRoomForTheFullRefreshLabel()
    {
        CompatibilityPage page = CreatePage();
        Button primary = Element<Button>(page, "BtnCompatibilityPrimary");
        StackPanel actions = Element<StackPanel>(page, "CompatibilityTerminalActions");
        StackPanel forward = Element<StackPanel>(page, "CompatibilityForwardActions");

        Assert.IsGreaterThanOrEqualTo(272d, primary.MinWidth);
        Assert.IsLessThanOrEqualTo(8d, actions.Spacing);
        Assert.IsLessThanOrEqualTo(8d, forward.Spacing);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task MissingDestination_LeavesViewModelCommandAndPageDisabled()
    {
        CompatibilityScreenModel result = CompatibilityScreenModel.ForPresentation(
            CompatibilityScreenState.EstimatedCompatible,
            [],
            [],
            BaselineExclusionReason.None,
            useCurrentModelAvailable: false,
            continueEnabled: true);
        var blockedPage = new CompatibilityPage(
            _ => Task.FromResult(result),
            continueDestinationAvailable: false)
        {
            StartAutomatically = false
        };
        await blockedPage.ViewModel.StartAsync();

        Assert.IsFalse(blockedPage.ViewModel.Presentation.PrimaryActionEnabled);
        Assert.IsFalse(Element<Button>(blockedPage, "BtnCompatibilityPrimary").IsEnabled);
        Assert.IsFalse(blockedPage.ViewModel.ContinueCommand.CanExecute(null));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void OptimisationRequired_KeepsStableResultTreeUntilConfigureIsChosen()
    {
        CompatibilityPage page = CreatePage();
        page.Apply(SourceBackedSafeSliderPresentation());
        Arrange(page, 1000, 900);

        FrameworkElement card = Element<FrameworkElement>(
            page, "CompatibilityOptimizationPanel");
        Slider slider = Element<Slider>(page, "CompatibilityPreferenceSlider");
        Button primary = Element<Button>(page, "BtnCompatibilityPrimary");
        Button secondary = Element<Button>(page, "BtnCompatibilityBack");

        Assert.AreEqual(Visibility.Collapsed, card.Visibility);
        Assert.AreEqual(Visibility.Visible,
            Element<FrameworkElement>(page, "CompatibilityOutcomePanel").Visibility);
        Assert.AreEqual("Choose optimisation", primary.Content);
        InvokePrimaryClickHandler(page, primary);
        Arrange(page, 1000, 900);
        Assert.AreEqual(Visibility.Visible, card.Visibility);
        Assert.AreEqual("CompatibilityOptimizationPanel",
            AutomationProperties.GetAutomationId(card));
        Assert.IsGreaterThanOrEqualTo(44d, slider.ActualHeight);
        Assert.IsGreaterThanOrEqualTo(44d, primary.MinHeight);
        Assert.IsGreaterThanOrEqualTo(44d, secondary.MinHeight);
        Assert.AreEqual(ScrollBarVisibility.Auto,
            Descendants(page).OfType<ScrollViewer>().Single()
                .VerticalScrollBarVisibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task AuthoritativeSafeChoices_StayOnResultUntilConfigureIsChosen()
    {
        CompatibilityPage page = CreatePage();
        int handoffCount = 0;
        int configureEntries = 0;
        int backRequests = 0;
        page.OptimizationRequested += (_, _) => handoffCount++;
        page.ConfigureStageEntered += (_, _) => configureEntries++;
        page.BackRequested += (_, _) => backRequests++;

        CompatibilityPresentation authoritative =
            SourceBackedSafeSliderPresentation();
        page.ViewModel.ShowFixture(authoritative);

        await using WinUiRenderHost host =
            await WinUiRenderHost.ShowAsync(page, 700, 620);
        _ = await host.CaptureAsync();

        FrameworkElement result = Element<FrameworkElement>(
            page, "CompatibilityOutcomePanel");
        FrameworkElement selector = Element<FrameworkElement>(
            page, "CompatibilityConfigurePanel");
        FrameworkElement optimization = Element<FrameworkElement>(
            page, "CompatibilityOptimizationPanel");
        Button entry = Element<Button>(page, "BtnCompatibilityPrimary");

        Assert.AreEqual(0, configureEntries);
        Assert.AreEqual(0, handoffCount);
        Assert.AreEqual(Visibility.Visible, result.Visibility,
            "Stage 3 must show the complete compatibility result first.");
        Assert.AreEqual(Visibility.Collapsed, optimization.Visibility);
        Assert.AreEqual(Visibility.Collapsed, selector.Visibility);
        Assert.AreEqual(Visibility.Visible,
            Element<FrameworkElement>(page, "CompatibilityOutcomeBand").Visibility);
        Assert.AreEqual(Visibility.Visible,
            Element<FrameworkElement>(page, "CompatibilityTerminalActionBand").Visibility);
        Assert.AreEqual("Choose optimisation", entry.Content);
        Assert.IsNull(entry.Command,
            "Choosing configuration is a local stage transition, not an optimization request.");
        Assert.IsTrue(entry.IsEnabled,
            "An authoritative safe choice must allow entry before experimental consent.");

        InvokePrimaryClickHandler(page, entry);

        Assert.AreEqual(1, configureEntries);
        Assert.AreEqual(0, handoffCount);
        Assert.AreEqual(Visibility.Collapsed, result.Visibility,
            "Configure must not retain the result evidence tree.");
        Assert.AreEqual(Visibility.Visible, optimization.Visibility);
        Assert.AreEqual(Visibility.Visible, selector.Visibility);
        page.Apply(authoritative);
        _ = await host.CaptureAsync();
        Assert.AreEqual(1, configureEntries,
            "Reapplying the accepted current snapshot must not re-enter stage 4.");
        Slider slider = Element<Slider>(page, "CompatibilityPreferenceSlider");
        Assert.IsGreaterThan(0d, slider.ActualWidth);
        Assert.IsGreaterThanOrEqualTo(
            optimization.ActualWidth - 40d,
            slider.ActualWidth,
            "The configure-stage preference slider must span the selector content.");
        CompatibilityOptimizationPresentation exactOptimization =
            authoritative.Optimization!;
        CompatibilityOptimizationModePresentation exactSelection =
            exactOptimization.SafeSliderModes[
                exactOptimization.SafeSliderSelectedIndex!.Value].Mode;
        StringAssert.Contains(AutomationProperties.GetName(slider),
            exactSelection.Label);
        StringAssert.Contains(AutomationProperties.GetHelpText(slider),
            exactSelection.ExpectedQualityText);
        Assert.IsTrue(slider.IsTabStop);
        Assert.IsTrue(slider.IsEnabled,
            "An authoritative admitted selector must remain enabled.");

        Button final = Element<Button>(page, "BtnCompatibilityConfigurePrimary");
        Assert.AreSame(page.ViewModel.StartOptimizationCommand, final.Command);
        Assert.AreEqual("Start optimisation", final.Content);
        Assert.AreEqual(
            "CompatibilityConfigureAction.StartOptimization",
            AutomationProperties.GetAutomationId(final));
        Assert.AreEqual("Start optimisation", AutomationProperties.GetName(final));
        Assert.AreEqual(
            page.ViewModel.StartOptimizationCommand.CanExecute(null),
            final.IsEnabled,
            "Configure Start must project the revalidating exact-selection intent.");
        Button back = Element<Button>(page, "BtnCompatibilityConfigureBack");
        Invoke(back);
        Assert.AreEqual(0, backRequests,
            "Configure Back is a local return to the retained compatibility result.");
        Assert.AreEqual(0, handoffCount);
        Assert.IsNull(back.Command);
        Assert.AreEqual("Back to compatibility result",
            AutomationProperties.GetName(back));
        Assert.AreEqual(Visibility.Visible, result.Visibility);
        Assert.AreEqual(Visibility.Collapsed, selector.Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task OptionalCurrentFit_SeparatesChatFromLocalConfigureEntry()
    {
        CompatibilityViewModelTests.RealExactPlanningFixture fixture =
            CompatibilityViewModelTests.RealExactFixture();
        CompatibilitySetupView current = fixture.Evaluation.Screen.CurrentSetup
            ?? throw new AssertFailedException(
                "The released fixture needs a current setup.");
        CompatibilityEvaluation optionalEvaluation = new(
            AuthoritativeEstimatedCompatibleScreen(current),
            fixture.Evaluation.PlanningSession,
            CurrentConfiguration: null)
        {
            OptionalOptimization = fixture.Evaluation.Screen.Optimization,
            MachineMemory = fixture.Evaluation.MachineMemory
        };
        OptimizationJourneyBinding binding =
            Assert.IsInstanceOfType<OptimizationJourneyBinding>(
                typeof(OpenVinoOptimizationProductionAuthority).GetField(
                    "_binding",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(fixture.Authority));
        int evaluations = 0;
        var page = new CompatibilityPage(
            (_, _) =>
            {
                evaluations++;
                return Task.FromResult(optionalEvaluation);
            },
            new ChatEnabledAuthority(fixture.Authority),
            _ => CompatibilityViewModelTests.CurrentHandoff(binding),
            timeProvider: fixture.TimeProvider)
        {
            StartAutomatically = false
        };
        int chatRequests = 0;
        int optimizationRequests = 0;
        int configureEntries = 0;
        page.CurrentModelChatRequested += (_, _) => chatRequests++;
        page.OptimizationRequested += (_, _) => optimizationRequests++;
        page.ConfigureStageEntered += (_, _) => configureEntries++;

        await using WinUiRenderHost host =
            await WinUiRenderHost.ShowAsync(page, 900, 760);
        await page.ViewModel.StartAsync();

        Button chat = Element<Button>(page, "BtnCompatibilityPrimary");
        Button choose = Element<Button>(page, "BtnCompatibilitySecondaryForward");
        Assert.AreEqual(Visibility.Visible, chat.Visibility);
        Assert.AreEqual("Chat with current model", chat.Content);
        Assert.AreSame(page.ViewModel.ChatCurrentModelCommand, chat.Command);
        Assert.AreEqual(Visibility.Visible, choose.Visibility);
        Assert.AreEqual("Choose optimisation", choose.Content);
        Assert.IsNull(choose.Command);

        Invoke(chat);

        Assert.AreEqual(1, chatRequests);
        Assert.AreEqual(0, optimizationRequests);
        Assert.AreEqual(0, configureEntries);
        Assert.AreEqual(1, evaluations);
        Assert.AreEqual(Visibility.Visible,
            Element<FrameworkElement>(page, "CompatibilityOutcomePanel").Visibility);

        Invoke(choose);

        Assert.AreEqual(1, chatRequests);
        Assert.AreEqual(0, optimizationRequests);
        Assert.AreEqual(1, configureEntries);
        Assert.AreEqual(1, evaluations);
        Assert.AreEqual(Visibility.Collapsed,
            Element<FrameworkElement>(page, "CompatibilityOutcomePanel").Visibility);
        Assert.AreEqual(Visibility.Visible,
            Element<FrameworkElement>(page, "CompatibilityConfigurePanel").Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ConfigureBack_RetainsRealSelectionAndHandoffWithoutReevaluation()
    {
        CompatibilityViewModelTests.RealExactPlanningFixture fixture =
            CompatibilityViewModelTests.RealExactFixture();
        int evaluations = 0;
        var page = new CompatibilityPage(
            (_, _) =>
            {
                evaluations++;
                return Task.FromResult(fixture.Evaluation);
            },
            fixture.Authority,
            _ => null,
            timeProvider: fixture.TimeProvider)
        {
            StartAutomatically = false
        };
        int backRequests = 0;
        int configureExits = 0;
        int optimizationRequests = 0;
        page.BackRequested += (_, _) => backRequests++;
        page.ConfigureStageExited += (_, _) => configureExits++;
        page.OptimizationRequested += (_, _) => optimizationRequests++;

        await using WinUiRenderHost host =
            await WinUiRenderHost.ShowAsync(page, 900, 760);
        await page.ViewModel.StartAsync();
        page.ViewModel.SelectManualPreference(10);
        int completedEvaluations = evaluations;

        Button choose = Element<Button>(
            page,
            "BtnCompatibilityPrimary");
        Assert.IsTrue(choose.IsLoaded);
        Assert.AreEqual(Visibility.Visible, choose.Visibility);
        Assert.IsTrue(choose.IsEnabled);
        Assert.AreEqual("Choose optimisation", choose.Content);
        Assert.AreEqual("CompatibilityAction.ConfigureModel",
            AutomationProperties.GetAutomationId(choose));
        Assert.AreEqual(Visibility.Collapsed,
            Element<Button>(page, "BtnCompatibilitySecondaryForward").Visibility);
        Invoke(choose);
        OptimizationPreferenceSelection retainedPreference =
            page.ViewModel.SelectedPreference
            ?? throw new AssertFailedException(
                "Configure must synchronize a retained exact preference.");
        OptimizationSelectionHandoff retainedHandoff =
            page.ViewModel.CurrentOptimizationHandoff
            ?? throw new AssertFailedException(
                "Configure must retain its exact issued handoff.");
        CompatibilityOptimizationPresentation retainedOptimization =
            page.ViewModel.Presentation.Optimization
            ?? throw new AssertFailedException(
                "Configure must retain the released safe-slider projection.");
        int retainedIndex = retainedOptimization.SafeSliderSelectedIndex
            ?? throw new AssertFailedException(
                "Configure must retain the synchronized safe-slider index.");
        Assert.AreEqual(OptimizationPreferenceKind.Exact, retainedPreference.Kind);
        Assert.AreEqual(
            retainedOptimization.SafeSliderModes[retainedIndex].CandidateIdentity,
            retainedPreference.ExactCandidateIdentity);
        Assert.AreEqual(
            retainedPreference.ExactCandidateIdentity,
            retainedHandoff.Plan.Preference.ExactCandidateIdentity);
        Button configureBack = Element<Button>(
            page, "BtnCompatibilityConfigureBack");
        Assert.IsTrue(configureBack.IsLoaded);
        Assert.AreEqual(Visibility.Visible, configureBack.Visibility);
        Assert.IsTrue(configureBack.IsEnabled);
        Assert.AreEqual("CompatibilityConfigureAction.Back",
            AutomationProperties.GetAutomationId(configureBack));
        Invoke(configureBack);

        Assert.AreEqual(completedEvaluations, evaluations,
            "Configure Back must not recapture or reevaluate compatibility.");
        Assert.AreEqual(retainedPreference, page.ViewModel.SelectedPreference);
        Assert.AreSame(retainedHandoff, page.ViewModel.CurrentOptimizationHandoff);
        Assert.AreEqual(0, backRequests);
        Assert.AreEqual(1, configureExits);
        Assert.AreEqual(0, optimizationRequests);
        Assert.AreEqual(Visibility.Visible,
            Element<FrameworkElement>(page, "CompatibilityOutcomePanel").Visibility);
        Assert.AreEqual(Visibility.Collapsed,
            Element<FrameworkElement>(page, "CompatibilityConfigurePanel").Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ConfigureStart_ChecksFreshAuthorityAndBackCancelsWithoutDispatch()
    {
        CompatibilityViewModelTests.RealExactPlanningFixture fixture =
            CompatibilityViewModelTests.RealExactFixture();
        var refresh = new TaskCompletionSource<CompatibilityEvaluation>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int evaluations = 0;
        var page = new CompatibilityPage(
            (_, _) => ++evaluations == 1
                ? Task.FromResult(fixture.Evaluation)
                : refresh.Task,
            fixture.Authority,
            _ => null,
            timeProvider: fixture.TimeProvider)
        {
            StartAutomatically = false
        };
        int optimizationRequests = 0;
        page.OptimizationRequested += (_, _) => optimizationRequests++;

        await page.ViewModel.StartAsync();
        InvokePrimaryClickHandler(
            page,
            Element<Button>(page, "BtnCompatibilityPrimary"));
        Button start = Element<Button>(page, "BtnCompatibilityConfigurePrimary");
        Assert.AreSame(page.ViewModel.StartOptimizationCommand, start.Command);
        Assert.IsTrue(start.IsEnabled);

        Task pending = page.ViewModel.StartOptimizationAsync();

        Assert.IsFalse(start.IsEnabled);
        Assert.AreEqual(Visibility.Visible,
            Element<FrameworkElement>(page, "CompatibilitySelectedSetupSummary").Visibility,
            "Checking fresh authority must keep the exact selected setup visible.");
        Assert.AreEqual(Visibility.Visible,
            Element<TextBlock>(page, "CompatibilitySelectionStatusText").Visibility);
        Assert.AreEqual("Checking current memory and storage…",
            Element<TextBlock>(page, "CompatibilitySelectionStatusText").Text);
        Slider slider = Element<Slider>(page, "CompatibilityPreferenceSlider");
        Assert.IsFalse(slider.IsEnabled);
        Assert.AreEqual("Checking current memory and storage",
            AutomationProperties.GetName(slider));
        Assert.AreEqual("Checking current memory and storage…",
            AutomationProperties.GetHelpText(slider));

        Invoke(Element<Button>(page, "BtnCompatibilityConfigureBack"));
        Assert.IsFalse(page.ViewModel.IsOptimizationStartBusy);
        Assert.AreEqual(Visibility.Visible,
            Element<FrameworkElement>(page, "CompatibilityOutcomePanel").Visibility);
        Assert.AreEqual(Visibility.Collapsed,
            Element<FrameworkElement>(page, "CompatibilityConfigurePanel").Visibility);
        refresh.SetResult(fixture.Evaluation);
        await pending;

        Assert.AreEqual(0, optimizationRequests);
        Assert.AreEqual(2, evaluations);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ConfigureSafeSlider_UsesEveryReleasedSourceBackedChoiceAndSynchronizesSelection()
    {
        CompatibilityViewModelTests.RealExactPlanningFixture fixture =
            CompatibilityViewModelTests.RealExactFixture();
        CompatibilityOptimizationView source = fixture.Evaluation.Screen.Optimization
            ?? throw new AssertFailedException("The source-backed fixture needs optimisation choices.");
        Assert.IsTrue(source.SafeSliderModes.Count > 1,
            "The real fixture must exercise a multi-stop safe slider.");
        int initialIndex = source.SafeSliderSelectedIndex
            ?? throw new AssertFailedException("The source-backed fixture needs a selected safe setup.");
        string initialIdentity = source.SafeSliderModes[initialIndex].CandidateIdentity;
        int evaluations = 0;
        var page = new CompatibilityPage(
            (_, _) =>
            {
                evaluations++;
                return Task.FromResult(fixture.Evaluation);
            },
            fixture.Authority,
            _ => null,
            timeProvider: fixture.TimeProvider)
        {
            StartAutomatically = false
        };
        int optimizationRequests = 0;
        int configureEntries = 0;
        page.OptimizationRequested += (_, _) => optimizationRequests++;
        page.ConfigureStageEntered += (_, _) => configureEntries++;

        await page.ViewModel.StartAsync();
        InvokePrimaryClickHandler(
            page,
            Element<Button>(page, "BtnCompatibilityPrimary"));

        Slider slider = Element<Slider>(page, "CompatibilityPreferenceSlider");
        Assert.AreEqual(Visibility.Visible, slider.Visibility);
        Assert.AreEqual(0d, slider.Minimum);
        Assert.AreEqual(source.SafeSliderModes.Count - 1d, slider.Maximum);
        Assert.AreEqual((double)initialIndex, slider.Value);
        Assert.AreEqual(1d, slider.StepFrequency);
        Assert.AreEqual(1d, slider.SmallChange);
        Assert.AreEqual(1d, slider.LargeChange);
        Assert.AreEqual("StepValues", slider.SnapsTo.ToString());
        Assert.IsFalse(slider.IsThumbToolTipEnabled);
        Assert.AreEqual(OptimizationPreferenceKind.Exact,
            page.ViewModel.SelectedPreference?.Kind);
        Assert.AreEqual(initialIdentity,
            page.ViewModel.SelectedPreference?.ExactCandidateIdentity,
            "Entering Configure must synchronize the visible default to the retained exact plan.");
        Assert.AreEqual(initialIdentity,
            page.ViewModel.CurrentOptimizationHandoff?.Plan.Preference.ExactCandidateIdentity);
        Assert.IsTrue(Element<Button>(page, "BtnCompatibilityConfigurePrimary").IsEnabled);
        Assert.AreEqual(1, evaluations);
        Assert.AreEqual(1, configureEntries);
        Assert.AreEqual(0, optimizationRequests);
        Assert.IsNull(page.FindName("CompatibilitySelectionModeSelector"));
        Assert.IsNull(page.FindName("CompatibilityExactSetupComboBox"));
        Assert.IsNull(page.FindName("CompatibilityExperimentalOptionNotice"));

        CompatibilityExactOptimizationModePresentation[] projectedChoices =
            [.. page.ViewModel.Presentation.Optimization!.SafeSliderModes];
        for (int index = 0; index < source.SafeSliderModes.Count; index++)
        {
            slider.Value = index;
            string identity = source.SafeSliderModes[index].CandidateIdentity;
            CompatibilityOptimizationModePresentation expected =
                projectedChoices[index].Mode;
            Assert.AreEqual(identity,
                page.ViewModel.SelectedPreference?.ExactCandidateIdentity,
                $"Slider stop {index} must select only its source-backed identity.");
            Assert.AreEqual(identity,
                page.ViewModel.CurrentOptimizationHandoff?.Plan.Preference.ExactCandidateIdentity,
                $"Slider stop {index} must reissue the matching exact handoff.");
            CompatibilityOptimizationModePresentation selected = page.ViewModel.Presentation
                .Optimization?.SelectedMode
                ?? throw new AssertFailedException("The selected safe setup must be projected.");
            Assert.AreEqual(expected, selected);
            Assert.AreEqual(expected.WeightFormat,
                Element<TextBlock>(page, "CompatibilitySelectedWeightFormat").Text);
            Assert.AreEqual(expected.CacheFormat,
                Element<TextBlock>(page, "CompatibilitySelectedCacheFormat").Text);
            Assert.AreEqual(expected.ContextText,
                Element<TextBlock>(page, "CompatibilitySelectedContext").Text);
            Assert.AreEqual(expected.ExpectedQualityText,
                Element<TextBlock>(page, "CompatibilityExpectedQuality").Text);
        }
        Assert.AreEqual(0, optimizationRequests);
        Assert.AreEqual(1, evaluations);

        slider.Value = 0;
        Assert.IsTrue(page.TryHandlePreferenceSliderKey(
            Windows.System.VirtualKey.Right));
        Assert.AreEqual(1d, slider.Value,
            "GGUF keyboard input must advance exactly one source-backed slot.");
        Assert.IsTrue(page.TryHandlePreferenceSliderKey(
            Windows.System.VirtualKey.PageDown));
        Assert.AreEqual(0d, slider.Value,
            "GGUF page-key input must retreat exactly one source-backed slot.");

        page.ViewModel.SelectExactPreference(new string('d', 64));

        Assert.AreEqual(Visibility.Visible,
            Element<TextBlock>(page, "CompatibilitySelectionStatusText").Visibility);
        Assert.AreEqual(Visibility.Collapsed,
            Element<FrameworkElement>(page, "CompatibilitySelectedSetupSummary").Visibility);
        Assert.IsFalse(Element<Button>(
            page,
            "BtnCompatibilityConfigurePrimary").IsEnabled);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ConfigureStage_SelectedSetupRefreshesAtomicallyFromSelectedMode()
    {
        CompatibilityPage page = CreatePage();
        CompatibilityPresentation initial = SourceBackedSafeSliderPresentation();
        CompatibilityOptimizationPresentation initialOptimization = initial.Optimization
            ?? throw new AssertFailedException("The source-backed fixture needs optimisation choices.");
        Assert.IsTrue(initialOptimization.SafeSliderModes.Count > 1);
        page.Apply(initial);
        InvokePrimaryClickHandler(
            page,
            Element<Button>(page, "BtnCompatibilityPrimary"));

        int changedIndex = initialOptimization.SafeSliderSelectedIndex == 0 ? 1 : 0;
        CompatibilityExactOptimizationModePresentation changedChoice =
            initialOptimization.SafeSliderModes[changedIndex];
        CompatibilityOptimizationModePresentation changed = changedChoice.Mode;
        // Rendering-only atomic projection; real identity selection and plan
        // issuance are exercised by the source-backed ViewModel/page test above.
        CompatibilityPresentation changedPresentation = initial with
        {
            Optimization = initialOptimization with
            {
                SafeSliderSelectedIndex = changedIndex,
                SelectedMode = changed,
                Preference = OptimizationPreferenceSelection.Exact(
                    changedChoice.CandidateIdentity)
            }
        };
        page.Apply(changedPresentation);

        Assert.AreEqual("SELECTED SETUP",
            Element<TextBlock>(page, "CompatibilitySelectedSetupLabel").Text);
        Assert.AreEqual(changed.WeightFormat,
            Element<TextBlock>(page, "CompatibilitySelectedWeightFormat").Text);
        Assert.AreEqual(changed.CacheFormat,
            Element<TextBlock>(page, "CompatibilitySelectedCacheFormat").Text);
        Assert.AreEqual(changed.ContextText,
            Element<TextBlock>(page, "CompatibilitySelectedContext").Text);
        Assert.AreEqual(changed.Label,
            Element<TextBlock>(page, "CompatibilitySelectedMode").Text);
        Assert.AreEqual(changed.ExpectedQualityText,
            Element<TextBlock>(page, "CompatibilityExpectedQuality").Text);
        StringAssert.Contains(
            AutomationProperties.GetName(
                Element<FrameworkElement>(page, "CompatibilitySelectedSetupSummary")),
            changed.WeightFormat);
        StringAssert.Contains(
            AutomationProperties.GetName(
                Element<FrameworkElement>(page, "CompatibilitySelectedSetupSummary")),
            changed.CacheFormat);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void NonAuthoritativeOptimisation_CannotEnterConfigureStage()
    {
        CompatibilityPage page = CreatePage();
        CompatibilityPresentation authoritative = SourceBackedSafeSliderPresentation();
        Assert.IsNotNull(authoritative.Optimization);
        CompatibilityPresentation unavailable = authoritative with
        {
            PrimaryActionEnabled = false,
            Optimization = authoritative.Optimization with
            {
                IsActionAuthoritative = false
            }
        };
        int entered = 0;
        page.ConfigureStageEntered += (_, _) => entered++;

        page.Apply(unavailable);
        Button entry = Element<Button>(page, "BtnCompatibilityPrimary");

        Assert.IsFalse(entry.IsEnabled);
        Assert.AreEqual(Visibility.Collapsed, entry.Visibility,
            "A non-authoritative candidate list must not expose Choose optimisation.");
        InvokePrimaryClickHandler(page, entry);
        Assert.AreEqual(0, entered);
        Assert.AreEqual(Visibility.Visible,
            Element<FrameworkElement>(page, "CompatibilityOutcomePanel").Visibility);
        Assert.AreEqual(Visibility.Collapsed,
            Element<FrameworkElement>(page, "CompatibilityConfigurePanel").Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ConfigureSelector_UsesEveryAuthoritativeSafeFormatAsDiscreteRamOrderedSteps()
    {
        CompatibilityPage page = CreatePage();
        CompatibilityViewModelTests.RealExactPlanningFixture fixture =
            CompatibilityViewModelTests.RealExactFixture();
        CompatibilityPresentation presentation =
            CompatibilityPresentationFactory.From(fixture.Evaluation);
        CompatibilityOptimizationPresentation optimization = presentation.Optimization
            ?? throw new AssertFailedException("The source-backed fixture needs optimisation choices.");
        Assert.IsTrue(optimization.SafeSliderModes.Count > 1);

        page.Apply(presentation);

        InvokePrimaryClickHandler(
            page,
            Element<Button>(page, "BtnCompatibilityPrimary"));

        Slider slider = Element<Slider>(page, "CompatibilityPreferenceSlider");
        Assert.AreEqual(Visibility.Visible, slider.Visibility);
        Assert.AreEqual(0d, slider.Minimum);
        Assert.AreEqual(optimization.SafeSliderModes.Count - 1d, slider.Maximum);
        Assert.AreEqual(1d, slider.StepFrequency);
        Assert.AreEqual("StepValues", slider.SnapsTo.ToString());
        Assert.IsFalse(slider.IsThumbToolTipEnabled);
        Assert.AreEqual(Visibility.Visible,
            Element<FrameworkElement>(page, "CompatibilitySafeSliderEndpoints").Visibility);
        StringAssert.Contains(AutomationProperties.GetHelpText(slider),
            "lower to higher estimated RAM");
        StringAssert.Contains(AutomationProperties.GetHelpText(slider),
            "quality is shown separately");
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ConfigureSelector_HidesSliderAndFocusesTheOnlySafeCandidate()
    {
        CompatibilityPage page = CreatePage();
        CompatibilityViewModelTests.RealExactPlanningFixture fixture =
            CompatibilityViewModelTests.RealExactFixture();
        CompatibilityPresentation presentation =
            CompatibilityPresentationFactory.From(fixture.Evaluation);
        CompatibilityOptimizationPresentation optimization = presentation.Optimization
            ?? throw new AssertFailedException("The source-backed fixture needs optimisation choices.");
        CompatibilityExactOptimizationModePresentation only =
            optimization.SafeSliderModes[0];

        // Rendering-only projection: authority and identity behavior are covered
        // by ConfigureSafeSlider_UsesEveryReleasedSourceBackedChoiceAndSynchronizesSelection.
        page.Apply(presentation with
        {
            Optimization = optimization with
            {
                SafeSliderModes = [only],
                SafeSliderSelectedIndex = 0,
                SelectedMode = only.Mode,
                Preference = OptimizationPreferenceSelection.Exact(
                    only.CandidateIdentity)
            }
        });

        await using WinUiRenderHost host =
            await WinUiRenderHost.ShowAsync(page, 700, 620);
        _ = await host.CaptureAsync();
        InvokePrimaryClickHandler(
            page,
            Element<Button>(page, "BtnCompatibilityPrimary"));
        ContentControl focusTarget = Element<ContentControl>(
            page,
            "CompatibilitySelectedSetupFocusTarget");
        await WaitUntilAsync(() => ReferenceEquals(
            focusTarget,
            Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(page.XamlRoot)));

        Assert.AreEqual(
            Visibility.Collapsed,
            Element<Slider>(page, "CompatibilityPreferenceSlider").Visibility);
        Assert.AreEqual(
            Visibility.Visible,
            Element<FrameworkElement>(page, "CompatibilitySelectedSetupSummary").Visibility);
        Assert.AreEqual(Visibility.Collapsed,
            Element<FrameworkElement>(page, "CompatibilitySafeSliderEndpoints").Visibility);
        Assert.IsNull(page.FindName("CompatibilitySelectionModeSelector"));
        Assert.IsNull(page.FindName("CompatibilityExactSetupComboBox"));
        Assert.IsTrue(focusTarget.IsTabStop);
        Assert.AreEqual(AccessibilityView.Content,
            AutomationProperties.GetAccessibilityView(focusTarget));
        StringAssert.Contains(
            AutomationProperties.GetName(focusTarget),
            only.Mode.WeightFormat);
        StringAssert.Contains(
            AutomationProperties.GetName(focusTarget),
            only.Mode.CacheFormat);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ConfigureSafeSlider_ContainsOnlyReleasedModesAndNoExperimentalControls()
    {
        CompatibilityPage page = CreatePage();
        CompatibilityViewModelTests.RealExactPlanningFixture fixture =
            CompatibilityViewModelTests.RealExactFixture();
        CompatibilityPresentation presentation =
            CompatibilityPresentationFactory.From(fixture.Evaluation);
        CompatibilityOptimizationPresentation optimization = presentation.Optimization
            ?? throw new AssertFailedException("The source-backed fixture needs optimisation choices.");
        int optimizationRequests = 0;
        page.OptimizationRequested += (_, _) => optimizationRequests++;
        page.Apply(presentation);

        Button choose = Element<Button>(page, "BtnCompatibilityPrimary");
        Assert.AreEqual("Choose optimisation", choose.Content);
        Assert.IsTrue(choose.IsEnabled);
        InvokePrimaryClickHandler(page, choose);
        Assert.AreEqual(0, optimizationRequests,
            "Entering Configure must not request optimization before Start.");

        Assert.AreEqual(Visibility.Visible,
            Element<FrameworkElement>(page, "CompatibilityConfigurePanel").Visibility);
        Assert.IsTrue(optimization.SafeSliderModes.Count > 0);
        Assert.IsTrue(optimization.SafeSliderModes.All(mode =>
            mode.Availability == CompatibilityExactOptimizationAvailability.Released
            && !mode.Mode.IsExperimental));
        Assert.IsNull(page.FindName("CompatibilitySelectionModeSelector"));
        Assert.IsNull(page.FindName("CompatibilityExactSetupComboBox"));
        Assert.IsNull(page.FindName("CompatibilityExperimentalOptionNotice"));
        Assert.IsNull(page.FindName("CompatibilityExperimentalConsent"));
        Assert.IsNull(page.FindName("CompatibilityExperimentalFinalConfirmation"));
        Assert.AreEqual(
            "Review the released safe setups available for this model and computer.",
            Element<TextBlock>(page, "CompatibilityConfigureDetail").Text);
        Assert.AreEqual(Visibility.Collapsed,
            Element<FrameworkElement>(page, "CompatibilityOutcomePanel").Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void MissingSafeSliderSelection_RemainsFailClosedInConfigure()
    {
        CompatibilityPage page = CreatePage();
        CompatibilityViewModelTests.RealExactPlanningFixture fixture =
            CompatibilityViewModelTests.RealExactFixture();
        CompatibilityPresentation presentation =
            CompatibilityPresentationFactory.From(fixture.Evaluation);
        CompatibilityOptimizationPresentation optimization = presentation.Optimization
            ?? throw new AssertFailedException("The source-backed fixture needs optimisation choices.");
        page.Apply(presentation);
        InvokePrimaryClickHandler(
            page,
            Element<Button>(page, "BtnCompatibilityPrimary"));

        // Rendering-only stale snapshot: the source-backed ViewModel test above
        // separately proves that an invalid identity cannot issue a handoff.
        page.Apply(presentation with
        {
            PrimaryActionEnabled = false,
            Optimization = optimization with
            {
                SafeSliderSelectedIndex = null,
                SelectionStatusText =
                    "That exact setup is no longer available. Choose another setup to continue."
            }
        });

        Assert.AreEqual(Visibility.Visible,
            Element<FrameworkElement>(page, "CompatibilityConfigurePanel").Visibility);
        Assert.AreEqual(Visibility.Collapsed,
            Element<FrameworkElement>(page, "CompatibilityOutcomePanel").Visibility,
            "Configure must remain isolated from the retained Stage 3 result.");
        Assert.AreEqual(Visibility.Visible,
            Element<TextBlock>(page, "CompatibilitySelectionStatusText").Visibility);
        Slider slider = Element<Slider>(page, "CompatibilityPreferenceSlider");
        Assert.IsFalse(slider.IsEnabled);
        Assert.AreEqual("Safe setup unavailable",
            AutomationProperties.GetName(slider));
        Assert.AreEqual(
            "That exact setup is no longer available. Choose another setup to continue.",
            AutomationProperties.GetHelpText(slider));
        Assert.AreEqual(Visibility.Collapsed,
            Element<FrameworkElement>(page, "CompatibilitySelectedSetupSummary").Visibility);
        Assert.IsFalse(Element<Button>(page, "BtnCompatibilityConfigurePrimary").IsEnabled);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void SafeSliderListBecomingEmpty_ClearsSelectorAndRunnableStart()
    {
        CompatibilityPage page = CreatePage();
        CompatibilityPresentation presentation = SourceBackedSafeSliderPresentation();
        CompatibilityOptimizationPresentation optimization = presentation.Optimization
            ?? throw new AssertFailedException("The source-backed fixture needs optimisation choices.");
        page.Apply(presentation);
        InvokePrimaryClickHandler(
            page,
            Element<Button>(page, "BtnCompatibilityPrimary"));

        // Rendering-only disappearance snapshot. Core/source authority remains
        // exercised by the real fixture before Configure is entered.
        page.Apply(presentation with
        {
            PrimaryActionEnabled = false,
            Optimization = optimization with
            {
                SafeSliderModes = [],
                SafeSliderSelectedIndex = null,
                SelectionStatusText = string.Empty
            }
        });

        Assert.AreEqual(Visibility.Visible,
            Element<FrameworkElement>(page, "CompatibilityConfigurePanel").Visibility);
        Assert.AreEqual(Visibility.Collapsed,
            Element<FrameworkElement>(page, "CompatibilitySafeSliderPanel").Visibility);
        Assert.AreEqual(Visibility.Collapsed,
            Element<Slider>(page, "CompatibilityPreferenceSlider").Visibility);
        Assert.AreEqual(Visibility.Visible,
            Element<TextBlock>(page, "CompatibilitySelectionStatusText").Visibility);
        Assert.AreEqual(
            "No verified safe setup is available for this model and computer.",
            Element<TextBlock>(page, "CompatibilitySelectionStatusText").Text);
        Assert.IsFalse(Element<Button>(page, "BtnCompatibilityConfigurePrimary").IsEnabled);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void MemoryRecoveryRefresh_DoesNotEnterConfigureStage()
    {
        CompatibilityPage page = CreatePage();
        CompatibilityPresentation authoritative = SourceBackedSafeSliderPresentation();
        CompatibilityPresentation recovery = authoritative with
        {
            MemoryRecoveryReason = CompatibilityMemoryRecoveryReason.SystemMemoryPressure
        };
        int entered = 0;
        page.ConfigureStageEntered += (_, _) => entered++;

        page.Apply(authoritative);
        Button secondary = Element<Button>(
            page,
            "BtnCompatibilitySecondaryForward");
        Assert.AreEqual(
            "CompatibilityAction.ConfigureModel",
            AutomationProperties.GetAutomationId(secondary));
        Assert.AreEqual("Choose optimisation",
            AutomationProperties.GetName(secondary));
        string configureHelp = AutomationProperties.GetHelpText(secondary);
        Button normalPrimary = Element<Button>(page, "BtnCompatibilityPrimary");
        Assert.AreEqual(
            "Review admitted optimisation options for this model and current hardware.",
            AutomationProperties.GetHelpText(normalPrimary));

        page.Apply(recovery);
        Button refresh = Element<Button>(page, "BtnCompatibilityPrimary");

        Assert.AreSame(page.ViewModel.RefreshMemoryCommand, refresh.Command);
        Assert.AreEqual(
            page.ViewModel.RefreshMemoryCommand.CanExecute(null),
            refresh.IsEnabled);
        Assert.AreSame(page.ViewModel.OpenTaskManagerCommand, secondary.Command);
        Assert.AreEqual(
            page.ViewModel.OpenTaskManagerCommand.CanExecute(null),
            secondary.IsEnabled);
        Assert.AreEqual(
            "CompatibilityAction.OpenTaskManager",
            AutomationProperties.GetAutomationId(secondary));
        Assert.AreEqual("Open Task Manager",
            AutomationProperties.GetName(secondary));
        Assert.AreEqual(
            "Open Windows Task Manager to close apps and free memory.",
            AutomationProperties.GetHelpText(secondary));
        Assert.AreEqual(
            "CompatibilityAction.RefreshMemory",
            AutomationProperties.GetAutomationId(refresh));
        Assert.AreEqual(
            "Refresh memory and check again",
            AutomationProperties.GetName(refresh));
        Assert.AreEqual(
            "Refresh current memory information and run the compatibility check again.",
            AutomationProperties.GetHelpText(refresh));
        InvokePrimaryClickHandler(page, refresh);
        Assert.AreEqual(0, entered);
        Assert.AreEqual(Visibility.Visible,
            Element<FrameworkElement>(page, "CompatibilityOutcomePanel").Visibility);
        Assert.AreEqual(Visibility.Collapsed,
            Element<FrameworkElement>(page, "CompatibilityConfigurePanel").Visibility);

        page.Apply(authoritative);
        Assert.AreEqual(
            "CompatibilityAction.ConfigureModel",
            AutomationProperties.GetAutomationId(secondary));
        Assert.AreEqual("Choose optimisation",
            AutomationProperties.GetName(secondary));
        Assert.AreEqual(configureHelp,
            AutomationProperties.GetHelpText(secondary));
        Assert.AreEqual(
            "Review admitted optimisation options for this model and current hardware.",
            AutomationProperties.GetHelpText(normalPrimary),
            "normal projection must clear the prior recovery action help text");
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ConfigureStage_CentersIntroductionAndAdaptsActionsAtCompactWidth()
    {
        CompatibilityPage page = CreatePage();
        page.Apply(SourceBackedSafeSliderPresentation());
        InvokePrimaryClickHandler(
            page,
            Element<Button>(page, "BtnCompatibilityPrimary"));

        await using WinUiRenderHost host =
            await WinUiRenderHost.ShowAsync(page, 560, 620);
        _ = await host.CaptureAsync();

        TextBlock heading = Element<TextBlock>(
            page, "CompatibilityConfigureHeading");
        TextBlock detail = Element<TextBlock>(
            page, "CompatibilityConfigureDetail");
        StackPanel actions = Element<StackPanel>(
            page, "CompatibilityConfigureActions");
        StackPanel resultActions = Element<StackPanel>(
            page, "CompatibilityTerminalActions");
        StackPanel resultForwardActions = Element<StackPanel>(
            page, "CompatibilityForwardActions");
        Grid tradeoff = Element<Grid>(
            page, "CompatibilitySelectedTradeoffSummary");
        FrameworkElement quality = Element<FrameworkElement>(
            page, "CompatibilityExpectedQualityGroup");
        Grid selectedSetup = Element<Grid>(
            page, "CompatibilitySelectedSetupSummary");
        FrameworkElement selectedCache = Element<FrameworkElement>(
            page, "CompatibilitySelectedCacheGroup");
        FrameworkElement selectedContext = Element<FrameworkElement>(
            page, "CompatibilitySelectedContextGroup");
        Button back = Element<Button>(page, "BtnCompatibilityConfigureBack");
        Button primary = Element<Button>(page, "BtnCompatibilityConfigurePrimary");

        Assert.AreEqual(TextAlignment.Center, heading.TextAlignment);
        Assert.AreEqual(HorizontalAlignment.Center, heading.HorizontalAlignment);
        Assert.AreEqual(TextAlignment.Center, detail.TextAlignment);
        Assert.AreEqual(HorizontalAlignment.Center, detail.HorizontalAlignment);
        Assert.AreEqual(Orientation.Vertical, actions.Orientation);
        Assert.AreEqual(Orientation.Vertical, resultActions.Orientation);
        Assert.AreEqual(Orientation.Vertical, resultForwardActions.Orientation);
        Assert.AreEqual(1, Grid.GetRow(quality));
        Assert.AreEqual(0, Grid.GetColumn(quality));
        Assert.AreEqual(0d, tradeoff.ColumnDefinitions[1].Width.Value);
        Assert.AreEqual(0d, selectedSetup.ColumnDefinitions[1].Width.Value);
        Assert.AreEqual(0d, selectedSetup.ColumnDefinitions[2].Width.Value);
        Assert.AreEqual(1, Grid.GetRow(selectedCache));
        Assert.AreEqual(0, Grid.GetColumn(selectedCache));
        Assert.AreEqual(2, Grid.GetRow(selectedContext));
        Assert.AreEqual(0, Grid.GetColumn(selectedContext));
        Assert.AreEqual(44d, back.MinHeight);
        Assert.AreEqual(double.NaN, back.Height);
        Assert.AreEqual(44d, primary.MinHeight);
        Assert.AreEqual(double.NaN, primary.Height);

        SolidColorBrush accessibleLabelBrush = Assert.IsInstanceOfType<SolidColorBrush>(
            page.Resources["CompatibilityMutedTextBrush"]);
        TextBlock modeLabel = Descendants(
                Element<FrameworkElement>(page, "CompatibilityOptimizationPanel"))
            .OfType<TextBlock>()
            .Single(text => text.Text == "MODE");
        Assert.AreEqual(
            accessibleLabelBrush.Color,
            Assert.IsInstanceOfType<SolidColorBrush>(modeLabel.Foreground).Color);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void OptimisationRequired_KeepsTypedMemoryOverviewAboveChoices()
    {
        CompatibilityPage page = CreatePage();
        CompatibilityPresentation authoritative = SourceBackedSafeSliderPresentation();
        CompatibilityPresentation presentation = authoritative with
        {
            MemoryOverview = new CompatibilityMemoryOverviewPresentation(
                AvailableSystemMemoryBytes: 4_831_838_208,
                SafetyReserveBytes: 536_870_912,
                SafeModelBudgetBytes: 4_294_967_296,
                CurrentRequiredBytes: 5_368_709_120,
                ContextTokens: 4096,
                MinimumRequiredBytes: 3_221_225_472,
                MinimumRequirementLabel: "Smallest offered setup needs")
        };

        page.Apply(authoritative);
        object? baselinePrimaryContent =
            Element<Button>(page, "BtnCompatibilityPrimary").Content;
        page.Apply(presentation);

        Assert.AreEqual(Visibility.Visible,
            Element<FrameworkElement>(page, "CompatibilityDecisionStrip").Visibility);
        Assert.AreEqual(Visibility.Visible,
            Element<FrameworkElement>(page, "CompatibilityBudgetDiagram").Visibility);
        Button configure = Element<Button>(page, "BtnCompatibilityPrimary");
        Assert.AreEqual(Visibility.Collapsed,
            Element<FrameworkElement>(page, "CompatibilityOptimizationPanel").Visibility);
        InvokePrimaryClickHandler(page, configure);
        Assert.AreEqual(Visibility.Visible,
            Element<FrameworkElement>(page, "CompatibilityOptimizationPanel").Visibility);
        Assert.AreEqual("FREE RAM NOW",
            Element<TextBlock>(page, "CompatibilityMemoryNeededLabel").Text);
        Assert.AreEqual("RAM AVAILABLE TO MODEL",
            Element<TextBlock>(page, "CompatibilitySafeMemoryLabel").Text);
        Assert.AreEqual("CURRENT SETUP PEAK RAM",
            Element<TextBlock>(page, "CompatibilitySpareLabel").Text);
        Assert.AreEqual("SMALLEST OFFERED SETUP NEEDS",
            Element<TextBlock>(page, "CompatibilityContextLabel").Text);
        Assert.AreEqual(Visibility.Collapsed,
            Element<TextBlock>(page, "CompatibilityMemoryClarityShortfall").Visibility);
        Assert.IsTrue(Element<TextBlock>(page, "CompatibilityAvailableRamLabel")
            .Text.StartsWith("RAM available to the model now", StringComparison.Ordinal));
        Assert.IsTrue(Element<TextBlock>(page, "CompatibilityMinimumRamLabel")
            .Text.StartsWith("Smallest offered setup needs", StringComparison.Ordinal));
        Assert.AreEqual(Visibility.Visible,
            Element<Line>(page, "CompatibilityAvailableRamMarker").Visibility);
        Assert.AreEqual(Visibility.Visible,
            Element<Line>(page, "CompatibilityMinimumRamMarker").Visibility);
        Assert.AreEqual(baselinePrimaryContent,
            Element<Button>(page, "BtnCompatibilityPrimary").Content);

        page.Apply(CompatibilityPresentation.Empty);
        Assert.AreEqual(Visibility.Collapsed,
            Element<FrameworkElement>(page, "CompatibilityDecisionStrip").Visibility);
        Assert.AreEqual(Visibility.Collapsed,
            Element<FrameworkElement>(page, "CompatibilityBudgetDiagram").Visibility);
        Assert.AreEqual(Visibility.Collapsed,
            Element<Line>(page, "CompatibilityAvailableRamMarker").Visibility);
        Assert.AreEqual(Visibility.Collapsed,
            Element<Line>(page, "CompatibilityMinimumRamMarker").Visibility);
        Assert.AreEqual(string.Empty,
            Element<TextBlock>(page, "CompatibilityAvailableRamLabel").Text);
        Assert.AreEqual(string.Empty,
            Element<TextBlock>(page, "CompatibilityMinimumRamLabel").Text);
        Assert.AreEqual("Estimated memory budget",
            AutomationProperties.GetName(
                Element<FrameworkElement>(page, "CompatibilityBudgetDiagram")));
        Assert.AreEqual(Visibility.Collapsed,
            Element<FrameworkElement>(page, "CompatibilityAvailableRamElbow").Visibility);
        Assert.AreEqual(Visibility.Collapsed,
            Element<FrameworkElement>(page, "CompatibilityMinimumRamElbow").Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task GgufCurrentFit_OptionalQ8_RendersMemoryParityAndAttachesPng()
    {
        CompatibilityPage page = CreatePage();
        CompatibilityEvaluation evaluation = GgufFitEvaluation(hasOptionalQ8: true);
        Assert.AreEqual(CompatibilityScreenState.EstimatedCompatible,
            evaluation.Screen.State);
        Assert.AreEqual(RuntimeRouteId.LlamaCpp, evaluation.Screen.Setup?.Route);
        Assert.AreEqual(WeightQuantisation.Q4_K_M,
            evaluation.Screen.Setup?.Weights);
        Assert.AreEqual(GgufKvCacheFormat.F16,
            evaluation.Screen.Setup?.GgufKvCache);
        Assert.AreEqual(3UL * 1024 * 1024 * 1024,
            evaluation.Screen.Setup!.Components.Aggregate(
                0UL, (sum, component) => checked(sum + component.Bytes)));
        CompatibilityExactOptimizationModeView offered = evaluation.OptionalOptimization!
            .SafeSliderModes.Single();
        Assert.AreEqual(OptimizationRoute.Gguf, offered.Mode.Route);
        Assert.AreEqual(GgufKvCacheFormat.Q8_0, offered.Mode.GgufKvCache);
        CompatibilityPresentation presentation =
            CompatibilityPresentationFactory.From(evaluation);
        page.Apply(presentation);

        await using WinUiRenderHost host = await WinUiRenderHost.ShowAsync(page, 1000, 760);
        RenderedFrame frame = await host.CaptureAsync();
        string attachment = await frame.SavePngAsync("gguf-optional-q8.png");
        TestContext.AddResultFile(attachment);

        Assert.AreEqual("SMALLEST OFFERED SETUP NEEDS",
            Element<TextBlock>(page, "CompatibilityContextLabel").Text);
        Assert.AreEqual("2.5 GB",
            Element<TextBlock>(page, "CompatibilityContextValue").Text);
        Assert.AreEqual(Visibility.Visible,
            Element<Line>(page, "CompatibilityMinimumRamMarker").Visibility);
        Button optimise = Element<Button>(page, "BtnCompatibilityPrimary");
        Assert.AreEqual("Optimise further", optimise.Content);
        Assert.AreEqual(Visibility.Visible, optimise.Visibility);
        Assert.IsFalse(optimise.IsEnabled,
            "A render-only presentation must not create optimisation authority.");
        Assert.AreEqual("CompatibilityAction.OptimiseFurther",
            AutomationProperties.GetAutomationId(optimise));
        Assert.AreEqual(Visibility.Collapsed,
            Element<FrameworkElement>(page, "CompatibilityOptimizationPanel").Visibility);
        Assert.IsTrue(File.Exists(attachment));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task GgufCurrentFit_NoOptionalSetup_RendersUnavailableAndAttachesPng()
    {
        CompatibilityPage page = CreatePage();
        CompatibilityEvaluation evaluation = GgufFitEvaluation(hasOptionalQ8: false);
        Assert.AreEqual(CompatibilityScreenState.EstimatedCompatible,
            evaluation.Screen.State);
        Assert.AreEqual(RuntimeRouteId.LlamaCpp, evaluation.Screen.Setup?.Route);
        Assert.AreEqual(GgufKvCacheFormat.F16,
            evaluation.Screen.Setup?.GgufKvCache);
        Assert.AreEqual(3UL * 1024 * 1024 * 1024,
            evaluation.Screen.Setup!.Components.Aggregate(
                0UL, (sum, component) => checked(sum + component.Bytes)));
        Assert.IsNull(evaluation.OptionalOptimization);
        CompatibilityPresentation presentation =
            CompatibilityPresentationFactory.From(evaluation);
        page.Apply(presentation);

        await using WinUiRenderHost host = await WinUiRenderHost.ShowAsync(page, 1000, 760);
        RenderedFrame frame = await host.CaptureAsync();
        string attachment = await frame.SavePngAsync("gguf-no-optional.png");
        TestContext.AddResultFile(attachment);

        Assert.AreEqual("SMALLEST OFFERED SETUP NEEDS",
            Element<TextBlock>(page, "CompatibilityContextLabel").Text);
        Assert.AreEqual("Not available",
            Element<TextBlock>(page, "CompatibilityContextValue").Text);
        Assert.AreEqual(Visibility.Collapsed,
            Element<Line>(page, "CompatibilityMinimumRamMarker").Visibility);
        Assert.AreEqual(Visibility.Collapsed,
            Element<TextBlock>(page, "CompatibilityMinimumRamLabel").Visibility);
        Button optimise = Element<Button>(page, "BtnCompatibilityPrimary");
        Assert.AreEqual("Optimise further", optimise.Content);
        Assert.AreEqual(Visibility.Visible, optimise.Visibility);
        Assert.IsFalse(optimise.IsEnabled);
        Assert.IsTrue(File.Exists(attachment));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task SecondaryAction_InvokesCancelRetryAndBackForTheRenderedState()
    {
        TaskCompletionSource<CompatibilityScreenModel> first =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        var page = new CompatibilityPage(_ => ++calls == 1
            ? first.Task
            : Task.FromResult(CompatibilityScreenModel.ForPresentation(
                CompatibilityScreenState.EstimatedCompatible,
                [], [], BaselineExclusionReason.None,
                useCurrentModelAvailable: false,
                continueEnabled: true)))
        {
            StartAutomatically = false
        };
        Button cancel = Element<Button>(page, "BtnCancelCompatibility");
        Button secondary = Element<Button>(page, "BtnCompatibilityBack");

        Task analysing = page.ViewModel.StartAsync();
        Assert.AreEqual("Cancel", cancel.Content);
        Invoke(cancel);
        first.SetResult(CompatibilityScreenModel.ForPresentation(
            CompatibilityScreenState.EstimatedCompatible,
            [], [], BaselineExclusionReason.None,
            useCurrentModelAvailable: false,
            continueEnabled: true));
        await analysing;
        Assert.AreEqual("Check stopped", page.ViewModel.Presentation.OutcomeTitle);

        Assert.AreEqual("Check again", secondary.Content);
        Assert.AreEqual(
            "CompatibilityAction.Retry",
            AutomationProperties.GetAutomationId(secondary));
        Assert.AreEqual("Check again", AutomationProperties.GetName(secondary));
        Assert.AreEqual(
            "Run the compatibility check again.",
            AutomationProperties.GetHelpText(secondary));
        Invoke(secondary);
        await WaitUntilAsync(() =>
            calls == 2
            && page.ViewModel.Presentation.SecondaryActionKind
                == CompatibilitySecondaryActionKind.Back
            && Equals(secondary.Content, "Back"));
        Assert.IsTrue(page.ViewModel.Presentation.OutcomeTitle.StartsWith(
            "Yes",
            StringComparison.Ordinal));

        bool backed = false;
        page.BackRequested += (_, _) => backed = true;
        Assert.AreEqual("Back", secondary.Content);
        Assert.AreEqual(
            "CompatibilityAction.Back",
            AutomationProperties.GetAutomationId(secondary));
        Assert.AreEqual("Back", AutomationProperties.GetName(secondary));
        Assert.AreEqual(
            "Return to model inspection.",
            AutomationProperties.GetHelpText(secondary));
        Invoke(secondary);
        Assert.IsTrue(backed);

        page.Apply(page.ViewModel.Presentation with
        {
            SecondaryActionText = "Cancel",
            SecondaryActionEnabled = true,
            SecondaryActionKind = CompatibilitySecondaryActionKind.Cancel
        });
        Assert.AreEqual(
            "CompatibilityAction.Cancel",
            AutomationProperties.GetAutomationId(secondary));
        Assert.AreEqual("Cancel", AutomationProperties.GetName(secondary));
        Assert.AreEqual(
            "Stop the compatibility check.",
            AutomationProperties.GetHelpText(secondary));

        page.Apply(CompatibilityPresentation.Empty with
        {
            SecondaryActionText = "Unavailable",
            SecondaryActionEnabled = true,
            SecondaryActionKind = CompatibilitySecondaryActionKind.None
        });
        Assert.IsFalse(secondary.IsEnabled);
        Assert.IsNull(secondary.Command);
        Assert.AreEqual(string.Empty,
            AutomationProperties.GetAutomationId(secondary));
        Assert.AreEqual(string.Empty,
            AutomationProperties.GetName(secondary));
        Assert.AreEqual(string.Empty,
            AutomationProperties.GetHelpText(secondary));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task NotEstablished_OffersOneAccessibleImportRequestBesideRetry()
    {
        CompatibilityPage page = CreatePage();
        page.Apply(CompatibilityPresentationFactory.From(
            CompatibilityScreenModel.ForPresentation(
                CompatibilityScreenState.NotEstablished,
                [], [], BaselineExclusionReason.None,
                useCurrentModelAvailable: false,
                continueEnabled: false)));
        Button retry = Element<Button>(page, "BtnCompatibilityBack");
        Button import = Element<Button>(page, "BtnCompatibilitySecondaryForward");
        Button proceed = Element<Button>(page, "BtnCompatibilityPrimary");
        int requests = 0;
        page.ImportAnotherModelRequested += (_, _) => requests++;

        await using WinUiRenderHost host =
            await WinUiRenderHost.ShowAsync(page, 900, 760);
        _ = await host.CaptureAsync();

        Assert.AreEqual("Check again", retry.Content);
        Assert.AreEqual("Import another model", import.Content);
        Assert.AreEqual(Visibility.Visible, import.Visibility);
        Assert.IsTrue(import.IsEnabled);
        Assert.AreEqual(
            "CompatibilityAction.ImportAnotherModel",
            AutomationProperties.GetAutomationId(import));
        Assert.AreEqual("Import another model", AutomationProperties.GetName(import));
        Assert.AreEqual(
            "Return to model selection to import another model.",
            AutomationProperties.GetHelpText(import));
        Assert.AreEqual("Continue", proceed.Content);
        Assert.IsFalse(proceed.IsEnabled);

        Assert.IsTrue(import.IsLoaded);
        Invoke(import);

        Assert.AreEqual(1, requests);
        Assert.IsFalse(import.IsEnabled);
        Assert.IsTrue(page.CanCompleteImportNavigation);
        page.CancelImportNavigation();
        Assert.IsTrue(import.IsEnabled);
        Assert.IsFalse(page.CanCompleteImportNavigation);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ConfigureSafeSliderAccessibilityTracksTheExactModeAndIndependentQuality()
    {
        CompatibilityPage page = CreatePage();
        CompatibilityViewModelTests.RealExactPlanningFixture fixture =
            CompatibilityViewModelTests.RealExactFixture();
        CompatibilityPresentation automatic =
            CompatibilityPresentationFactory.From(fixture.Evaluation);
        CompatibilityOptimizationPresentation optimization = automatic.Optimization
            ?? throw new AssertFailedException("The source-backed fixture needs optimisation choices.");
        Assert.IsTrue(optimization.SafeSliderModes.Count > 1);
        page.Apply(automatic);
        InvokePrimaryClickHandler(page, Element<Button>(page, "BtnCompatibilityPrimary"));
        Slider slider = Element<Slider>(page, "CompatibilityPreferenceSlider");

        int selectedIndex = optimization.SafeSliderModes.Count - 1;
        CompatibilityExactOptimizationModePresentation selectedChoice =
            optimization.SafeSliderModes[selectedIndex];
        // Rendering-only accessibility projection over identities supplied by
        // the real source-backed factory presentation.
        page.Apply(automatic with
        {
            Optimization = optimization with
            {
                SafeSliderSelectedIndex = selectedIndex,
                SelectedMode = selectedChoice.Mode,
                Preference = OptimizationPreferenceSelection.Exact(
                    selectedChoice.CandidateIdentity)
            }
        });

        string selectedSetup =
            $"{Element<TextBlock>(page, "CompatibilitySelectedWeightFormat").Text} \u00B7 " +
            Element<TextBlock>(page, "CompatibilitySelectedCacheFormat").Text;
        Assert.AreEqual(
            $"{selectedChoice.Mode.WeightFormat} \u00B7 {selectedChoice.Mode.CacheFormat}",
            selectedSetup);
        StringAssert.Contains(selectedSetup, "\u00B7");
        Assert.IsFalse(selectedSetup.Contains("\u00C2", StringComparison.Ordinal));
        Assert.AreEqual((double)selectedIndex, slider.Value);
        StringAssert.Contains(AutomationProperties.GetName(slider),
            $"Safe setup {selectedIndex + 1} of {optimization.SafeSliderModes.Count}");
        StringAssert.Contains(AutomationProperties.GetHelpText(slider),
            selectedChoice.Mode.WeightFormat);
        StringAssert.Contains(AutomationProperties.GetHelpText(slider),
            selectedChoice.Mode.CacheFormat);
        StringAssert.Contains(AutomationProperties.GetHelpText(slider),
            selectedChoice.Mode.ExpectedQualityText);
        StringAssert.Contains(AutomationProperties.GetHelpText(slider),
            "quality is shown separately");

        page.Apply(automatic);
        Assert.AreEqual((double)optimization.SafeSliderSelectedIndex!.Value, slider.Value);
        StringAssert.Contains(
            AutomationProperties.GetName(slider),
            $"Safe setup {optimization.SafeSliderSelectedIndex!.Value + 1} " +
            $"of {optimization.SafeSliderModes.Count}");
        Assert.IsNull(page.FindName("CompatibilityExperimentalOptionNotice"));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task OpenVinoFiveStopsBindExactAuthoritativeCandidatesAndEnableStart()
    {
        OpenVinoStaticPackageEvidence evidence = new(
            1,
            new string('a', 64),
            VerifiedOpenVinoOptimizationEvidence.SourceModelSha256,
            6_805_673_303,
            "granite",
            "GraniteForCausalLM",
            "text-generation-with-past",
            131_072,
            "float16",
            "PreTrainedTokenizerFast",
            10,
            true,
            40,
            4096,
            32,
            8);
        Guid hardwareRun = Guid.NewGuid();
        ModelInspectionHandoff model = new(
            ModelInspectionHandoff.CurrentSchemaVersion,
            Guid.NewGuid(),
            Guid.NewGuid(),
            ModelInspectionOutcome.Ready,
            evidence.ModelSha256,
            evidence.ModelLengthBytes);
        HardwareInspectionHandoff hardware = HardwareInspectionHandoff.Create(
            hardwareRun,
            HardwareInspectionOutcome.Completed,
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation(
                hardwareRun));
        Assert.IsTrue(OpenVinoCompatibilityInputProjector.TryPrepare(
            model, evidence, hardwareRun, hardware, out var prepared));
        var official =
            OpenVinoOfficialWorkerAuthority.CreateInstallation(
                System.IO.Path.GetFullPath("official-test-worker"),
                VerifiedOpenVinoOptimizationEvidence
                    .PackagedOfficialWorkerManifestSha256);
        var turbo =
            OpenVinoTurboQuantWorkerAuthority.CreateInstallation(
                System.IO.Path.GetFullPath("turbo-test-worker"),
                VerifiedOpenVinoOptimizationEvidence
                    .PackagedTurboWorkerManifestSha256,
                VerifiedOpenVinoOptimizationEvidence.TurboPatchSeriesSha256,
                VerifiedOpenVinoOptimizationEvidence
                    .TurboRuntimeManifestSha256);
        Assert.IsTrue(OpenVinoOptimizationProductionAuthority.TryCreate(
            prepared!,
            official.ExpectedBuildEvidence,
            turbo.ExpectedBuildEvidence,
            true,
            out var authority));
        DateTimeOffset now = DateTimeOffset.UtcNow;
        CompatibilityFreshResourcesInput resources =
            CompatibilityFreshResourcesInput.Create(
                CurrentlyAvailableMemory.FromBytes(8UL * 1024 * 1024 * 1024),
                null,
                64UL * 1024 * 1024 * 1024,
                now);
        CompatibilityPage page = new(
            (consent, token) => Task.FromResult(authority!.Evaluate(
                resources, consent, now, token)),
            authority!,
            _ => null)
        {
            StartAutomatically = false
        };
        await using WinUiRenderHost host =
            await WinUiRenderHost.ShowAsync(page, 1000, 760);
        await page.ViewModel.StartAsync();
        InvokePrimaryClickHandler(
            page,
            Element<Button>(page, "BtnCompatibilityPrimary"));

        Slider slider = Element<Slider>(page, "CompatibilityPreferenceSlider");
        Button start = Element<Button>(page, "BtnCompatibilityConfigurePrimary");
        Assert.AreEqual(50d, slider.Value,
            "Configure must enter on the exact authoritative Balanced candidate.");
        Assert.AreEqual(20d, slider.SmallChange,
            "Arrow keys must traverse one OpenVINO preference stop at a time.");
        Assert.AreEqual(20d, slider.LargeChange,
            "Page-key input must remain aligned to an OpenVINO preference stop.");
        Assert.AreEqual("Balanced",
            Element<TextBlock>(page, "CompatibilitySelectedMode").Text);
        Assert.AreEqual("INT4",
            Element<TextBlock>(page, "CompatibilitySelectedWeightFormat").Text);
        Assert.AreEqual("U4",
            Element<TextBlock>(page, "CompatibilitySelectedCacheFormat").Text);

        var selectedIdentities = new HashSet<string>(StringComparer.Ordinal);
        foreach ((int stop, string label, string weight, string cache,
            string context) in new[]
        {
            (10, "Maximum efficiency", "INT4", "TurboQuant TBQ3", "4,096 tokens"),
            (30, "Efficient", "INT4", "TurboQuant TBQ4", "4,096 tokens"),
            (50, "Balanced", "INT4", "U4", "4,096 tokens"),
            (70, "High capability", "INT4", "U8", "4,096 tokens"),
            (90, "Maximum capability", "INT8",
                "Automatic (OpenVINO default)", "4,096 tokens")
        })
        {
            slider.Value = stop;
            CompatibilityOptimizationPresentation optimization =
                page.ViewModel.Presentation.Optimization!;
            OptimizationPreferenceSelection selected =
                page.ViewModel.SelectedPreference!;
            CompatibilityExactOptimizationModePresentation[] exactMatches =
            [.. optimization.ExactSafeModes.Where(mode => string.Equals(
                    mode.CandidateIdentity,
                    selected.ExactCandidateIdentity,
                    StringComparison.Ordinal))
                .Take(2)];
            Assert.AreEqual(1, exactMatches.Length,
                $"Stop {stop} must resolve exactly one authoritative exact candidate.");
            CompatibilityExactOptimizationModePresentation chosen =
                exactMatches[0];
            Assert.AreEqual(OptimizationPreferenceKind.Exact, selected.Kind, stop.ToString());
            Assert.AreEqual(chosen.CandidateIdentity,
                selected.ExactCandidateIdentity, stop.ToString());
            Assert.AreEqual(chosen.CandidateIdentity,
                page.ViewModel.CurrentOptimizationHandoff?.Plan.Preference
                    .ExactCandidateIdentity, stop.ToString());
            Assert.IsTrue(selectedIdentities.Add(chosen.CandidateIdentity),
                $"Stop {stop} must bind a distinct exact candidate identity.");
            Assert.AreEqual((double)stop, slider.Value,
                $"Stop {stop} must remain selected after exact candidate binding.");
            Assert.AreEqual(label,
                Element<TextBlock>(page, "CompatibilitySelectedMode").Text,
                stop.ToString());
            Assert.AreEqual(chosen.Mode.WeightFormat,
                Element<TextBlock>(page, "CompatibilitySelectedWeightFormat").Text,
                stop.ToString());
            Assert.AreEqual(chosen.Mode.CacheFormat,
                Element<TextBlock>(page, "CompatibilitySelectedCacheFormat").Text,
                stop.ToString());
            Assert.AreEqual(weight, chosen.Mode.WeightFormat,
                $"Stop {stop} must resolve the expected exact weight format.");
            Assert.AreEqual(cache, chosen.Mode.CacheFormat,
                $"Stop {stop} must resolve the expected exact KV-cache format.");
            Assert.AreEqual(context, chosen.Mode.ContextText,
                $"Stop {stop} must resolve the expected exact context.");
            Assert.AreEqual(context,
                Element<TextBlock>(page, "CompatibilitySelectedContext").Text,
                $"Stop {stop} must render the exact selected context.");
            Assert.IsTrue(start.IsEnabled, stop.ToString());
            Assert.IsTrue(page.ViewModel.StartOptimizationCommand.CanExecute(null),
                stop.ToString());
        }
        Assert.AreEqual(5, selectedIdentities.Count);

        slider.Value = 10;
        Assert.IsTrue(page.TryHandlePreferenceSliderKey(
            Windows.System.VirtualKey.Right));
        Assert.AreEqual(30d, slider.Value);
        Assert.AreEqual("Efficient",
            Element<TextBlock>(page, "CompatibilitySelectedMode").Text);
        Assert.IsTrue(page.TryHandlePreferenceSliderKey(
            Windows.System.VirtualKey.PageUp));
        Assert.AreEqual(50d, slider.Value);
        Assert.IsTrue(page.TryHandlePreferenceSliderKey(
            Windows.System.VirtualKey.End));
        Assert.AreEqual(90d, slider.Value);
        Assert.IsTrue(page.TryHandlePreferenceSliderKey(
            Windows.System.VirtualKey.Left));
        Assert.AreEqual(70d, slider.Value);
        Assert.IsTrue(page.TryHandlePreferenceSliderKey(
            Windows.System.VirtualKey.PageDown));
        Assert.AreEqual(50d, slider.Value);
        Assert.IsTrue(page.TryHandlePreferenceSliderKey(
            Windows.System.VirtualKey.Home));
        Assert.AreEqual(10d, slider.Value);
        Assert.IsTrue(start.IsEnabled);
    }

    [TestMethod]
    public void OpenVinoNearestSurvivorUsesLowerPreferenceForAnEqualDistance()
    {
        Assert.AreEqual(30,
            CompatibilityPage.ResolveNearestOpenVinoPreferenceValue(
                50,
                new[] { 30, 70 }));
        Assert.AreEqual(70,
            CompatibilityPage.ResolveNearestOpenVinoPreferenceValue(
                90,
                new[] { 10, 30, 70 }));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task OpenVinoLowMemoryUnavailablePointerAndKeyboardStopsSnapSafely()
    {
        const ulong gib = 1024UL * 1024 * 1024;
        CompatibilityPage page = CreateOpenVinoRaw3BPage(4 * gib);
        await using WinUiRenderHost host =
            await WinUiRenderHost.ShowAsync(page, 1000, 760);
        await page.ViewModel.StartAsync();
        InvokePrimaryClickHandler(
            page,
            Element<Button>(page, "BtnCompatibilityPrimary"));

        Slider slider = Element<Slider>(page, "CompatibilityPreferenceSlider");
        Assert.IsTrue(slider.IsEnabled,
            "At least one authoritative survivor must keep the slider actionable.");
        Assert.IsFalse(page.ViewModel.Presentation.Optimization!.ExactSafeModes.Any(
            mode => OpenVinoPreferenceStop(mode) == 90),
            "The low-memory fixture must exclude Maximum capability.");

        slider.Value = 90;
        AssertOpenVinoSelectionAligned(page, 70);

        Assert.IsTrue(page.TryHandlePreferenceSliderKey(
            Windows.System.VirtualKey.Home));
        AssertOpenVinoSelectionAligned(page, 10);
        Assert.IsTrue(page.TryHandlePreferenceSliderKey(
            Windows.System.VirtualKey.End));
        AssertOpenVinoSelectionAligned(page, 70);
        Assert.IsTrue(page.TryHandlePreferenceSliderKey(
            Windows.System.VirtualKey.Left));
        AssertOpenVinoSelectionAligned(page, 50);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task OpenVinoSelectionFailureKeepsSurvivorsSelectableUntilValidatedRecovery()
    {
        CompatibilityPage page = CreateOpenVinoRaw3BPage(
            4UL * 1024 * 1024 * 1024);
        await using WinUiRenderHost host =
            await WinUiRenderHost.ShowAsync(page, 1000, 760);
        await page.ViewModel.StartAsync();
        InvokePrimaryClickHandler(page,
            Element<Button>(page, "BtnCompatibilityPrimary"));

        page.ViewModel.SelectExactPreference(string.Empty);

        Slider slider = Element<Slider>(page, "CompatibilityPreferenceSlider");
        Assert.IsFalse(string.IsNullOrWhiteSpace(
            page.ViewModel.Presentation.Optimization!.SelectionStatusText));
        Assert.IsTrue(slider.IsEnabled,
            "Failure must retain selection among authoritative surviving choices.");
        Assert.IsNull(page.ViewModel.CurrentOptimizationHandoff);
        Assert.IsFalse(page.ViewModel.StartOptimizationCommand.CanExecute(null));
        Assert.IsFalse(Element<Button>(page, "BtnCompatibilityConfigurePrimary").IsEnabled);

        Assert.IsTrue(page.TryHandlePreferenceSliderKey(Windows.System.VirtualKey.Home));
        AssertOpenVinoSelectionAligned(page, 10);
        Assert.IsTrue(string.IsNullOrWhiteSpace(
            page.ViewModel.Presentation.Optimization!.SelectionStatusText));
        Assert.IsNotNull(page.ViewModel.CurrentOptimizationHandoff);
        Assert.IsTrue(page.ViewModel.StartOptimizationCommand.CanExecute(null));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task OpenVinoRemovedCurrentFallsBackAndInvalidFrontiersFailClosed()
    {
        CompatibilityPage page = CreateOpenVinoRaw3BPage(
            8UL * 1024 * 1024 * 1024);
        await using WinUiRenderHost host =
            await WinUiRenderHost.ShowAsync(page, 1000, 760);
        await page.ViewModel.StartAsync();
        InvokePrimaryClickHandler(
            page,
            Element<Button>(page, "BtnCompatibilityPrimary"));

        Slider slider = Element<Slider>(page, "CompatibilityPreferenceSlider");
        slider.Value = 70;
        AssertOpenVinoSelectionAligned(page, 70);
        string staleIdentity = page.ViewModel.CurrentOptimizationHandoff!.Plan
            .Preference.ExactCandidateIdentity!;

        CompatibilityPresentation full = page.ViewModel.Presentation;
        CompatibilityOptimizationPresentation optimization = full.Optimization!;
        CompatibilityExactOptimizationModePresentation[] lowerSurvivors =
        [
            ExactOpenVinoChoiceAt(optimization, 10),
            ExactOpenVinoChoiceAt(optimization, 30)
        ];
        page.Apply(full with
        {
            Optimization = optimization with { ExactSafeModes = lowerSurvivors }
        });

        AssertOpenVinoSelectionAligned(page, 30);
        Assert.AreNotEqual(staleIdentity,
            page.ViewModel.CurrentOptimizationHandoff!.Plan.Preference
                .ExactCandidateIdentity,
            "A removed current choice must not retain its stale issued handoff.");

        CompatibilityPresentation rebound = page.ViewModel.Presentation;
        page.Apply(rebound with
        {
            Optimization = rebound.Optimization! with { ExactSafeModes = [] }
        });
        Assert.IsNull(page.ViewModel.CurrentOptimizationHandoff);
        Assert.IsFalse(page.ViewModel.StartOptimizationCommand.CanExecute(null));
        Assert.IsFalse(Element<Button>(
            page,
            "BtnCompatibilityConfigurePrimary").IsEnabled);
        Assert.IsFalse(slider.IsEnabled);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task OpenVinoAmbiguousOrDuplicateReleasedFrontierFailsClosed()
    {
        CompatibilityPage page = CreateOpenVinoRaw3BPage(
            8UL * 1024 * 1024 * 1024);
        await using WinUiRenderHost host =
            await WinUiRenderHost.ShowAsync(page, 1000, 760);
        await page.ViewModel.StartAsync();
        InvokePrimaryClickHandler(
            page,
            Element<Button>(page, "BtnCompatibilityPrimary"));

        CompatibilityPresentation full = page.ViewModel.Presentation;
        CompatibilityOptimizationPresentation optimization = full.Optimization!;
        CompatibilityExactOptimizationModePresentation balanced =
            ExactOpenVinoChoiceAt(optimization, 50);
        CompatibilityExactOptimizationModePresentation ambiguous = balanced with
        {
            CandidateIdentity = new string('f', 64)
        };
        page.Apply(full with
        {
            Optimization = optimization with
            {
                ExactSafeModes = [.. optimization.ExactSafeModes, ambiguous]
            }
        });

        Assert.IsNull(page.ViewModel.CurrentOptimizationHandoff);
        Assert.IsFalse(page.ViewModel.StartOptimizationCommand.CanExecute(null));
        Assert.IsFalse(Element<Button>(
            page,
            "BtnCompatibilityConfigurePrimary").IsEnabled);
        Assert.IsFalse(Element<Slider>(
            page,
            "CompatibilityPreferenceSlider").IsEnabled);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void NativePreferenceSlidersRetainSystemResolvedHighContrastResources()
    {
        CompatibilityPage compatibilityPage = CreatePage();
        ResourceDictionary highContrast = Assert.IsInstanceOfType<ResourceDictionary>(
            compatibilityPage.Resources.ThemeDictionaries["HighContrast"]);
        Assert.IsInstanceOfType<SolidColorBrush>(
            highContrast["CompatibilityAccentBrush"]);
        Assert.IsFalse(compatibilityPage.Resources.MergedDictionaries.Any(
            dictionary => dictionary.Source?.OriginalString.EndsWith(
                "/ModelPreferenceSliderTheme.xaml",
                StringComparison.Ordinal) == true),
            "The native compatibility slider must not depend on an orphan shared dictionary.");
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task HostLifetime_UnloadRetiresLateWorkAndReloadStartsExactlyOnce()
    {
        TaskCompletionSource<CompatibilityScreenModel> first =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        var page = new CompatibilityPage(_ => ++calls == 1
            ? first.Task
            : Task.FromResult(CompatibilityScreenModel.ForPresentation(
                CompatibilityScreenState.NotEstablished,
                [], [], BaselineExclusionReason.None,
                useCurrentModelAvailable: false,
                continueEnabled: false)));

        Task firstLifetime = InvokeLifecycleAsync(page, "ActivateAsync");
        await WaitUntilAsync(() => calls == 1);
        await InvokeLifecycleAsync(page, "ActivateAsync");
        Assert.AreEqual(1, calls, "Repeated Loaded must be idempotent in one lifetime.");

        InvokeLifecycle(page, "Deactivate");
        first.SetResult(CompatibilityScreenModel.ForPresentation(
            CompatibilityScreenState.EstimatedCompatible,
            [], [], BaselineExclusionReason.None,
            useCurrentModelAvailable: false,
            continueEnabled: true));
        await firstLifetime;
        Assert.AreEqual(
            CompatibilitySecondaryActionKind.Cancel,
            page.ViewModel.Presentation.SecondaryActionKind);
        Assert.IsFalse(page.ViewModel.Presentation.PrimaryActionEnabled);

        await InvokeLifecycleAsync(page, "ActivateAsync");
        Assert.AreEqual(2, calls, "Reload starts one intentional fresh attempt.");
        await InvokeLifecycleAsync(page, "ActivateAsync");
        Assert.AreEqual(2, calls);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task HostLoadedBoundary_ReportsOneSafeEvaluatorFaultWithoutThrowing()
    {
        const string privateText = @"adapter failed at C:\Users\private\model.gguf";
        var reporter = new BoundedApplicationFaultReporter(capacity: 4);
        var page = new CompatibilityPage(
            _ => Task.FromException<CompatibilityScreenModel>(
                new InvalidOperationException(privateText)),
            faultReporter: reporter);

        await InvokeLifecycleAsync(page, "ActivateAsync");

        Assert.AreEqual(
            "The compatibility check could not finish",
            page.ViewModel.Presentation.OutcomeTitle);
        Assert.AreEqual("Check again", Element<Button>(page, "BtnCompatibilityBack").Content);
        IReadOnlyList<ApplicationFault> faults = reporter.Capture();
        Assert.HasCount(1, faults);
        ApplicationFault fault = faults[0];
        Assert.AreEqual(ApplicationFaultCode.CompatibilityEvaluationUnexpected, fault.Code);
        Assert.AreEqual(ApplicationFaultClassification.InvalidOperation, fault.Classification);
        Assert.IsFalse(fault.ToString().Contains(privateText, StringComparison.Ordinal));

        InvokeLifecycle(page, "Deactivate");
        await InvokeLifecycleAsync(page, "ActivateAsync");
        Assert.HasCount(1, reporter.Capture(), "The host boundary reports its fault code once.");
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task HostLoadedTypedUnavailabilityRendersSafelyWithoutFaultReport()
    {
        var reporter = new BoundedApplicationFaultReporter(capacity: 4);
        var source = new UnavailableFreshSource();
        var orchestrator = A1BackendProductionAuthorities.Shared.CreateCompatibility(
            source,
            TimeProvider.System);
        var page = new CompatibilityPage(
            token => orchestrator.EvaluateBoundAsync(UnreachableBinder, token),
            faultReporter: reporter);

        await InvokeLifecycleAsync(page, "ActivateAsync");

        Assert.AreEqual(
            "We can't answer this yet",
            page.ViewModel.Presentation.OutcomeTitle);
        Assert.AreEqual(1, source.CaptureCount);
        Assert.HasCount(0, reporter.Capture());
    }

    [TestMethod]
    public void RequiredOptimizationFixtures_CarryExactRouteFormatsAndWarnings()
    {
        var expected = new[]
        {
            new { Id = "CMP-020", Route = OptimizationRoute.Gguf, CurrentWeight = "Q4_K_M", CurrentCache = "F16", RecommendedWeight = "Q3_K_M", RecommendedCache = "Q8_0", Quality = "Expected quality: Good", Experimental = false, Strong = false },
            new { Id = "CMP-021", Route = OptimizationRoute.Gguf, CurrentWeight = "Q4_K_M", CurrentCache = "F16", RecommendedWeight = "Q2_K", RecommendedCache = "Q8_0", Quality = "Expected quality: Not established", Experimental = false, Strong = true },
            new { Id = "CMP-022", Route = OptimizationRoute.Gguf, CurrentWeight = "Q4_K_M", CurrentCache = "F16", RecommendedWeight = "Q3_K_M", RecommendedCache = "Q8_0", Quality = "Expected quality: Acceptable", Experimental = false, Strong = false },
            new { Id = "CMP-023", Route = OptimizationRoute.OpenVino, CurrentWeight = "INT8", CurrentCache = "U8", RecommendedWeight = "INT4", RecommendedCache = "Automatic (OpenVINO default)", Quality = "Expected quality: Good", Experimental = false, Strong = false },
            new { Id = "CMP-024", Route = OptimizationRoute.OpenVino, CurrentWeight = "INT8", CurrentCache = "U8", RecommendedWeight = "INT8", RecommendedCache = "U4", Quality = "Expected quality: Excellent", Experimental = false, Strong = false },
            new { Id = "CMP-025", Route = OptimizationRoute.OpenVino, CurrentWeight = "INT8", CurrentCache = "U8", RecommendedWeight = "INT8", RecommendedCache = "TurboQuant TBQ4", Quality = "Expected quality: Good", Experimental = true, Strong = false },
            new { Id = "CMP-026", Route = OptimizationRoute.OpenVino, CurrentWeight = "INT8", CurrentCache = "U8", RecommendedWeight = "INT8", RecommendedCache = "TurboQuant TBQ3", Quality = "Expected quality: Acceptable", Experimental = true, Strong = true },
            new { Id = "CMP-027", Route = OptimizationRoute.Gguf, CurrentWeight = "Q4_K_M", CurrentCache = "F16", RecommendedWeight = "Q3_K_M", RecommendedCache = "Q8_0", Quality = "Expected quality: Acceptable", Experimental = false, Strong = false },
            new { Id = "CMP-028", Route = OptimizationRoute.Gguf, CurrentWeight = "Q4_K_M", CurrentCache = "F16", RecommendedWeight = "Q4_K_M", RecommendedCache = "Q8_0", Quality = "Expected quality: Good", Experimental = false, Strong = false }
        };

        foreach (var item in expected)
        {
            CompatibilityOptimizationPresentation optimization =
                CompatibilityFixtureCatalogue.ById(item.Id)!.Presentation.Optimization!;
            CompatibilityOptimizationModePresentation selected = optimization.SelectedMode;
            Assert.AreEqual(item.Route, optimization.Route, item.Id);
            Assert.AreEqual(item.CurrentWeight, optimization.CurrentWeightFormat, item.Id);
            Assert.AreEqual(item.CurrentCache, optimization.CurrentCacheFormat, item.Id);
            Assert.AreEqual(item.RecommendedWeight, selected.WeightFormat, item.Id);
            Assert.AreEqual(item.RecommendedCache, selected.CacheFormat, item.Id);
            Assert.IsTrue(
                string.Equals(item.Quality, selected.ExpectedQualityText, StringComparison.Ordinal),
                item.Id);
            Assert.AreEqual(item.Experimental, selected.IsExperimental, item.Id);
            Assert.AreEqual(item.Strong, selected.HasStrongQualityWarning, item.Id);
        }

        Assert.IsNull(CompatibilityFixtureCatalogue.ById("CMP-030")!.Presentation.Optimization);
        Assert.IsNull(CompatibilityFixtureCatalogue.ById("CMP-040")!.Presentation.Optimization);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ConfigureLayout_RespondsToItsOwnNarrowHost()
    {
        CompatibilityPage page = CreatePage();
        Arrange(page, 680, 720);
        page.Apply(SourceBackedSafeSliderPresentation());
        InvokePrimaryClickHandler(page, Element<Button>(page, "BtnCompatibilityPrimary"));
        InvokeResponsiveLayout(page, 680d);
        page.UpdateLayout();

        Assert.AreEqual(
            Visibility.Collapsed,
            Element<FrameworkElement>(page, "CompatibilityOutcomePanel").Visibility);
        Assert.AreEqual(
            Visibility.Visible,
            Element<FrameworkElement>(page, "CompatibilityConfigurePanel").Visibility);
        Assert.AreEqual(
            Orientation.Vertical,
            Element<StackPanel>(page, "CompatibilityConfigureActions").Orientation);
        Assert.AreEqual(
            1,
            Grid.GetRow(Element<FrameworkElement>(page, "CompatibilitySelectedCacheGroup")));
        Assert.AreEqual(
            2,
            Grid.GetRow(Element<FrameworkElement>(page, "CompatibilitySelectedContextGroup")));
        Assert.AreEqual(ScrollBarVisibility.Disabled,
            Element<ScrollViewer>(page, "CompatibilityContentScroll")
                .HorizontalScrollBarVisibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ConfigureLayout_GrowsAndScrollsAtRepresentative200PercentText()
    {
        CompatibilityPage page = CreatePage();
        page.Apply(SourceBackedSafeSliderPresentation());
        InvokePrimaryClickHandler(page, Element<Button>(page, "BtnCompatibilityPrimary"));
        Arrange(page, 560, 520);
        TextBlock title = Element<TextBlock>(page, "CompatibilityConfigureHeading");
        double naturalTitleHeight = title.ActualHeight;

        foreach (TextBlock text in Descendants(page).OfType<TextBlock>())
        {
            text.FontSize *= 2d;
        }
        Arrange(page, 560, 520);

        ScrollViewer scroll = Element<ScrollViewer>(
            page, "CompatibilityContentScroll");
        Assert.AreEqual(TextWrapping.WrapWholeWords, title.TextWrapping);
        Assert.IsTrue(title.IsTextScaleFactorEnabled);
        Assert.IsGreaterThan(naturalTitleHeight * 1.5d, title.ActualHeight,
            "the centered title must grow naturally at the 200% preview scale");
        Assert.IsGreaterThan(0d, scroll.ScrollableHeight,
            "the reduced viewport must expose vertical scrolling");
        Assert.AreNotEqual(ScrollMode.Disabled, scroll.VerticalScrollMode);
        string[] clipped = Descendants(page).OfType<TextBlock>()
            .Where(text => text.ActualWidth > 0d && text.ActualHeight > 0d)
            .Where(text => text.ActualHeight + 4d < text.DesiredSize.Height)
            .Select(text => $"'{text.Text}' actual={text.ActualHeight:F1} desired={text.DesiredSize.Height:F1}")
            .ToArray();
        Assert.IsEmpty(clipped,
            "realized text must receive its desired height instead of clipping: "
            + string.Join("; ", clipped));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ConcludedPresentation_ExposesSemanticMemoryEstimate()
    {
        CompatibilityFixture? fixture = CompatibilityFixtureCatalogue.ById("CMP-010");
        Assert.IsNotNull(fixture);
        CompatibilityEstimateSummary? summary = fixture.Presentation.EstimateSummary;
        Assert.IsNotNull(summary);

        Assert.AreEqual(4_697_620_480UL, summary.ModelWeightsBytes);
        Assert.AreEqual(536_870_912UL, summary.KvCacheBytes);
        Assert.AreEqual(369_098_752UL, summary.RuntimeAndBufferBytes);
        Assert.AreEqual(560_359_014UL, summary.MarginForErrorBytes);
        Assert.AreEqual(6_163_949_158UL, summary.EstimatedPeakBytes);
        Assert.AreEqual(9_663_676_416UL, summary.SafeMemoryBytes);
        Assert.IsNull(CompatibilityPresentation.Empty.EstimateSummary);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void OnlyMemoryBlockedOutcome_TellsUserToCloseApplicationsAndTabs()
    {
        CompatibilityFixture? blockedFixture =
            CompatibilityFixtureCatalogue.ById("CMP-030");
        Assert.IsNotNull(blockedFixture);
        CompatibilityPresentation blocked = blockedFixture.Presentation;

        StringAssert.Contains(
            blocked.OutcomeDetail,
            "Close unused applications and browser tabs");
        Assert.IsTrue(blocked.Recoveries.Any(recovery =>
            recovery.Detail.Contains("browser tabs", StringComparison.Ordinal)));

        foreach (CompatibilityFixture fixture in CompatibilityFixtureCatalogue.All
            .Where(fixture => fixture.Id != "CMP-030"))
        {
            Assert.IsFalse(fixture.Presentation.OutcomeDetail.Contains(
                "Close unused applications and browser tabs",
                StringComparison.Ordinal));
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void PresentationWithoutSafeModelDisplayText_UsesSafeIdentityFallback()
    {
        CompatibilityPage page = CreatePage();
        CompatibilityFixture? fixture = CompatibilityFixtureCatalogue.ById("CMP-030");
        Assert.IsNotNull(fixture);

        page.Apply(fixture.Presentation);
        page.UpdateLayout();

        Assert.AreEqual(
            Visibility.Visible,
            Element<FrameworkElement>(page, "CompatibilityIdentityHeader").Visibility);
        Assert.AreEqual(
            "Selected model",
            Element<TextBlock>(page, "CompatibilityModelName").Text);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void MachineMemoryFacts_RenderExactValuesAndCollapseWithoutEvidence()
    {
        CompatibilityPage page = CreatePage();
        CompatibilityFixture? fixture = CompatibilityFixtureCatalogue.ById("CMP-010");
        Assert.IsNotNull(fixture);
        CompatibilityPresentation presentation = fixture.Presentation with
        {
            MachineMemory = new CompatibilityMachineMemoryPresentation(
            [
                new CompatibilityFact("Installed RAM", "16 GB", "Physical memory in this computer"),
                new CompatibilityFact("Available now", "6 GB", "Free when this check ran"),
                new CompatibilityFact("Safety reserve", "614 MB", "Kept for Windows and other applications"),
                new CompatibilityFact("Safe for this model", "5.4 GB", "Available after the safety reserve")
            ])
        };

        page.Apply(presentation);
        InvokeResponsiveLayout(page, 560d);
        page.UpdateLayout();

        Assert.AreEqual(
            Visibility.Visible,
            Element<FrameworkElement>(page, "CompatibilitySetupFacts").Visibility);
        Assert.AreEqual("16 GB", Element<TextBlock>(page, "CompatibilityInstalledRam").Text);
        Assert.AreEqual("6 GB", Element<TextBlock>(page, "CompatibilityAvailableNow").Text);
        Assert.AreEqual("614 MB", Element<TextBlock>(page, "CompatibilitySafetyReserve").Text);
        Assert.AreEqual("5.4 GB", Element<TextBlock>(page, "CompatibilitySafeForModel").Text);

        Grid facts = Element<Grid>(page, "CompatibilitySetupFacts");
        Assert.AreEqual(new GridLength(1, GridUnitType.Star), facts.ColumnDefinitions[0].Width);
        Assert.AreEqual(new GridLength(1, GridUnitType.Star), facts.ColumnDefinitions[1].Width);
        string[] labels = Descendants(facts).OfType<TextBlock>()
            .Select(text => text.Text)
            .ToArray();
        CollectionAssert.IsSubsetOf(
            new[] { "Installed RAM", "Available now", "Safety reserve", "Safe for this model" },
            labels);

        page.Apply(CompatibilityPresentation.Empty);
        Assert.AreEqual(
            Visibility.Collapsed,
            Element<FrameworkElement>(page, "CompatibilitySetupFacts").Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void MemoryBlockedOutcome_IsAnExplicitActionableWarning()
    {
        CompatibilityFixture? fixture = CompatibilityFixtureCatalogue.ById("CMP-030");
        Assert.IsNotNull(fixture);

        StringAssert.Contains(fixture.Presentation.OutcomeTitle, "Memory warning");
        StringAssert.Contains(fixture.Presentation.OutcomeDetail, "check again");
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void RuntimeRoute_UsesReadableLightThemeTextBeforeFirstLoad()
    {
        CompatibilityPage page = CreatePage();
        CompatibilityFixture? fixture = CompatibilityFixtureCatalogue.ById("CMP-030");
        Assert.IsNotNull(fixture);

        page.Apply(fixture.Presentation);
        TextBlock route = Element<TextBlock>(page, "CompatibilityRuntimeRoute");
        SolidColorBrush foreground = Assert.IsInstanceOfType<SolidColorBrush>(route.Foreground);

        Assert.AreEqual("#FF111827", foreground.Color.ToString());
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void EveryFixture_RendersWithoutThrowing()
    {
        // The page resolves theme-scoped brushes from code. A key that is
        // missing under one theme throws inside the constructor, which is a
        // crash on navigation rather than a visual defect, so it has to be
        // caught by construction rather than by inspection.
        CompatibilityPage page = CreatePage();

        foreach (CompatibilityFixture fixture in CompatibilityFixtureCatalogue.All)
        {
            page.Apply(fixture.Presentation);
            page.UpdateLayout();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ConcludedScreens_ShowTheFiguresBehindTheVerdict()
    {
        // A verdict with empty cards under it is the failure this whole design
        // exists to prevent: a confident sentence with nothing supporting it.
        CompatibilityPage page = CreatePage();

        foreach (CompatibilityFixture fixture in Concluded())
        {
            page.Apply(fixture.Presentation);
            page.UpdateLayout();

            Assert.AreEqual(
                Visibility.Visible,
                Element<FrameworkElement>(page, "CompatibilityDecisionStrip").Visibility,
                $"{fixture.Id} stated a verdict with no decision facts under it.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(
                Element<TextBlock>(page, "CompatibilityMemoryNeeded").Text));
            Assert.IsFalse(string.IsNullOrWhiteSpace(
                Element<TextBlock>(page, "CompatibilitySafeMemory").Text));
            Assert.AreEqual(
                Visibility.Visible,
                Element<FrameworkElement>(page, "CompatibilitySetupFacts").Visibility,
                $"{fixture.Id} did not identify the current setup and machine memory.");
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ConcludedScreens_DrawTheMemoryBar()
    {
        CompatibilityPage page = CreatePage();

        foreach (CompatibilityFixture fixture in Concluded())
        {
            page.Apply(fixture.Presentation);
            page.UpdateLayout();

            Assert.AreEqual(
                Visibility.Visible,
                Element<FrameworkElement>(page, "CompatibilityBudgetDiagram").Visibility,
                $"{fixture.Id} reached a verdict without showing the memory it rests on.");
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void EveryFixture_HidesResultSectionsThatHaveNoVisibleContent()
    {
        CompatibilityPage page = CreatePage();

        foreach (CompatibilityFixture fixture in CompatibilityFixtureCatalogue.All)
        {
            CompatibilityPresentation presentation = fixture.Presentation;
            page.Apply(presentation);
            page.UpdateLayout();

            bool hasDecision = presentation.MemoryClarity is not null
                || presentation.MemoryOverview is not null
                || presentation.Facts.Count > 0
                || presentation.Budget.Segments.Count > 0;
            bool hasSetup = presentation.RuntimeRows.Count > 0
                || presentation.MachineMemory?.Facts.Count > 0
                || presentation.Optimization is not null;
            bool hasRecovery = presentation.Recoveries.Count > 0
                || presentation.MemoryRecoveryReason
                    == CompatibilityMemoryRecoveryReason.SystemMemoryPressure;

            Assert.AreEqual(
                hasDecision
                    ? Visibility.Visible
                    : Visibility.Collapsed,
                Element<FrameworkElement>(page, "CompatibilityDecisionStrip").Visibility,
                $"{fixture.Id} left an empty decision strip visible.");
            Assert.AreEqual(
                hasSetup
                    ? Visibility.Visible
                    : Visibility.Collapsed,
                Element<FrameworkElement>(page, "CompatibilitySetupFacts").Visibility,
                $"{fixture.Id} left the setup facts empty.");
            Assert.AreEqual(
                hasRecovery
                    ? Visibility.Visible
                    : Visibility.Collapsed,
                Element<FrameworkElement>(page, "CompatibilityRecoveryNotice").Visibility,
                $"{fixture.Id} left an empty recovery notice visible.");
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void EvaluatedScreen_ShowsVisibleEstimatedMemoryEvidence()
    {
        CompatibilityPage page = CreatePage();
        CompatibilityFixture? fixture = CompatibilityFixtureCatalogue.ById("CMP-010");
        Assert.IsNotNull(fixture);

        page.Apply(fixture.Presentation);
        page.UpdateLayout();

        Assert.AreEqual(
            Visibility.Visible,
            Element<FrameworkElement>(page, "CompatibilityBudgetDiagram").Visibility);
        Assert.AreEqual("5.7 GB", Element<TextBlock>(page, "CompatibilityMemoryNeeded").Text);
        Assert.AreEqual("9 GB", Element<TextBlock>(page, "CompatibilitySafeMemory").Text);
        Assert.AreEqual("49%", Element<TextBlock>(page, "CompatibilityLegendWeightsValue").Text);
        Assert.AreEqual("6%", Element<TextBlock>(page, "CompatibilityLegendCacheValue").Text);
        Assert.AreEqual("4%", Element<TextBlock>(page, "CompatibilityLegendWorkingValue").Text);
        Assert.AreEqual("6%", Element<TextBlock>(page, "CompatibilityLegendMarginValue").Text);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void UnevaluatedScreen_HidesEstimatedMemoryEvidence()
    {
        CompatibilityPage page = CreatePage();

        page.Apply(CompatibilityPresentation.Empty);
        page.UpdateLayout();

        Assert.AreEqual(
            Visibility.Collapsed,
            Element<FrameworkElement>(page, "CompatibilityBudgetDiagram").Visibility);
        Assert.AreEqual(
            Visibility.Collapsed,
            Element<FrameworkElement>(page, "CompatibilityDecisionStrip").Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ScreenThatReachedNoAnswer_DrawsNoMemoryBar()
    {
        // An empty bar beside "we could not work this out" reads as a model
        // that costs nothing, which is the opposite of what it means.
        CompatibilityPage page = CreatePage();

        page.Apply(CompatibilityPresentation.Empty);
        page.UpdateLayout();

        Assert.AreEqual(
            Visibility.Collapsed,
            Element<FrameworkElement>(page, "CompatibilityBudgetDiagram").Visibility);
        Assert.AreEqual(
            false,
            Element<Expander>(page, "CompatibilityMemoryLegendExpander").IsExpanded);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void EveryFixture_LeavesTheResultActionBandVisible()
    {
        // Disabled, never removed. A button that disappears reads as an option
        // that never existed, and the user cannot tell they are blocked.
        CompatibilityPage page = CreatePage();

        foreach (CompatibilityFixture fixture in CompatibilityFixtureCatalogue.All)
        {
            page.Apply(fixture.Presentation);
            page.UpdateLayout();

            Visibility expected = fixture.Id is "CMP-001" or "CMP-002"
                    or "CMP-003" or "CMP-004"
                ? Visibility.Collapsed
                : Visibility.Visible;
            Assert.AreEqual(
                expected,
                Element<FrameworkElement>(page, "CompatibilityTerminalActionBand").Visibility,
                $"{fixture.Id} hid the result actions entirely.");
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void EveryBlockingScreen_OffersSomethingToDo()
    {
        // A problem stated without a remedy is half an answer.
        CompatibilityPage page = CreatePage();

        foreach (CompatibilityFixture fixture in CompatibilityFixtureCatalogue.All)
        {
            if (fixture.Presentation.Tone != CompatibilityOutcomeTone.Blocking)
            {
                continue;
            }

            page.Apply(fixture.Presentation);
            page.UpdateLayout();

            Assert.AreEqual(
                Visibility.Visible,
                Element<FrameworkElement>(page, "CompatibilityRecoveryNotice").Visibility,
                $"{fixture.Id} blocked the user and suggested nothing.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(
                Element<TextBlock>(page, "CompatibilityRecoveryHeading").Text));
            Assert.IsFalse(string.IsNullOrWhiteSpace(
                Element<TextBlock>(page, "CompatibilityRecoveryDetail").Text));
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void EveryFixture_SaysWhereItsFiguresCameFrom()
    {
        // The estimated-versus-tested distinction is the one claim this feature
        // must never blur, and the badge is where it is carried.
        CompatibilityPage page = CreatePage();

        foreach (CompatibilityFixture fixture in Concluded())
        {
            page.Apply(fixture.Presentation);
            page.UpdateLayout();

            Assert.AreNotEqual(
                string.Empty,
                Element<TextBlock>(page, "CompatibilityOutcomeBadgeText").Text,
                $"{fixture.Id} showed figures without saying how they were arrived at.");
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void AppliedTwice_LeavesNoEvidenceBehindFromTheFirstSnapshot()
    {
        // The page applies deltas to a tree that already exists. A card that
        // appended rather than replaced would show one screen's checks stacked
        // under another's.
        CompatibilityPage page = CreatePage();
        CompatibilityFixture fixture = Concluded().First();

        page.Apply(fixture.Presentation);
        page.UpdateLayout();
        string firstOutcome = Element<TextBlock>(page, "CompatibilityOutcomeHeading").Text;
        string firstFacts = AutomationProperties.GetName(
            Element<FrameworkElement>(page, "CompatibilityDecisionStrip"));

        page.Apply(fixture.Presentation);
        page.UpdateLayout();

        Assert.AreEqual(firstOutcome,
            Element<TextBlock>(page, "CompatibilityOutcomeHeading").Text);
        Assert.AreEqual(firstFacts, AutomationProperties.GetName(
            Element<FrameworkElement>(page, "CompatibilityDecisionStrip")));
    }

    private static CompatibilityPage CreateOpenVinoRaw3BPage(
        ulong currentlyAvailableMemoryBytes)
    {
        OpenVinoStaticPackageEvidence evidence = new(
            1,
            new string('a', 64),
            VerifiedOpenVinoOptimizationEvidence.SourceModelSha256,
            6_805_673_303,
            "granite",
            "GraniteForCausalLM",
            "text-generation-with-past",
            131_072,
            "float16",
            "PreTrainedTokenizerFast",
            10,
            true,
            40,
            4096,
            32,
            8);
        Guid hardwareRun = Guid.NewGuid();
        ModelInspectionHandoff model = new(
            ModelInspectionHandoff.CurrentSchemaVersion,
            Guid.NewGuid(),
            Guid.NewGuid(),
            ModelInspectionOutcome.Ready,
            evidence.ModelSha256,
            evidence.ModelLengthBytes);
        HardwareInspectionHandoff hardware = HardwareInspectionHandoff.Create(
            hardwareRun,
            HardwareInspectionOutcome.Completed,
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation(
                hardwareRun));
        Assert.IsTrue(OpenVinoCompatibilityInputProjector.TryPrepare(
            model, evidence, hardwareRun, hardware, out var prepared));
        var official = OpenVinoOfficialWorkerAuthority.CreateInstallation(
            System.IO.Path.GetFullPath("official-test-worker"),
            VerifiedOpenVinoOptimizationEvidence
                .PackagedOfficialWorkerManifestSha256);
        var turbo = OpenVinoTurboQuantWorkerAuthority.CreateInstallation(
            System.IO.Path.GetFullPath("turbo-test-worker"),
            VerifiedOpenVinoOptimizationEvidence
                .PackagedTurboWorkerManifestSha256,
            VerifiedOpenVinoOptimizationEvidence.TurboPatchSeriesSha256,
            VerifiedOpenVinoOptimizationEvidence.TurboRuntimeManifestSha256);
        Assert.IsTrue(OpenVinoOptimizationProductionAuthority.TryCreate(
            prepared!,
            official.ExpectedBuildEvidence,
            turbo.ExpectedBuildEvidence,
            true,
            out var authority));
        DateTimeOffset now = DateTimeOffset.UtcNow;
        CompatibilityFreshResourcesInput resources =
            CompatibilityFreshResourcesInput.Create(
                CurrentlyAvailableMemory.FromBytes(
                    currentlyAvailableMemoryBytes),
                null,
                64UL * 1024 * 1024 * 1024,
                now);
        return new CompatibilityPage(
            (consent, token) => Task.FromResult(authority!.Evaluate(
                resources, consent, now, token)),
            authority!,
            _ => null)
        {
            StartAutomatically = false
        };
    }

    private static CompatibilityExactOptimizationModePresentation
        ExactOpenVinoChoiceAt(
            CompatibilityOptimizationPresentation optimization,
            int preferenceValue) =>
        optimization.ExactSafeModes.Single(mode =>
            OpenVinoPreferenceStop(mode) == preferenceValue);

    private static int OpenVinoPreferenceStop(
        CompatibilityExactOptimizationModePresentation mode) =>
        (mode.Mode.WeightFormat, mode.Mode.CacheFormat) switch
        {
            ("INT4", "TurboQuant TBQ3") => 10,
            ("INT4", "TurboQuant TBQ4") => 30,
            ("INT4", "U4") => 50,
            ("INT4", "U8") => 70,
            ("INT8", "Automatic (OpenVINO default)") => 90,
            _ => -1
        };

    private static void AssertOpenVinoSelectionAligned(
        CompatibilityPage page,
        int expectedPreference)
    {
        Slider slider = Element<Slider>(page, "CompatibilityPreferenceSlider");
        Button start = Element<Button>(page, "BtnCompatibilityConfigurePrimary");
        CompatibilityOptimizationPresentation optimization =
            page.ViewModel.Presentation.Optimization!;
        OptimizationPreferenceSelection selected =
            page.ViewModel.SelectedPreference!;
        CompatibilityExactOptimizationModePresentation exact =
            optimization.ExactSafeModes.Single(mode => string.Equals(
                mode.CandidateIdentity,
                selected.ExactCandidateIdentity,
                StringComparison.Ordinal));
        Assert.AreEqual(expectedPreference, OpenVinoPreferenceStop(exact));
        Assert.AreEqual((double)expectedPreference, slider.Value);
        Assert.AreEqual(exact.CandidateIdentity,
            page.ViewModel.CurrentOptimizationHandoff?.Plan.Preference
                .ExactCandidateIdentity);
        Assert.AreEqual(exact.Mode.WeightFormat,
            Element<TextBlock>(page, "CompatibilitySelectedWeightFormat").Text);
        Assert.AreEqual(exact.Mode.CacheFormat,
            Element<TextBlock>(page, "CompatibilitySelectedCacheFormat").Text);
        Assert.AreEqual(exact.Mode.ContextText,
            Element<TextBlock>(page, "CompatibilitySelectedContext").Text);
        Assert.IsTrue(start.IsEnabled);
        Assert.IsTrue(page.ViewModel.StartOptimizationCommand.CanExecute(null));
    }

    /// <summary>
    /// Reaches a named control the way the other rendered-state suites do,
    /// through the name scope rather than a field, so the page needs no test
    /// affordance widening its own surface.
    /// </summary>
    private static T Element<T>(FrameworkElement root, string name)
        where T : DependencyObject =>
        Assert.IsInstanceOfType<T>(root.FindName(name));

    private static void Arrange(FrameworkElement element, double width, double height)
    {
        element.Width = width;
        element.Height = height;
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
    }

    private static void AssertBayCentered(
        FrameworkElement centeringHost,
        FrameworkElement bay,
        string fixtureId,
        double viewportWidth)
    {
        Point bayOrigin = bay.TransformToVisual(centeringHost)
            .TransformPoint(new Point());
        double bayMidpoint = bayOrigin.X + (bay.ActualWidth / 2d);
        double hostMidpoint = centeringHost.ActualWidth / 2d;
        Assert.AreEqual(hostMidpoint, bayMidpoint, 0.5,
            $"{fixtureId} was not centered in the {viewportWidth:N0}-DIP viewport.");
    }

    private static void Invoke(Button button)
    {
        ButtonAutomationPeer peer = new(button);
        IInvokeProvider provider = Assert.IsInstanceOfType<IInvokeProvider>(
            peer.GetPattern(PatternInterface.Invoke));
        provider.Invoke();
    }

    private static CompatibilityPresentation SourceBackedSafeSliderPresentation()
    {
        CompatibilityViewModelTests.RealExactPlanningFixture fixture =
            CompatibilityViewModelTests.RealExactFixture();
        return CompatibilityPresentationFactory.From(fixture.Evaluation);
    }

    private static CompatibilityEvaluation GgufFitEvaluation(bool hasOptionalQ8)
    {
        const ulong gib = 1024UL * 1024 * 1024;
        CompatibilitySetupView setup = CompatibilitySetupView.ForPresentation(
            RuntimeRouteId.LlamaCpp,
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            WeightQuantisation.Q4_K_M,
            contextTokens: 4096,
            CompatibilityFitState.Safe,
            requiredBytes: 3 * gib,
            safeBudgetBytes: 4 * gib,
            headroomBytes: gib,
            uncertaintyAllowanceBytes: 0,
            isExperimental: false,
            requiresConversion: false,
            [
                new CompatibilityComponentView(ResourceComponentKind.Weights,
                    2 * gib),
                new CompatibilityComponentView(ResourceComponentKind.KvCache,
                    gib / 2),
                new CompatibilityComponentView(
                    ResourceComponentKind.BackendAllocation, gib / 2)
            ],
            ggufKvCache: GgufKvCacheFormat.F16);
        CompatibilityScreenModel screen = CompatibilityScreenModel.ForPresentation(
            CompatibilityScreenState.EstimatedCompatible,
            [],
            [],
            BaselineExclusionReason.None,
            useCurrentModelAvailable: true,
            continueEnabled: true,
            setup: setup);
        AvailableMemorySafetyBudget budget = SystemMemoryBudgetCalculator.Calculate(
            CurrentlyAvailableMemory.FromBytes((9 * gib) / 2));
        return new CompatibilityEvaluation(screen, null, null)
        {
            MachineMemory = CompatibilityMachineMemory.Create(
                TotalPhysicalMemory.FromBytes(16 * gib),
                budget.Available,
                budget.Reserve,
                budget.Executable),
            OptionalOptimization = hasOptionalQ8 ? GgufQ8Optimization() : null
        };
    }

    private static CompatibilityOptimizationView GgufQ8Optimization()
    {
        CompatibilityOptimizationLabelCode[] labels =
        [
            CompatibilityOptimizationLabelCode.Automatic,
            CompatibilityOptimizationLabelCode.MaximumEfficiency,
            CompatibilityOptimizationLabelCode.Efficient,
            CompatibilityOptimizationLabelCode.Balanced,
            CompatibilityOptimizationLabelCode.HighCapability,
            CompatibilityOptimizationLabelCode.MaximumCapability
        ];
        int?[] sliders = [null, 10, 30, 50, 70, 90];
        CompatibilityOptimizationModeView[] modes = labels
            .Select((label, index) => CompatibilityOptimizationModeView.ForPresentation(
                label,
                sliders[index],
                OptimizationRoute.Gguf,
                GgufWeightFormat.Q4KM,
                GgufKvCacheFormat.Q8_0,
                null,
                null,
                DeviceRouteId.Cpu,
                OptimizationAssessment.Good,
                contextTokens: 4096,
                predictedPeakBytes: 2_684_354_560,
                safeBudgetBytes: 4_294_967_296,
                headroomBytes: 1_610_612_736,
                requiresPersistentArtifact: false,
                requiresRequantisationAcknowledgement: false,
                qualityNotice: OptimizationQualityNotice.None,
                isExperimental: false,
                sharedWithAdjacentBand: false))
            .ToArray();
        CompatibilityExactOptimizationModeView safe =
            Assert.IsInstanceOfType<CompatibilityExactOptimizationModeView>(
                typeof(CompatibilityExactOptimizationModeView)
                    .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                    .Single(constructor => constructor.GetParameters() is
                        [{ ParameterType: var identityType },
                         { ParameterType: var modeType }]
                        && identityType == typeof(string)
                        && modeType == typeof(CompatibilityOptimizationModeView))
                    .Invoke([new string('a', 64), modes[0]]));
        return Assert.IsInstanceOfType<CompatibilityOptimizationView>(
            typeof(CompatibilityOptimizationView)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(constructor => constructor.GetParameters().Length == 10)
                .Invoke([
                    CompatibilityOptimizationLabelCode.Automatic,
                    null,
                    modes,
                    false,
                    false,
                    OptimizationQualityNotice.None,
                    new[] { safe },
                    false,
                    new[] { safe },
                    0
                ]));
    }

    private static void InvokePrimaryClickHandler(
        CompatibilityPage page,
        Button sender)
    {
        MethodInfo? method = typeof(CompatibilityPage).GetMethod(
            "CompatibilityPrimary_Click",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        method.Invoke(page, new object[] { sender, new RoutedEventArgs() });
    }

    private static CompatibilityScreenModel AuthoritativeEstimatedCompatibleScreen(
        CompatibilitySetupView current)
    {
        ConstructorInfo constructor = typeof(CompatibilityScreenModel)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.GetParameters().Length == 11);
        return (CompatibilityScreenModel)constructor.Invoke(
        [
            CompatibilityScreenState.EstimatedCompatible,
            Array.Empty<CompatibilityFindingView>(),
            Array.Empty<CompatibilityModeView>(),
            BaselineExclusionReason.None,
            true,
            true,
            current,
            null,
            true,
            null,
            null
        ]);
    }

    private sealed class ChatEnabledAuthority(
        ICompatibilityActionAuthority inner)
        : ICompatibilityActionAuthority
    {
        public bool TryGetOptimizationAuthority(
            OptimizationRoute route,
            out IOptimizationExecutionPayloadComposer? composer,
            out OptimizationIssuanceAuthority? issuanceAuthority) =>
            inner.TryGetOptimizationAuthority(
                route, out composer, out issuanceAuthority);

        public bool IsCurrentModelChatAvailable(OptimizationRoute route) => true;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.IsTrue(DateTime.UtcNow < deadline, "Timed out waiting for the UI action.");
            await Task.Delay(10);
        }
    }

    private static Task InvokeLifecycleAsync(CompatibilityPage page, string methodName)
    {
        MethodInfo? method = typeof(CompatibilityPage).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        return Assert.IsInstanceOfType<Task>(method.Invoke(page, null));
    }

    private static void InvokeLifecycle(CompatibilityPage page, string methodName)
    {
        MethodInfo? method = typeof(CompatibilityPage).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        method.Invoke(page, null);
    }

    private static void InvokeResponsiveLayout(CompatibilityPage page, double width)
    {
        MethodInfo? method = typeof(CompatibilityPage).GetMethod(
            "ApplyConfigureResponsiveLayout",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        method.Invoke(page, new object[] { width });
        page.UpdateLayout();
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (DependencyObject descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static IEnumerable<CompatibilityFixture> Concluded() =>
        CompatibilityFixtureCatalogue.All.Where(fixture =>
            fixture.Presentation.Facts.Count > 0);

    private static CompatibilityPage CreatePage()
    {
        return new CompatibilityPage { StartAutomatically = false };
    }

    private static bool UnreachableBinder(
        CompatibilityFreshResourcesInput fresh,
        out CompatibilityProductionInput? input)
    {
        input = null;
        Assert.Fail("Typed unavailability must not invoke the production binder.");
        return false;
    }

    private sealed class UnavailableFreshSource : ICompatibilityFreshResourcesSource
    {
        internal int CaptureCount { get; private set; }

        public ValueTask<CompatibilityFreshResourcesInput> CaptureAsync(
            CancellationToken cancellationToken)
        {
            CaptureCount++;
            return ValueTask.FromException<CompatibilityFreshResourcesInput>(
                new CompatibilityFreshResourcesUnavailableException(
                    CompatibilityFreshResourcesUnavailableReason.StorageUnavailable));
        }
    }
}
#endif
