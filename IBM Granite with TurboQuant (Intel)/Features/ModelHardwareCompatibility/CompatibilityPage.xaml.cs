using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.ApplicationFaults;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.State;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Contracts;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using VirtualKey = Windows.System.VirtualKey;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility;

internal sealed partial class CompatibilityPage : Page
{
    private CompatibilityPresentation _presentation = CompatibilityPresentation.Empty;
    private bool _isActive;
    private bool _applyingOptimization;
    private readonly IApplicationFaultReporter _faultReporter;
    private int _unexpectedFaultReported;
    private bool _markerLayoutLoaded;
    private bool _markerLayoutPending;
    private bool _configureStageVisible;
    private long _markerEvidenceRevision;
    private double? _availableMarkerRatio;
    private double? _minimumMarkerRatio;
    private string[] _safeSliderChoiceIdentities = [];
    private int[] _openVinoAvailablePreferenceStops = [];
    private int _openVinoRequestedPreference = 50;
    private bool _safeSliderSelectionSynchronized;
    private bool _exactSelectionSynchronized;
    private bool _experimentalConsentPending;
    private long _exactConsentRevision;
    private int _importNavigationPending;
    private bool _hardwareRestartPending;

    private static readonly int[] OpenVinoPreferenceStops = [10, 30, 50, 70, 90];

    private readonly record struct OpenVinoSliderChoice(
        int PreferenceValue,
        CompatibilityExactOptimizationModePresentation Candidate);

    public CompatibilityPage() : this(new ViewModels.CompatibilityViewModel()) { }

    internal CompatibilityPage(
        Func<CancellationToken, Task<CompatibilityScreenModel>> evaluator,
        bool continueDestinationAvailable = true,
        IApplicationFaultReporter? faultReporter = null)
        : this(new ViewModels.CompatibilityViewModel(evaluator, continueDestinationAvailable), faultReporter) { }

    internal CompatibilityPage(
        Func<IReadOnlySet<string>, CancellationToken, Task<CompatibilityEvaluation>> evaluator,
        ICompatibilityActionAuthority actionAuthority,
        Func<CompatibilityEvaluation, CurrentModelLaunchHandoff?> currentModelHandoffResolver,
        bool continueDestinationAvailable = true,
        TimeProvider? timeProvider = null)
        : this(new ViewModels.CompatibilityViewModel(
            evaluator, continueDestinationAvailable,
            actionAuthority: actionAuthority,
            currentModelHandoffResolver: currentModelHandoffResolver,
            timeProvider: timeProvider)) { }

    private CompatibilityPage(ViewModels.CompatibilityViewModel viewModel,
        IApplicationFaultReporter? faultReporter = null)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _faultReporter = faultReporter ?? BoundedApplicationFaultReporter.Shared;
        InitializeComponent();

        BtnCancelCompatibility.Command = ViewModel.CancelCommand;
        BtnCompatibilityPrimary.Command = ViewModel.ContinueCommand;
        BtnCompatibilityConfigurePrimary.Command = ViewModel.StartOptimizationCommand;
        BtnCompatibilityConfigureBack.Click += CompatibilityConfigureBack_Click;
        BtnCompatibilitySecondaryForward.Command = ViewModel.OptionalOptimizationCommand;
        BtnCompatibilityPrimary.Click += CompatibilityPrimary_Click;
        BtnCompatibilitySecondaryForward.Click += CompatibilityPrimary_Click;
        CompatibilityPreferenceSlider.ValueChanged += PreferenceSlider_ValueChanged;
        CompatibilityPreferenceSlider.PreviewKeyDown +=
            PreferenceSlider_PreviewKeyDown;
        AutomationProperties.SetName(CompatibilityPreferenceSlider,
            "Safe setup");
        AutomationProperties.SetHelpText(CompatibilityPreferenceSlider,
            "Choose a released safe setup. Stops run from lower to higher estimated RAM; measured quality is shown separately.");

        ViewModel.PresentationChanged += (_, value) => Apply(value);
        ViewModel.ContinueRequested += (_, _) => ContinueRequested?.Invoke(this, EventArgs.Empty);
        ViewModel.OptimizationRequested += (_, args) => OptimizationRequested?.Invoke(this, args);
        ViewModel.CurrentModelChatRequested += (_, args) => CurrentModelChatRequested?.Invoke(this, args);
        ViewModel.BackRequested += (_, _) => BackRequested?.Invoke(this, EventArgs.Empty);
        ViewModel.AuxiliaryStatusChanged += (_, status) => ApplyAuxiliaryStatus(status);
        Loaded += Page_Loaded;
        Unloaded += Page_Unloaded;
        CompatibilityAvailableRamLabel.SizeChanged += MarkerLabel_SizeChanged;
        CompatibilityMinimumRamLabel.SizeChanged += MarkerLabel_SizeChanged;
        Apply(CompatibilityPresentation.Empty);
    }

    internal event EventHandler? ContinueRequested;
    internal event EventHandler<OptimizationRequestedEventArgs>? OptimizationRequested;
    internal event EventHandler<CurrentModelChatRequestedEventArgs>? CurrentModelChatRequested;
    internal event EventHandler? BackRequested;
    internal event EventHandler? HardwareRetryRequested;
    internal event EventHandler? ConfigureStageEntered;
    internal event EventHandler? ConfigureStageExited;
    internal event EventHandler? ImportAnotherModelRequested;
    internal bool StartAutomatically { get; set; } = true;
    internal ViewModels.CompatibilityViewModel ViewModel { get; }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _markerLayoutLoaded = true;
        RequestMarkerLayout();
        await ActivateAsync();
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        _markerLayoutLoaded = false;
        _markerLayoutPending = false;
        checked { _markerEvidenceRevision++; }
        _openVinoRequestedPreference = 50;
        Deactivate();
    }

    private async Task ActivateAsync()
    {
        if (_isActive) return;
        _isActive = true;
        if (!StartAutomatically) return;
        try { await ViewModel.StartAsync(); }
        catch (Exception exception)
        {
            if (Interlocked.Exchange(ref _unexpectedFaultReported, 1) == 0)
                _faultReporter.Report(ApplicationFault.FromException(
                    ApplicationFaultCode.CompatibilityEvaluationUnexpected, exception));
        }
    }

    private void Deactivate()
    {
        if (!_isActive) return;
        _isActive = false;
        checked { _exactConsentRevision++; }
        _experimentalConsentPending = false;
        ViewModel.RetireAttempt();
    }

    internal void Apply(CompatibilityPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        _presentation = presentation;
        if (presentation.ForwardActionKind !=
            CompatibilityForwardActionKind.ImportAnotherModel)
        {
            Interlocked.Exchange(ref _importNavigationPending, 0);
        }
        CompatibilityModelName.Text = string.IsNullOrWhiteSpace(presentation.ModelName)
            ? "Selected model" : presentation.ModelName;
        CompatibilityModelDetail.Text = ResolveModelDetail(presentation);
        CompatibilityModelDetail.Visibility = string.IsNullOrWhiteSpace(CompatibilityModelDetail.Text)
            ? Visibility.Collapsed : Visibility.Visible;

        bool analysing = ApplyAnalysisProgress(presentation);
        CompatibilityAnalysisPanel.Visibility = analysing ? Visibility.Visible : Visibility.Collapsed;
        CompatibilityOutcomePanel.Visibility = analysing ? Visibility.Collapsed : Visibility.Visible;
        ApplyIdentityStatus(presentation, analysing);
        if (analysing)
        {
            ExitConfigureStage(restoreFocus: false);
            ProjectVisibleStage(analysing: true);
            CompatibilityMemoryClarityShortfall.Text = string.Empty;
            AutomationProperties.SetName(
                CompatibilityDecisionStrip,
                "Compatibility decision facts");
            ApplyBudget(CompatibilityPresentation.Empty);
            return;
        }

        ApplyOutcome(presentation);
        ApplyFacts(presentation);
        ApplyBudget(presentation);
        ApplySetup(presentation);
        ApplyOptimization(presentation.Optimization);
        ApplyRecovery(presentation);
        CompatibilityCalculationDetail.Text = presentation.DisclosureDetail;
        CompatibilityCalculationExpander.Visibility =
            string.IsNullOrWhiteSpace(presentation.DisclosureDetail)
                ? Visibility.Collapsed : Visibility.Visible;
        ApplyActions(presentation);
        if (_configureStageVisible
            && !CanDisplayConfigureStage(presentation))
        {
            ExitConfigureStage(restoreFocus: false);
        }
        ProjectVisibleStage(analysing: false);
    }

    private static string ResolveModelDetail(CompatibilityPresentation presentation)
    {
        if (!string.IsNullOrWhiteSpace(presentation.ModelDetail)) return presentation.ModelDetail;
        CompatibilityRow? file = presentation.RuntimeRows.FirstOrDefault(
            row => string.Equals(row.Title, "Model file", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(file?.Subtitle)) return file.Subtitle;
        return presentation.Optimization is { } o
            ? $"{o.CurrentWeightFormat} · {o.CurrentCacheFormat}" : string.Empty;
    }

    private bool ApplyAnalysisProgress(CompatibilityPresentation presentation)
    {
        string[] titles = [
            "Working out what this model needs", "Checking the ways it could run",
            "Checking memory and safety limits", "Picking the safest setup"];
        string[] details = [
            "Reading the inspected model facts needed for a safe memory estimate.",
            "Comparing supported runtime and device routes for this package.",
            "Checking predicted peak memory against the current safe budget.",
            "Choosing the safest admitted setup from the available routes."];
        int activeIndex = Array.IndexOf(titles, presentation.OutcomeTitle);
        bool activeState = presentation.SecondaryActionKind == CompatibilitySecondaryActionKind.Cancel
            && activeIndex >= 0;
        if (!activeState) return false;

        CompatibilityProgressHeading.Text = titles[activeIndex];
        CompatibilityProgressDetail.Text = details[activeIndex];
        CompatibilityProgressCount.Text = $"{activeIndex} of 4 checks complete";
        Border[] surfaces = [CompatibilityStage1GlyphSurface, CompatibilityStage2GlyphSurface,
            CompatibilityStage3GlyphSurface, CompatibilityStage4GlyphSurface];
        TextBlock[] numbers = [CompatibilityStage1Number, CompatibilityStage2Number,
            CompatibilityStage3Number, CompatibilityStage4Number];
        FontIcon[] glyphs = [CompatibilityStage1Glyph, CompatibilityStage2Glyph,
            CompatibilityStage3Glyph, CompatibilityStage4Glyph];
        Grid[] orbits = [CompatibilityStage1OrbitPresenter, CompatibilityStage2OrbitPresenter,
            CompatibilityStage3OrbitPresenter, CompatibilityStage4OrbitPresenter];
        TextBlock[] statuses = [CompatibilityStage1Status, CompatibilityStage2Status,
            CompatibilityStage3Status, CompatibilityStage4Status];
        FrameworkElement[] rows = [CompatibilityStage1, CompatibilityStage2,
            CompatibilityStage3, CompatibilityStage4];

        for (int i = 0; i < 4; i++)
        {
            bool complete = i < activeIndex;
            bool current = i == activeIndex;
            string status = complete ? "Complete" : current ? "Active" : "Waiting";
            string surface = complete ? "CompatibilitySuccessSurfaceBrush" :
                current ? "CompatibilityAccentSurfaceBrush" : "CompatibilityWaitingSurfaceBrush";
            string border = complete ? "CompatibilitySuccessBorderBrush" :
                current ? "CompatibilityAccentBorderBrush" : "CompatibilityBorderBrush";
            string accent = complete ? "CompatibilitySuccessBrush" :
                current ? "CompatibilityAccentBrush" : "CompatibilityWaitingTextBrush";
            surfaces[i].Background = Brush(surface);
            surfaces[i].BorderBrush = Brush(border);
            numbers[i].Foreground = Brush(accent);
            numbers[i].Visibility = complete || current ? Visibility.Collapsed : Visibility.Visible;
            glyphs[i].Foreground = Brush(accent);
            glyphs[i].Visibility = complete ? Visibility.Visible : Visibility.Collapsed;
            orbits[i].Visibility = current ? Visibility.Visible : Visibility.Collapsed;
            statuses[i].Text = status;
            statuses[i].Foreground = Brush(accent);
            AutomationProperties.SetName(rows[i], $"{titles[i]}, step {i + 1} of 4, {status}");
            AutomationProperties.SetItemStatus(rows[i], status);
        }
        return true;
    }

    private void ApplyIdentityStatus(CompatibilityPresentation presentation, bool analysing)
    {
        var colors = Tone(analysing ? CompatibilityOutcomeTone.Neutral : presentation.Tone);
        CompatibilityIdentityStatus.Background = colors.surface;
        CompatibilityIdentityStatus.BorderBrush = colors.border;
        CompatibilityIdentityStatusText.Foreground = colors.accent;
        CompatibilityIdentityStatusText.Text = analysing ? "Analysing" :
            string.IsNullOrWhiteSpace(presentation.OutcomeBadge)
                ? presentation.OutcomeTitle : presentation.OutcomeBadge;
        CompatibilityIdentityStatus.Visibility = Visibility.Visible;
        AutomationProperties.SetName(CompatibilityIdentityStatus,
            CompatibilityIdentityStatusText.Text);
    }

    private void ApplyOutcome(CompatibilityPresentation presentation)
    {
        var colors = Tone(presentation.Tone);
        CompatibilityOutcomeGlyphSurface.Background = colors.surface;
        CompatibilityOutcomeGlyphSurface.BorderBrush = colors.border;
        CompatibilityOutcomeGlyph.Foreground = colors.accent;
        CompatibilityOutcomeGlyph.Glyph = presentation.Tone switch
        {
            CompatibilityOutcomeTone.Positive => "\uE73E",
            CompatibilityOutcomeTone.Caution => "\uE7BA",
            CompatibilityOutcomeTone.Blocking => "\uE711",
            _ => "\uE946"
        };
        CompatibilityOutcomeHeading.Text = presentation.OutcomeTitle;
        CompatibilityOutcomeDetail.Text = presentation.OutcomeDetail;
        CompatibilityOutcomeBadge.Background = colors.surface;
        CompatibilityOutcomeBadge.BorderBrush = colors.border;
        CompatibilityOutcomeBadgeText.Foreground = colors.accent;
        CompatibilityOutcomeBadgeText.Text = presentation.OutcomeBadge;
        CompatibilityOutcomeBadge.Visibility = string.IsNullOrWhiteSpace(presentation.OutcomeBadge)
            ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ApplyFacts(CompatibilityPresentation presentation)
    {
        if (presentation.MemoryClarity is { } clarity)
        {
            CompatibilityMemoryNeededLabel.Text = "FREE RAM NOW";
            CompatibilityMemoryNeeded.Text = CompatibilityBudget.Describe(
                clarity.AvailableSystemMemoryBytes);
            CompatibilitySafeMemoryLabel.Text = "RAM AVAILABLE TO MODEL";
            CompatibilitySafeMemory.Text = CompatibilityBudget.Describe(
                clarity.SafeModelBudgetBytes);
            CompatibilitySpareLabel.Text = "CURRENT SETUP PEAK RAM";
            CompatibilitySpareValue.Text = CompatibilityBudget.Describe(
                clarity.CurrentRequiredBytes);
            CompatibilityContextLabel.Text = "MINIMUM FREE RAM TO OPTIMISE";
            CompatibilityContextValue.Text = CompatibilityBudget.Describe(
                clarity.MinimumFreeToOptimizeBytes);
            CompatibilityMemoryClarityShortfall.Text =
                presentation.StorageShortage is { } storageShortage
                    ? "Additional storage needed: "
                        + CompatibilityBudget.Describe(
                            storageShortage.AdditionalRequiredBytes)
                        + "."
                    : "Additional free RAM needed: "
                        + CompatibilityBudget.Describe(
                            clarity.AdditionalFreeRequiredBytes)
                        + ".";
            CompatibilityMemoryClarityShortfall.Visibility = Visibility.Visible;
            AutomationProperties.SetName(
                CompatibilityDecisionStrip,
                "Memory facts. Free RAM now " + CompatibilityMemoryNeeded.Text
                + ". RAM available to model " + CompatibilitySafeMemory.Text
                + ". Current setup peak RAM " + CompatibilitySpareValue.Text
                + ". Minimum free RAM to optimise " + CompatibilityContextValue.Text
                + ". " + CompatibilityMemoryClarityShortfall.Text);
            CompatibilityDecisionStrip.Visibility = Visibility.Visible;
            return;
        }

        if (presentation.MemoryOverview is { } overview)
        {
            CompatibilityMemoryNeededLabel.Text = "FREE RAM NOW";
            CompatibilityMemoryNeeded.Text = CompatibilityBudget.Describe(
                overview.AvailableSystemMemoryBytes);
            CompatibilitySafeMemoryLabel.Text = "RAM AVAILABLE TO MODEL";
            CompatibilitySafeMemory.Text = CompatibilityBudget.Describe(
                overview.SafeModelBudgetBytes);
            CompatibilitySpareLabel.Text = "CURRENT SETUP PEAK RAM";
            CompatibilitySpareValue.Text = CompatibilityBudget.Describe(
                overview.CurrentRequiredBytes);
            if (overview.MinimumRequiredBytes is ulong minimum
                && !string.IsNullOrWhiteSpace(overview.MinimumRequirementLabel))
            {
                CompatibilityContextLabel.Text =
                    overview.MinimumRequirementLabel.ToUpperInvariant();
                CompatibilityContextValue.Text = CompatibilityBudget.Describe(minimum);
            }
            else
            {
                bool reservedMinimum = !string.IsNullOrWhiteSpace(
                    overview.MinimumRequirementLabel);
                CompatibilityContextLabel.Text = reservedMinimum
                    ? overview.MinimumRequirementLabel!.ToUpperInvariant()
                    : "CONTEXT";
                CompatibilityContextValue.Text = reservedMinimum
                    ? "Not available"
                    : $"{overview.ContextTokens:N0} tokens";
            }
            CompatibilityMemoryClarityShortfall.Text = string.Empty;
            CompatibilityMemoryClarityShortfall.Visibility = Visibility.Collapsed;
            AutomationProperties.SetName(
                CompatibilityDecisionStrip,
                "Memory facts. Free RAM now " + CompatibilityMemoryNeeded.Text
                + ". RAM available to model " + CompatibilitySafeMemory.Text
                + ". Current setup peak RAM " + CompatibilitySpareValue.Text
                + ". " + CompatibilityContextLabel.Text + " "
                + CompatibilityContextValue.Text + ".");
            CompatibilityDecisionStrip.Visibility = Visibility.Visible;
            return;
        }

        CompatibilityMemoryNeededLabel.Text = "MEMORY NEEDED";
        CompatibilityMemoryNeeded.Text = FindFact(presentation.Facts, "Memory needed")
            ?? CompatibilityBudget.Describe(presentation.Budget.RequiredBytes);
        CompatibilitySafeMemoryLabel.Text = "SAFE MEMORY";
        CompatibilitySafeMemory.Text = FindFact(presentation.Facts, "Memory allowed")
            ?? CompatibilityBudget.Describe(presentation.Budget.SafeLimitBytes);
        ulong spare = presentation.Budget.Fits
            ? presentation.Budget.SafeLimitBytes >= presentation.Budget.RequiredBytes
                ? presentation.Budget.SafeLimitBytes - presentation.Budget.RequiredBytes
                : 0
            : presentation.Budget.RequiredBytes >= presentation.Budget.SafeLimitBytes
                ? presentation.Budget.RequiredBytes - presentation.Budget.SafeLimitBytes
                : 0;
        CompatibilitySpareLabel.Text = presentation.Budget.Fits ? "SPARE" : "OVER SAFE LIMIT";
        CompatibilitySpareValue.Text = CompatibilityBudget.Describe(spare);
        CompatibilityContextLabel.Text = "CONTEXT";
        CompatibilityContextValue.Text = FindFact(presentation.Facts, "Context") ?? "Not reported";
        CompatibilityMemoryClarityShortfall.Visibility = Visibility.Collapsed;
        AutomationProperties.SetName(
            CompatibilityDecisionStrip,
            "Compatibility decision facts");
        CompatibilityDecisionStrip.Visibility = presentation.Facts.Count > 0 ||
            presentation.Budget.Segments.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyBudget(CompatibilityPresentation presentation)
    {
        CompatibilityBudget b = presentation.Budget;
        bool shown = b.Segments.Count > 0 || presentation.EstimateSummary is not null;
        CompatibilityBudgetDiagram.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
        CompatibilityBudgetSummary.Text = string.Empty;
        CompatibilityBudgetSummary.Visibility = Visibility.Collapsed;
        CompatibilityAvailableRamLabel.Text = string.Empty;
        CompatibilityAvailableRamLabel.Visibility = Visibility.Collapsed;
        CompatibilityMinimumRamLabel.Text = string.Empty;
        CompatibilityMinimumRamLabel.Visibility = Visibility.Collapsed;
        CompatibilityAvailableRamMarker.Visibility = Visibility.Collapsed;
        CompatibilityMinimumRamMarker.Visibility = Visibility.Collapsed;
        CompatibilityAvailableRamElbow.Visibility = Visibility.Collapsed;
        CompatibilityMinimumRamElbow.Visibility = Visibility.Collapsed;
        CompatibilityAvailableRamMarkerBeforeColumn.Width = new GridLength(1, GridUnitType.Star);
        CompatibilityAvailableRamMarkerAfterColumn.Width = new GridLength(0, GridUnitType.Star);
        CompatibilityMinimumRamMarkerBeforeColumn.Width = new GridLength(1, GridUnitType.Star);
        CompatibilityMinimumRamMarkerAfterColumn.Width = new GridLength(0, GridUnitType.Star);
        CompatibilityBudgetScaleContentColumn.Width = new GridLength(1, GridUnitType.Star);
        CompatibilityBudgetScaleRemainderColumn.Width = new GridLength(0, GridUnitType.Star);
        CompatibilityAvailableRamLabelTransform.X = 0;
        CompatibilityMinimumRamLabelTransform.X = 0;
        AutomationProperties.SetName(
            CompatibilityBudgetDiagram,
            "Estimated memory budget");
        _availableMarkerRatio = null;
        _minimumMarkerRatio = null;
        checked { _markerEvidenceRevision++; }
        if (!shown) return;
        ulong diagramScale = 0;
        if (presentation.MemoryClarity is { } clarity)
        {
            string required = CompatibilityBudget.Describe(
                clarity.CurrentRequiredBytes);
            string safe = CompatibilityBudget.Describe(
                clarity.SafeModelBudgetBytes);
            string minimum = CompatibilityBudget.Describe(
                clarity.SmallestOptimizedRequiredBytes);
            CompatibilityAvailableRamLabel.Text =
                $"RAM available to the model now · {safe}";
            CompatibilityAvailableRamLabel.Visibility = Visibility.Visible;
            CompatibilityMinimumRamLabel.Text =
                $"Smallest evaluated setup needs · {minimum}";
            CompatibilityMinimumRamLabel.Visibility = Visibility.Visible;
            AutomationProperties.SetName(
                CompatibilityBudgetDiagram,
                "Estimated memory budget. Current setup peak RAM " + required
                + ". RAM available to the model now " + safe
                + ". Smallest evaluated setup needs " + minimum + ".");

            diagramScale = Math.Max(
                clarity.CurrentRequiredBytes,
                Math.Max(
                    clarity.SafeModelBudgetBytes,
                    clarity.SmallestOptimizedRequiredBytes));
            if (diagramScale > 0)
            {
                double availableRatio = Math.Clamp(
                    (double)clarity.SafeModelBudgetBytes / diagramScale,
                    0d,
                    1d);
                CompatibilityAvailableRamMarkerBeforeColumn.Width =
                    new GridLength(availableRatio, GridUnitType.Star);
                CompatibilityAvailableRamMarkerAfterColumn.Width =
                    new GridLength(1d - availableRatio, GridUnitType.Star);
                CompatibilityAvailableRamMarker.Visibility = Visibility.Visible;
                _availableMarkerRatio = availableRatio;

                double minimumRatio = Math.Clamp(
                    (double)clarity.SmallestOptimizedRequiredBytes / diagramScale,
                    0d,
                    1d);
                CompatibilityMinimumRamMarkerBeforeColumn.Width =
                    new GridLength(minimumRatio, GridUnitType.Star);
                CompatibilityMinimumRamMarkerAfterColumn.Width =
                    new GridLength(1d - minimumRatio, GridUnitType.Star);
                CompatibilityMinimumRamMarker.Visibility = Visibility.Visible;
                _minimumMarkerRatio = minimumRatio;
            }
        }
        else if (presentation.MemoryOverview is { } overview)
        {
            string required = CompatibilityBudget.Describe(
                overview.CurrentRequiredBytes);
            string safe = CompatibilityBudget.Describe(
                overview.SafeModelBudgetBytes);
            CompatibilityAvailableRamLabel.Text =
                $"RAM available to the model now · {safe}";
            CompatibilityAvailableRamLabel.Visibility = Visibility.Visible;
            AutomationProperties.SetName(
                CompatibilityBudgetDiagram,
                "Estimated memory budget. Current setup peak RAM " + required
                + ". RAM available to the model now " + safe + ".");

            diagramScale = Math.Max(
                overview.CurrentRequiredBytes,
                overview.SafeModelBudgetBytes);
            if (overview.MinimumRequiredBytes is ulong minimum
                && !string.IsNullOrWhiteSpace(overview.MinimumRequirementLabel))
            {
                diagramScale = Math.Max(diagramScale, minimum);
                CompatibilityMinimumRamLabel.Text =
                    $"{overview.MinimumRequirementLabel} · "
                    + CompatibilityBudget.Describe(minimum);
                CompatibilityMinimumRamLabel.Visibility = Visibility.Visible;
                AutomationProperties.SetName(
                    CompatibilityBudgetDiagram,
                    AutomationProperties.GetName(CompatibilityBudgetDiagram)
                    + " " + overview.MinimumRequirementLabel + " "
                    + CompatibilityBudget.Describe(minimum) + ".");
            }

            if (diagramScale > 0)
            {
                double availableRatio = Math.Clamp(
                    (double)overview.SafeModelBudgetBytes / diagramScale,
                    0d,
                    1d);
                CompatibilityAvailableRamMarkerBeforeColumn.Width =
                    new GridLength(availableRatio, GridUnitType.Star);
                CompatibilityAvailableRamMarkerAfterColumn.Width =
                    new GridLength(1d - availableRatio, GridUnitType.Star);
                CompatibilityAvailableRamMarker.Visibility = Visibility.Visible;
                _availableMarkerRatio = availableRatio;

                if (overview.MinimumRequiredBytes is ulong markerMinimum
                    && !string.IsNullOrWhiteSpace(
                        overview.MinimumRequirementLabel))
                {
                    double minimumRatio = Math.Clamp(
                        (double)markerMinimum / diagramScale, 0d, 1d);
                    CompatibilityMinimumRamMarkerBeforeColumn.Width =
                        new GridLength(minimumRatio, GridUnitType.Star);
                    CompatibilityMinimumRamMarkerAfterColumn.Width =
                        new GridLength(1d - minimumRatio, GridUnitType.Star);
                    CompatibilityMinimumRamMarker.Visibility = Visibility.Visible;
                    _minimumMarkerRatio = minimumRatio;
                }
            }
        }
        else
        {
            CompatibilityBudgetSummary.Text =
                $"{CompatibilityBudget.Describe(b.RequiredBytes)} of {CompatibilityBudget.Describe(b.SafeLimitBytes)} safe memory";
            CompatibilityBudgetSummary.Visibility = Visibility.Visible;
            AutomationProperties.SetName(
                CompatibilityBudgetDiagram,
                "Estimated memory budget");
        }
        ulong weights = presentation.EstimateSummary?.ModelWeightsBytes ?? Segment(b, 0);
        ulong cache = presentation.EstimateSummary?.KvCacheBytes ?? Segment(b, 1);
        ulong working = presentation.EstimateSummary?.RuntimeAndBufferBytes ?? Segment(b, 2);
        ulong margin = presentation.EstimateSummary?.MarginForErrorBytes ?? Segment(b, 3);
        ulong spare = b.SafeLimitBytes > b.RequiredBytes ? b.SafeLimitBytes - b.RequiredBytes : 0;
        ulong representedScale = Math.Max(b.RequiredBytes, b.SafeLimitBytes);
        if (diagramScale == 0)
        {
            diagramScale = Math.Max(representedScale, 1UL);
        }
        representedScale = Math.Min(representedScale, diagramScale);
        CompatibilityBudgetScaleContentColumn.Width =
            new GridLength(representedScale, GridUnitType.Star);
        CompatibilityBudgetScaleRemainderColumn.Width =
            new GridLength(diagramScale - representedScale, GridUnitType.Star);
        SetColumn(CompatibilityBudgetWeightsColumn, weights);
        SetColumn(CompatibilityBudgetCacheColumn, cache);
        SetColumn(CompatibilityBudgetWorkingColumn, working);
        SetColumn(CompatibilityBudgetMarginColumn, margin);
        SetColumn(CompatibilityBudgetSpareColumn, spare);
        RequestMarkerLayout();
        CompatibilityLegendWeightsValue.Text = Percent(weights, diagramScale);
        CompatibilityLegendCacheValue.Text = Percent(cache, diagramScale);
        CompatibilityLegendWorkingValue.Text = Percent(working, diagramScale);
        CompatibilityLegendMarginValue.Text = Percent(margin, diagramScale);
        CompatibilityLegendSpareValue.Text = Percent(spare, diagramScale);
    }

    private static ulong Segment(CompatibilityBudget b, int i) =>
        i < b.Segments.Count ? b.Segments[i].Bytes : 0;
    private static void SetColumn(ColumnDefinition c, ulong value) =>
        c.Width = new GridLength(Math.Max(value, 1), GridUnitType.Star);
    private static string Percent(ulong value, ulong total) =>
        $"{Math.Round(value * 100d / total):0}%";
    private static ulong SaturatingAdd(ulong left, ulong right) =>
        left > ulong.MaxValue - right ? ulong.MaxValue : left + right;

    private void ApplySetup(CompatibilityPresentation presentation)
    {
        CompatibilityOptimizationPresentation? o = presentation.Optimization;
        CompatibilityCurrentWeights.Text = o?.CurrentWeightFormat
            ?? FindRuntime(presentation.RuntimeRows, "Model file") ?? "Not reported";
        CompatibilityCurrentCache.Text = o?.CurrentCacheFormat
            ?? FindRuntime(presentation.RuntimeRows, "Cache") ?? "Not reported";
        CompatibilityRuntimeRoute.Text = FindRuntime(presentation.RuntimeRows, "Engine")
            ?? FindRuntime(presentation.RuntimeRows, "Runtime") ?? "Not reported";
        CompatibilityCurrentContextRow.Visibility = presentation.MemoryClarity is null
            ? Visibility.Collapsed
            : Visibility.Visible;
        CompatibilityCurrentContext.Text = presentation.MemoryClarity is { } clarity
            ? $"{clarity.ContextTokens:N0} tokens"
            : string.Empty;
        IReadOnlyList<CompatibilityFact> memory = presentation.MachineMemory?.Facts ?? [];
        CompatibilityInstalledRam.Text = FindFact(memory, "Installed RAM") ?? "Not reported";
        CompatibilityAvailableNow.Text = FindFact(memory, "Available now") ?? "Not reported";
        CompatibilitySafetyReserve.Text = FindFact(memory, "Safety reserve") ?? "Not reported";
        CompatibilitySafeForModel.Text = FindFact(memory, "Safe for this model")
            ?? CompatibilityBudget.Describe(presentation.Budget.SafeLimitBytes);
        CompatibilitySetupFacts.Visibility = presentation.RuntimeRows.Count > 0 ||
            memory.Count > 0 || o is not null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyOptimization(CompatibilityOptimizationPresentation? o)
    {
        if (o is null)
        {
            _safeSliderChoiceIdentities = [];
            _openVinoAvailablePreferenceStops = [];
            _openVinoRequestedPreference = 50;
            _safeSliderSelectionSynchronized = false;
            _exactSelectionSynchronized = false;
            CompatibilitySafeSliderPanel.Visibility = Visibility.Collapsed;
            CompatibilitySafeSliderEndpoints.Visibility = Visibility.Collapsed;
            CompatibilitySelectionStatusText.Visibility = Visibility.Collapsed;
            CompatibilityPreferenceSlider.Visibility = Visibility.Collapsed;
            CompatibilityPreferenceSlider.IsEnabled = false;
            CompatibilitySelectedSetupFocusTarget.IsTabStop = false;
            AutomationProperties.SetAccessibilityView(
                CompatibilitySelectedSetupFocusTarget,
                AccessibilityView.Raw);
            return;
        }

        _applyingOptimization = true;
        try
        {
            bool isGguf = o.Route == GraniteEdgeAI.ModelHardwareCompatibility.Core
                .Application.Optimization.OptimizationRoute.Gguf;
            if (isGguf)
            {
                _openVinoAvailablePreferenceStops = [];
                _openVinoRequestedPreference = 50;
            }
            CompatibilityExactOptimizationModePresentation[] intendedGgufModes =
                o.ExactSafeModes.Where(mode =>
                    !mode.Mode.IsExperimental &&
                    IntendedGgufSliderSlot(mode.Mode.CacheFormat) >= 0)
                    .OrderBy(mode => IntendedGgufSliderSlot(mode.Mode.CacheFormat))
                    .ToArray();
            bool intendedGgufSetProven = intendedGgufModes.Length == 3 &&
                intendedGgufModes.Select(mode =>
                    IntendedGgufSliderSlot(mode.Mode.CacheFormat))
                    .Distinct().Count() == 3;
            IReadOnlyList<OpenVinoSliderChoice>
                intendedOpenVinoModes = ReleasedOpenVinoSliderChoices(o);
            IReadOnlyList<CompatibilityExactOptimizationModePresentation> choices =
                isGguf
                    ? intendedGgufSetProven ? intendedGgufModes : []
                    : intendedOpenVinoModes.Select(choice => choice.Candidate).ToArray();
            _safeSliderChoiceIdentities = choices
                .Select(mode => mode.CandidateIdentity)
                .ToArray();
            if (!isGguf)
            {
                _openVinoAvailablePreferenceStops = intendedOpenVinoModes
                    .Select(choice => choice.PreferenceValue)
                    .ToArray();
                if (intendedOpenVinoModes.Count == 0)
                {
                    if (o.IsActionAuthoritative
                        && (ViewModel.CurrentOptimizationHandoff is not null
                            || string.IsNullOrWhiteSpace(o.SelectionStatusText)))
                    {
                        ViewModel.SelectExactPreference(string.Empty);
                        return;
                    }
                }
                else
                {
                    int resolvedPreference = ResolveNearestOpenVinoPreferenceValue(
                        _openVinoRequestedPreference,
                        intendedOpenVinoModes);
                    OpenVinoSliderChoice resolvedChoice = intendedOpenVinoModes
                        .Single(choice => choice.PreferenceValue == resolvedPreference);
                    _openVinoRequestedPreference = resolvedPreference;
                    bool resolvedChoiceSelected = o.Preference.Kind ==
                            GraniteEdgeAI.ModelHardwareCompatibility.Core.Application
                                .Optimization.OptimizationPreferenceKind.Exact
                        && string.Equals(
                            o.Preference.ExactCandidateIdentity,
                            resolvedChoice.Candidate.CandidateIdentity,
                            StringComparison.Ordinal);
                    if (o.IsActionAuthoritative && !resolvedChoiceSelected)
                    {
                        ViewModel.SelectExactPreference(
                            resolvedChoice.Candidate.CandidateIdentity);
                        return;
                    }
                }
            }
            string? authoritativeSelectedIdentity =
                o.Preference.Kind == GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.OptimizationPreferenceKind.Exact
                    ? o.Preference.ExactCandidateIdentity
                    : o.SafeSliderSelectedIndex is { } sourceIndex &&
                        (uint)sourceIndex < (uint)o.SafeSliderModes.Count
                        ? o.SafeSliderModes[sourceIndex].CandidateIdentity
                        : null;
            int locatedIndex = Array.FindIndex(
                _safeSliderChoiceIdentities,
                identity => string.Equals(identity,
                    authoritativeSelectedIdentity,
                    StringComparison.Ordinal));
            int? selectedIndex = locatedIndex >= 0 ? locatedIndex : null;
            bool hasSelectedIndex = selectedIndex is { } index
                && (uint)index < (uint)choices.Count;
            CompatibilityExactOptimizationModePresentation[] exactMatches =
                o.Preference.Kind == GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.OptimizationPreferenceKind.Exact
                    ? o.ExactSafeModes.Where(mode => string.Equals(mode.CandidateIdentity,
                        o.Preference.ExactCandidateIdentity, StringComparison.Ordinal)).Take(2).ToArray()
                    : [];
            CompatibilityExactOptimizationModePresentation? exactChoice = exactMatches.Length == 1 ? exactMatches[0] : null;
            CompatibilityExactOptimizationModePresentation? selectedChoice =
                hasSelectedIndex ? choices[selectedIndex!.Value] : null;
            bool exactSelected = o.Preference.Kind ==
                GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization
                    .OptimizationPreferenceKind.Exact;
            bool hasAuthoritativeOpenVinoSelection = !isGguf
                && o.IsActionAuthoritative
                && exactSelected
                && hasSelectedIndex
                && string.Equals(
                    o.Preference.ExactCandidateIdentity,
                    selectedChoice?.CandidateIdentity,
                    StringComparison.Ordinal);
            bool hasValidSelection = isGguf
                ? hasSelectedIndex || exactChoice is not null
                : hasAuthoritativeOpenVinoSelection;
            bool selectionBusy = (ViewModel.IsOptimizationStartBusy || _experimentalConsentPending)
                && hasValidSelection;
            bool selectionFailed = !selectionBusy
                && (!string.IsNullOrWhiteSpace(o.SelectionStatusText)
                    || !hasValidSelection);
            string selectionStatus = o.SelectionStatusText;
            if (string.IsNullOrWhiteSpace(selectionStatus) && choices.Count == 0 && exactChoice is null)
            {
                selectionStatus =
                    "No verified safe setup is available for this model and computer.";
            }
            else if (string.IsNullOrWhiteSpace(selectionStatus) && !hasValidSelection)
            {
                selectionStatus =
                    "The selected safe setup is no longer available. Choose optimisation again.";
            }
            CompatibilityOptimizationModePresentation selected =
                isGguf
                    ? exactChoice?.Mode ?? selectedChoice?.Mode ?? o.SelectedMode
                    : o.SelectedMode;
            bool multipleChoices = isGguf ? choices.Count > 1 : choices.Count > 0;
            CompatibilitySafeSliderPanel.Visibility = (!isGguf || choices.Count > 0)
                && !(isGguf && o.IsActionAuthoritative && exactChoice is not null && !hasSelectedIndex)
                ? Visibility.Visible
                : Visibility.Collapsed;
            CompatibilitySafeSliderEndpoints.Visibility = multipleChoices
                ? Visibility.Visible
                : Visibility.Collapsed;
            CompatibilityPreferenceSlider.Visibility = multipleChoices
                ? Visibility.Visible
                : Visibility.Collapsed;
            CompatibilityPreferenceSlider.IsEnabled =
                multipleChoices && o.IsActionAuthoritative
                && (isGguf
                    ? !selectionBusy && !selectionFailed
                    : !ViewModel.IsOptimizationStartBusy && !_experimentalConsentPending);
            if (isGguf)
            {
                CompatibilityPreferenceSlider.Minimum = 0;
                CompatibilityPreferenceSlider.Maximum = Math.Max(0, choices.Count - 1);
            }
            else
            {
                // Set the expanding bound first so switching routes never
                // creates a transient Minimum > Maximum RangeBase state.
                CompatibilityPreferenceSlider.Maximum = OpenVinoPreferenceStops[^1];
                CompatibilityPreferenceSlider.Minimum = OpenVinoPreferenceStops[0];
            }
            CompatibilityPreferenceSlider.StepFrequency = isGguf ? 1 : 20;
            CompatibilityPreferenceSlider.SmallChange = isGguf ? 1 : 20;
            CompatibilityPreferenceSlider.LargeChange = isGguf ? 1 : 20;
            CompatibilityOptimizationDetail.Text = o.Instruction;
            if (isGguf)
            {
                if (hasSelectedIndex) CompatibilityPreferenceSlider.Value = selectedIndex!.Value;
            }
            else
            {
                CompatibilityPreferenceSlider.Value = _openVinoRequestedPreference;
            }

            _safeSliderSelectionSynchronized = isGguf
                ? selectedChoice is not null
                    && exactSelected
                    && string.Equals(
                        o.Preference.ExactCandidateIdentity,
                        selectedChoice.CandidateIdentity,
                        StringComparison.Ordinal)
                : hasAuthoritativeOpenVinoSelection && !selectionFailed;
            _exactSelectionSynchronized = isGguf
                && exactChoice is not null && o.IsActionAuthoritative;

            // Mode and expected quality are one projection. Apply both while
            // slider callbacks are suppressed so a programmatic Value update
            // cannot re-enter selection with a partly rendered option
            string selectedModeLabel = !isGguf
                ? OpenVinoPreferenceLabel(_openVinoRequestedPreference)
                : selected.Label;
            CompatibilitySelectedMode.Text = selectedModeLabel;
            CompatibilityExpectedQuality.Text = selected.ExpectedQualityText;
            CompatibilitySelectedWeightFormat.Text = selected.WeightFormat;
            CompatibilitySelectedCacheFormat.Text = selected.CacheFormat;
            CompatibilitySelectedContext.Text = selected.ContextText;
            string selectedSetupName =
                $"Selected setup. Weight format {selected.WeightFormat}. " +
                $"KV cache format {selected.CacheFormat}. Context {selected.ContextText}.";
            AutomationProperties.SetName(
                CompatibilitySelectedSetupSummary,
                selectedSetupName);
            bool useSingleSetupFocusTarget = isGguf && choices.Count == 1
                && o.IsActionAuthoritative
                && !selectionBusy && !selectionFailed;
            CompatibilitySelectedSetupFocusTarget.IsTabStop =
                useSingleSetupFocusTarget;
            AutomationProperties.SetAccessibilityView(
                CompatibilitySelectedSetupFocusTarget,
                useSingleSetupFocusTarget
                    ? AccessibilityView.Content
                    : AccessibilityView.Raw);
            AutomationProperties.SetName(
                CompatibilitySelectedSetupFocusTarget,
                selectedSetupName);
            CompatibilitySelectedSetupLabel.Visibility = selectionFailed
                ? Visibility.Collapsed
                : Visibility.Visible;
            CompatibilitySelectedSetupSummary.Visibility = selectionFailed
                ? Visibility.Collapsed
                : Visibility.Visible;
            CompatibilitySelectedTradeoffSummary.Visibility = selectionFailed
                ? Visibility.Collapsed
                : Visibility.Visible;
            CompatibilitySelectionStatusText.Text = selectionStatus;
            CompatibilitySelectionStatusText.Visibility = selectionBusy || selectionFailed
                ? Visibility.Visible
                : Visibility.Collapsed;
            CompatibilitySelectedWarningText.Text = selected.WarningText;
            CompatibilitySelectedWarningText.Visibility = !selectionFailed
                && selected.HasStrongQualityWarning
                ? Visibility.Visible : Visibility.Collapsed;
            AutomationProperties.SetName(
                CompatibilityPreferenceSlider,
                selectionBusy
                    ? "Checking current memory and storage"
                    : selectionFailed
                    ? "Safe setup unavailable"
                    : !isGguf
                    ? $"Optimisation preference {_openVinoRequestedPreference} of 90, " +
                        $"{selectedModeLabel}. Weight format {selected.WeightFormat}. " +
                        $"KV cache format {selected.CacheFormat}. Context {selected.ContextText}. " +
                        selected.ExpectedQualityText
                    : !hasSelectedIndex
                    ? "Released safe alternatives; the current exact setup is shown separately"
                    : $"Safe setup {selectedIndex!.Value + 1} of {choices.Count}, " +
                        $"{selected.Label}. Weight format {selected.WeightFormat}. " +
                        $"KV cache format {selected.CacheFormat}. Context {selected.ContextText}. " +
                        selected.ExpectedQualityText);
            AutomationProperties.SetHelpText(
                CompatibilityPreferenceSlider,
                selectionBusy || selectionFailed
                    ? selectionStatus
                    : !isGguf
                    ? "Five preference stops: Maximum efficiency, Efficient, Balanced, " +
                        "High capability, and Maximum capability. The selected setup is " +
                        "revalidated for current memory and storage. " +
                        $"Selected mode: {selectedModeLabel}. Weight format " +
                        $"{selected.WeightFormat}. KV cache format {selected.CacheFormat}. " +
                        $"Context {selected.ContextText}. {selected.ExpectedQualityText}."
                    : "Stops run from lower to higher estimated RAM; measured quality " +
                        "is shown separately. " +
                        $"Selected mode: {selected.Label}. Weight format " +
                        $"{selected.WeightFormat}. KV cache format {selected.CacheFormat}. " +
                        $"Context {selected.ContextText}. {selected.ExpectedQualityText}.");
            AutomationProperties.SetName(CompatibilitySelectedTradeoffSummary,
                $"Selected mode: {selectedModeLabel}. Weight format " +
                $"{selected.WeightFormat}. KV cache format {selected.CacheFormat}. " +
                $"Context {selected.ContextText}. {selected.ExpectedQualityText}.");
        }
        finally
        {
            _applyingOptimization = false;
        }
    }

    private void ApplyRecovery(CompatibilityPresentation presentation)
    {
        bool shown = presentation.Recoveries.Count > 0 ||
            presentation.MemoryRecoveryReason == CompatibilityMemoryRecoveryReason.SystemMemoryPressure;
        CompatibilityRecoveryNotice.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
        if (!shown) return;
        CompatibilityRecovery? recovery = presentation.Recoveries.FirstOrDefault();
        CompatibilityRecoveryHeading.Text = recovery?.Title ?? "Free some memory, then check again";
        CompatibilityRecoveryDetail.Text = recovery?.Detail ??
            "Close unused applications and browser tabs, then refresh the current memory check.";
    }

    private void ApplyActions(CompatibilityPresentation presentation)
    {
        BtnCompatibilityBack.Content = presentation.SecondaryActionText;
        BtnCompatibilityBack.Command = presentation.SecondaryActionKind switch
        {
            CompatibilitySecondaryActionKind.Back => ViewModel.BackCommand,
            CompatibilitySecondaryActionKind.Cancel => ViewModel.CancelCommand,
            CompatibilitySecondaryActionKind.Retry => ViewModel.RetryCommand,
            _ => null
        };
        BtnCompatibilityBack.Visibility = string.IsNullOrWhiteSpace(presentation.SecondaryActionText)
            ? Visibility.Collapsed : Visibility.Visible;
        BtnCompatibilityBack.IsEnabled = presentation.SecondaryActionEnabled &&
            BtnCompatibilityBack.Command is not null;
        (string backActionId, string backActionName, string backActionHelp) =
            presentation.SecondaryActionKind switch
            {
                CompatibilitySecondaryActionKind.Back => (
                    "CompatibilityAction.Back",
                    "Back",
                    "Return to model inspection."),
                CompatibilitySecondaryActionKind.Cancel => (
                    "CompatibilityAction.Cancel",
                    "Cancel",
                    "Stop the compatibility check."),
                CompatibilitySecondaryActionKind.Retry => (
                    "CompatibilityAction.Retry",
                    "Check again",
                    "Run the compatibility check again."),
                _ => (string.Empty, string.Empty, string.Empty)
            };
        AutomationProperties.SetAutomationId(BtnCompatibilityBack, backActionId);
        AutomationProperties.SetName(BtnCompatibilityBack, backActionName);
        AutomationProperties.SetHelpText(BtnCompatibilityBack, backActionHelp);

        bool recovery = presentation.MemoryRecoveryReason ==
            CompatibilityMemoryRecoveryReason.SystemMemoryPressure;
        if (recovery)
        {
            BtnCompatibilityPrimary.Visibility = Visibility.Visible;
            BtnCompatibilitySecondaryForward.Content = "Open Task Manager";
            BtnCompatibilitySecondaryForward.Command = ViewModel.OpenTaskManagerCommand;
            BtnCompatibilitySecondaryForward.Visibility = Visibility.Visible;
            BtnCompatibilitySecondaryForward.IsEnabled =
                ViewModel.OpenTaskManagerCommand.CanExecute(null);
            AutomationProperties.SetAutomationId(
                BtnCompatibilitySecondaryForward,
                "CompatibilityAction.OpenTaskManager");
            AutomationProperties.SetName(
                BtnCompatibilitySecondaryForward,
                "Open Task Manager");
            AutomationProperties.SetHelpText(
                BtnCompatibilitySecondaryForward,
                "Open Windows Task Manager to close apps and free memory.");
            BtnCompatibilityPrimary.Content = "Restart hardware inspection";
            BtnCompatibilityPrimary.Command = null;
            BtnCompatibilityPrimary.IsEnabled =
                !_hardwareRestartPending && ViewModel.RefreshMemoryCommand.CanExecute(null);
            AutomationProperties.SetAutomationId(
                BtnCompatibilityPrimary,
                "CompatibilityAction.RefreshMemory");
            AutomationProperties.SetName(
                BtnCompatibilityPrimary,
                "Restart hardware inspection");
            AutomationProperties.SetHelpText(
                BtnCompatibilityPrimary,
                "Start a fresh full hardware inspection, including current memory information.");
        }
        else
        {
            bool importAnotherModel = presentation.ForwardActionKind ==
                CompatibilityForwardActionKind.ImportAnotherModel;
            bool showOptimiseFurther = presentation.ShowOptimiseFurtherAction;
            bool canOptimiseFurther =
                ViewModel.OptionalOptimizationCommand.CanExecute(null);
            bool hasOptimizationChoices = presentation.Optimization is
                { Modes.Count: > 0 };
            bool canEnterConfigureStage = CanAccessConfigureStage(presentation);
            bool canChatWithCurrentModel =
                ViewModel.ChatCurrentModelCommand.CanExecute(null);
            string chatActionText = string.IsNullOrWhiteSpace(
                presentation.CurrentModelChatActionText)
                    ? "Chat with current model"
                    : presentation.CurrentModelChatActionText;
            bool unavailableOptimization = hasOptimizationChoices
                && !canEnterConfigureStage
                && !canChatWithCurrentModel;

            bool optimiseFurtherIsSecondary = showOptimiseFurther
                && canChatWithCurrentModel;
            BtnCompatibilitySecondaryForward.Content = importAnotherModel
                ? "Import another model"
                : optimiseFurtherIsSecondary
                    ? presentation.OptimiseFurtherActionText
                    : "Choose optimisation";
            BtnCompatibilitySecondaryForward.Command = null;
            BtnCompatibilitySecondaryForward.Visibility =
                importAnotherModel || optimiseFurtherIsSecondary
                    || canEnterConfigureStage && canChatWithCurrentModel
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            BtnCompatibilitySecondaryForward.IsEnabled =
                importAnotherModel
                    ? IsImportRequestEligible()
                        && Volatile.Read(ref _importNavigationPending) == 0
                    : optimiseFurtherIsSecondary
                        ? canOptimiseFurther
                        : canEnterConfigureStage && canChatWithCurrentModel;
            AutomationProperties.SetAutomationId(
                BtnCompatibilitySecondaryForward,
                importAnotherModel
                    ? "CompatibilityAction.ImportAnotherModel"
                    : optimiseFurtherIsSecondary
                        ? "CompatibilityAction.OptimiseFurther"
                    : "CompatibilityAction.ConfigureModel");
            AutomationProperties.SetName(
                BtnCompatibilitySecondaryForward,
                importAnotherModel
                    ? "Import another model"
                    : optimiseFurtherIsSecondary
                        ? presentation.OptimiseFurtherActionText
                    : "Choose optimisation");
            AutomationProperties.SetHelpText(
                BtnCompatibilitySecondaryForward,
                importAnotherModel
                    ? "Return to model selection to import another model."
                    : optimiseFurtherIsSecondary
                        ? "Review admitted optimisation options for this model and current hardware."
                    : "Review admitted optimisation options for this model and current hardware.");

            bool configureIsPrimary =
                !showOptimiseFurther && canEnterConfigureStage && !canChatWithCurrentModel;
            bool optimiseFurtherIsPrimary = showOptimiseFurther
                && !canChatWithCurrentModel;
            BtnCompatibilityPrimary.Visibility = unavailableOptimization
                && !optimiseFurtherIsPrimary
                ? Visibility.Collapsed
                : Visibility.Visible;
            BtnCompatibilityPrimary.Content = optimiseFurtherIsPrimary
                ? presentation.OptimiseFurtherActionText
                : configureIsPrimary
                ? "Choose optimisation"
                : canChatWithCurrentModel
                    ? chatActionText
                    : presentation.PrimaryActionText;
            BtnCompatibilityPrimary.Command = optimiseFurtherIsPrimary
                ? null
                : configureIsPrimary
                ? null
                : canChatWithCurrentModel
                    ? ViewModel.ChatCurrentModelCommand
                    : ViewModel.ContinueCommand;
            BtnCompatibilityPrimary.IsEnabled = optimiseFurtherIsPrimary
                ? canOptimiseFurther
                : configureIsPrimary
                ? canEnterConfigureStage
                : canChatWithCurrentModel
                    ? ViewModel.ChatCurrentModelCommand.CanExecute(null)
                    : presentation.PrimaryActionEnabled;
            AutomationProperties.SetAutomationId(
                BtnCompatibilityPrimary,
                optimiseFurtherIsPrimary
                    ? "CompatibilityAction.OptimiseFurther"
                    : configureIsPrimary
                    ? "CompatibilityAction.ConfigureModel"
                    : canChatWithCurrentModel
                        ? "CompatibilityAction.ChatCurrentModel"
                        : "CompatibilityAction.Continue");
            AutomationProperties.SetName(
                BtnCompatibilityPrimary,
                optimiseFurtherIsPrimary
                    ? presentation.OptimiseFurtherActionText
                    : configureIsPrimary
                    ? "Choose optimisation"
                    : canChatWithCurrentModel
                        ? chatActionText
                        : presentation.PrimaryActionText);
            AutomationProperties.SetHelpText(
                BtnCompatibilityPrimary,
                optimiseFurtherIsPrimary
                    ? "Review admitted optimisation options for this model and current hardware."
                    : configureIsPrimary
                    ? "Review admitted optimisation options for this model and current hardware."
                    : canChatWithCurrentModel
                        ? "Open chat with the current model without changing its setup."
                        : "Continue with the current compatibility result.");
            if (presentation.StorageShortage is not null)
            {
                BtnCompatibilityPrimary.Visibility = Visibility.Visible;
                BtnCompatibilityPrimary.Content = "Check again";
                BtnCompatibilityPrimary.Command = null;
                BtnCompatibilityPrimary.IsEnabled = true;
                AutomationProperties.SetAutomationId(BtnCompatibilityPrimary, "CompatibilityAction.CheckAgain");
                AutomationProperties.SetName(BtnCompatibilityPrimary, "Check again");
                AutomationProperties.SetHelpText(BtnCompatibilityPrimary, "Run hardware inspection again after freeing disk space.");
            }
            BtnCompatibilityConfigurePrimary.Content = "Start optimisation";
            BtnCompatibilityConfigurePrimary.IsEnabled =
                (_safeSliderSelectionSynchronized || _exactSelectionSynchronized)
                && !_experimentalConsentPending
                && ViewModel.StartOptimizationCommand.CanExecute(null);
            AutomationProperties.SetAutomationId(
                BtnCompatibilityConfigurePrimary,
                "CompatibilityConfigureAction.StartOptimization");
            AutomationProperties.SetName(
                BtnCompatibilityConfigurePrimary,
                "Start optimisation");
        }
    }

    private void CompatibilityPrimary_Click(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(sender, BtnCompatibilityPrimary)
            && (_presentation.StorageShortage is not null || _presentation.MemoryRecoveryReason == CompatibilityMemoryRecoveryReason.SystemMemoryPressure))
        {
            if (_hardwareRestartPending) return;
            _hardwareRestartPending = true;
            BtnCompatibilityPrimary.IsEnabled = false;
            BtnCompatibilityPrimary.Content = "Restarting hardware inspection…";
            if (HardwareRetryRequested is null) ShowHardwareRestartFailure();
            else
            {
                try { HardwareRetryRequested.Invoke(this, EventArgs.Empty); }
                catch (InvalidOperationException) { ShowHardwareRestartFailure(); }
            }
            return;
        }
        if (ReferenceEquals(sender, BtnCompatibilitySecondaryForward)
            && _presentation.ForwardActionKind ==
                CompatibilityForwardActionKind.ImportAnotherModel)
        {
            if (TryBeginImportNavigation())
            {
                ImportAnotherModelRequested?.Invoke(this, EventArgs.Empty);
            }
            return;
        }

        if (_presentation.ShowOptimiseFurtherAction
            && (ReferenceEquals(sender, BtnCompatibilityPrimary)
                || ReferenceEquals(sender, BtnCompatibilitySecondaryForward)))
        {
            if (!ViewModel.OptionalOptimizationCommand.CanExecute(null))
            {
                return;
            }

            ViewModel.OptionalOptimizationCommand.Execute(null);
            if (CanAccessConfigureStage(_presentation))
            {
                EnterConfigureStage();
            }
            return;
        }

        if (sender is not Button { Command: null }
            || !CanAccessConfigureStage(_presentation))
        {
            return;
        }

        EnterConfigureStage();
    }

    internal bool CanCompleteImportNavigation =>
        Volatile.Read(ref _importNavigationPending) != 0
        && IsImportRequestEligible();

    internal void CancelImportNavigation()
    {
        Interlocked.Exchange(ref _importNavigationPending, 0);
        ApplyActions(_presentation);
    }

    private bool TryBeginImportNavigation()
    {
        if (!IsImportRequestEligible()
            || Interlocked.CompareExchange(ref _importNavigationPending, 1, 0) != 0)
        {
            return false;
        }

        ApplyActions(_presentation);
        return true;
    }

    private bool IsImportRequestEligible() =>
        _presentation.ForwardActionKind ==
            CompatibilityForwardActionKind.ImportAnotherModel
        && !_configureStageVisible
        && CompatibilityOutcomePanel.Visibility == Visibility.Visible;

    private void EnterConfigureStage()
    {
        if (_configureStageVisible
            || !CanAccessConfigureStage(_presentation))
        {
            return;
        }

        SynchronizeSafeSliderSelection();
        _configureStageVisible = true;
        ApplyConfigureResponsiveLayout(LayoutRoot.ActualWidth);
        ProjectVisibleStage(analysing: false);
        ConfigureStageEntered?.Invoke(this, EventArgs.Empty);
        _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (CanFocusSafeSlider())
            {
                CompatibilityPreferenceSlider.Focus(FocusState.Programmatic);
            }
            else if (CanFocusSingleSafeSetup())
            {
                CompatibilitySelectedSetupFocusTarget.Focus(
                    FocusState.Programmatic);
            }
        });
    }

    private void CompatibilityConfigureBack_Click(
        object sender,
        RoutedEventArgs e) => ExitConfigureStage(restoreFocus: true);

    private void ExitConfigureStage(bool restoreFocus)
    {
        if (!_configureStageVisible)
        {
            return;
        }

        ViewModel.CancelOptimizationStartIntent();
        _openVinoRequestedPreference = 50;
        _configureStageVisible = false;
        ProjectVisibleStage(
            CompatibilityAnalysisPanel.Visibility == Visibility.Visible);
        ConfigureStageExited?.Invoke(this, EventArgs.Empty);
        if (!restoreFocus)
        {
            return;
        }

        _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (!_configureStageVisible
                && CompatibilityOutcomePanel.Visibility == Visibility.Visible)
            {
                Button entry = BtnCompatibilitySecondaryForward.Visibility
                        == Visibility.Visible
                    ? BtnCompatibilitySecondaryForward
                    : BtnCompatibilityPrimary;
                entry.Focus(FocusState.Programmatic);
            }
        });
    }

    private void ProjectVisibleStage(bool analysing)
    {
        bool configure = !analysing
            && _configureStageVisible
            && CanDisplayConfigureStage(_presentation);
        CompatibilityConfigureIntroduction.Visibility = configure
            ? Visibility.Visible
            : Visibility.Collapsed;
        CompatibilityOutcomePanel.Visibility = !analysing && !configure
            ? Visibility.Visible
            : Visibility.Collapsed;
        CompatibilityOutcomeBand.Visibility = configure
            ? Visibility.Collapsed
            : Visibility.Visible;
        CompatibilityTerminalActionBand.Visibility = !analysing && !configure
            ? Visibility.Visible
            : Visibility.Collapsed;
        CompatibilityConfigurePanel.Visibility = configure
            ? Visibility.Visible
            : Visibility.Collapsed;
        CompatibilityOptimizationPanel.Visibility = configure
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private static bool CanAccessConfigureStage(
        CompatibilityPresentation presentation) =>
        CanDisplayConfigureStage(presentation)
        && presentation.Optimization is { } optimization
        && (optimization.SafeSliderModes.Count > 0 || optimization.ExactSafeModes.Count > 0);

    private static bool CanDisplayConfigureStage(
        CompatibilityPresentation presentation) =>
        presentation.MemoryRecoveryReason !=
            CompatibilityMemoryRecoveryReason.SystemMemoryPressure
        && presentation.Optimization is { IsActionAuthoritative: true };

    private void SynchronizeSafeSliderSelection()
    {
        if (_presentation.Optimization is { Route: GraniteEdgeAI.ModelHardwareCompatibility.Core
                .Application.Optimization.OptimizationRoute.OpenVino })
        {
            SelectResolvedOpenVinoPreference(50);
            return;
        }

        if (_presentation.Optimization is { } retained
            && retained.Preference.Kind == GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.OptimizationPreferenceKind.Exact
            && retained.ExactSafeModes.Any(mode => string.Equals(mode.CandidateIdentity,
                retained.Preference.ExactCandidateIdentity, StringComparison.Ordinal))) return;
        if (_presentation.Optimization is not { } optimization
            || optimization.SafeSliderSelectedIndex is not { } index
            || (uint)index >= (uint)optimization.SafeSliderModes.Count)
        {
            return;
        }

        string identity = optimization.SafeSliderModes[index].CandidateIdentity;
        if (optimization.Preference.Kind ==
                GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization
                    .OptimizationPreferenceKind.Exact
            && string.Equals(
                optimization.Preference.ExactCandidateIdentity,
                identity,
                StringComparison.Ordinal))
        {
            return;
        }

        ViewModel.SelectExactPreference(identity);
    }

    private bool CanChangeExactOptions => _isActive && _configureStageVisible
        && !_applyingOptimization && !_experimentalConsentPending
        && !ViewModel.IsOptimizationStartBusy
        && _presentation.Optimization is { IsActionAuthoritative: true };

    private static int IntendedGgufSliderSlot(string cacheFormat) =>
        cacheFormat switch
        {
            "TurboQuant 3-bit" or "TurboQuant3Bit" => 0,
            "TurboQuant4Bit" or "TurboQuant 4-bit" => 1,
            "Q8_0" => 2,
            _ => -1
        };

    private static int NormalizeOpenVinoPreferenceValue(int value) =>
        OpenVinoPreferenceStops.MinBy(stop => Math.Abs(stop - value));

    private static string OpenVinoPreferenceLabel(int value) =>
        NormalizeOpenVinoPreferenceValue(value) switch
        {
            10 => "Maximum efficiency",
            30 => "Efficient",
            50 => "Balanced",
            70 => "High capability",
            90 => "Maximum capability",
            _ => "Balanced"
        };

    private async Task ConfirmExperimentalSelectionAsync(
        string candidateIdentity,
        string evidenceId,
        long revision)
    {
        _experimentalConsentPending = true;
        ViewModel.SetExperimentalFinalConfirmation(false);
        Apply(ViewModel.Presentation);
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Use this experimental setup?",
            Content = "This setup uses an experimental optimisation path. Its output quality may vary. Granite will recheck the selected setup before it can start.",
            PrimaryButtonText = "Use experimental setup",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };
        AutomationProperties.SetName(dialog, "Confirm experimental optimisation setup");

        ContentDialogResult result;
        try
        {
            result = await dialog.ShowAsync();
        }
        catch (Exception exception)
        {
            if (revision == _exactConsentRevision)
            {
                _experimentalConsentPending = false;
                ViewModel.SetExperimentalFinalConfirmation(false);
                Apply(ViewModel.Presentation);
            }
            if (Interlocked.Exchange(ref _unexpectedFaultReported, 1) == 0)
            {
                _faultReporter.Report(ApplicationFault.FromException(
                    ApplicationFaultCode.CompatibilityEvaluationUnexpected,
                    exception));
            }
            return;
        }
        bool current = _isActive && _configureStageVisible &&
            revision == _exactConsentRevision &&
            string.Equals(candidateIdentity,
                ViewModel.SelectedPreference?.ExactCandidateIdentity,
                StringComparison.Ordinal) &&
            string.Equals(evidenceId,
                ViewModel.SelectedExperimentalConsentEvidenceId,
                StringComparison.Ordinal);
        if (!current)
        {
            if (revision == _exactConsentRevision)
            {
                _experimentalConsentPending = false;
                ViewModel.SetExperimentalFinalConfirmation(false);
                Apply(ViewModel.Presentation);
            }
            return;
        }

        try
        {
            bool granted = result == ContentDialogResult.Primary &&
                await ViewModel.SetExperimentalConsentAsync(evidenceId, true);
            current = _isActive && _configureStageVisible &&
                revision == _exactConsentRevision &&
                string.Equals(candidateIdentity,
                    ViewModel.SelectedPreference?.ExactCandidateIdentity,
                    StringComparison.Ordinal) &&
                string.Equals(evidenceId,
                    ViewModel.SelectedExperimentalConsentEvidenceId,
                    StringComparison.Ordinal);
            ViewModel.SetExperimentalFinalConfirmation(granted && current);
            if (!granted && current)
            {
                await ViewModel.SetExperimentalConsentAsync(evidenceId, false);
            }
        }
        catch (Exception exception)
        {
            ViewModel.SetExperimentalFinalConfirmation(false);
            if (Interlocked.Exchange(ref _unexpectedFaultReported, 1) == 0)
            {
                _faultReporter.Report(ApplicationFault.FromException(
                    ApplicationFaultCode.CompatibilityEvaluationUnexpected,
                    exception));
            }
        }
        finally
        {
            if (revision == _exactConsentRevision)
            {
                _experimentalConsentPending = false;
                Apply(ViewModel.Presentation);
                CompatibilityPreferenceSlider.Focus(FocusState.Programmatic);
            }
        }
    }

    private void ApplyAuxiliaryStatus(ViewModels.CompatibilityAuxiliaryStatus status)
    {
        if (status.Kind == ViewModels.CompatibilityAuxiliaryStatusKind.None) return;
        CompatibilityRecoveryNotice.Visibility = Visibility.Visible;
        CompatibilityRecoveryHeading.Text =
            status.Kind == ViewModels.CompatibilityAuxiliaryStatusKind.Error
                ? "Action could not be completed" : "Compatibility update";
        CompatibilityRecoveryDetail.Text = status.Message;
    }

    internal void ShowHardwareRestartFailure()
    {
        _hardwareRestartPending = false;
        BtnCompatibilityPrimary.IsEnabled = _presentation.StorageShortage is not null
            || ViewModel.RefreshMemoryCommand.CanExecute(null);
        BtnCompatibilityPrimary.Content = _presentation.StorageShortage is not null ? "Check again" : "Restart hardware inspection";
        CompatibilityRecoveryNotice.Visibility = Visibility.Visible;
        CompatibilityRecoveryHeading.Text = "Hardware inspection could not restart";
        CompatibilityRecoveryDetail.Text = "Your current model is unchanged. Try again, or go Back to inspect it again.";
        AutomationProperties.SetLiveSetting(CompatibilityRecoveryDetail, AutomationLiveSetting.Polite);
        (FrameworkElementAutomationPeer.FromElement(CompatibilityRecoveryDetail)
            ?? FrameworkElementAutomationPeer.CreatePeerForElement(CompatibilityRecoveryDetail))?
            .RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private async void PreferenceSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_applyingOptimization
            || !CompatibilityPreferenceSlider.IsEnabled
            || _presentation.Optimization is null)
        {
            return;
        }

        CompatibilityOptimizationPresentation optimization =
            _presentation.Optimization;
        if (optimization.Route == GraniteEdgeAI.ModelHardwareCompatibility.Core
                .Application.Optimization.OptimizationRoute.OpenVino)
        {
            int preferenceValue = NormalizeOpenVinoPreferenceValue(
                (int)Math.Round(e.NewValue));
            if (Math.Abs(CompatibilityPreferenceSlider.Value - preferenceValue) > double.Epsilon)
            {
                _applyingOptimization = true;
                try
                {
                    CompatibilityPreferenceSlider.Value = preferenceValue;
                }
                finally
                {
                    _applyingOptimization = false;
                }
            }

            SelectResolvedOpenVinoPreference(preferenceValue);
            return;
        }

        int index = (int)Math.Round(e.NewValue);
        if ((uint)index >= (uint)_safeSliderChoiceIdentities.Length)
        {
            return;
        }

        string identity = _safeSliderChoiceIdentities[index];
        long revision = checked(++_exactConsentRevision);
        ViewModel.SelectExactPreference(identity);
        CompatibilityExactOptimizationModePresentation? selected =
            _presentation.Optimization?.ExactSafeModes.SingleOrDefault(mode =>
                string.Equals(mode.CandidateIdentity, identity,
                    StringComparison.Ordinal));
        if (selected?.Mode.IsExperimental == true &&
            ViewModel.SelectedExperimentalConsentEvidenceId is { } evidenceId)
        {
            await ConfirmExperimentalSelectionAsync(
                identity,
                evidenceId,
                revision);
        }
    }

    private void PreferenceSlider_PreviewKeyDown(
        object sender,
        KeyRoutedEventArgs e)
    {
        if (TryHandlePreferenceSliderKey(e.Key))
        {
            e.Handled = true;
        }
    }

    internal bool TryHandlePreferenceSliderKey(VirtualKey key)
    {
        if (_applyingOptimization
            || !CompatibilityPreferenceSlider.IsEnabled
            || _presentation.Optimization is not { } optimization)
        {
            return false;
        }

        int direction = key switch
        {
            VirtualKey.Left or VirtualKey.Down or VirtualKey.PageDown => -1,
            VirtualKey.Right or VirtualKey.Up or VirtualKey.PageUp => 1,
            VirtualKey.Home => int.MinValue,
            VirtualKey.End => int.MaxValue,
            _ => 0
        };
        if (direction == 0)
        {
            return false;
        }

        double target;
        if (optimization.Route == GraniteEdgeAI.ModelHardwareCompatibility.Core
                .Application.Optimization.OptimizationRoute.OpenVino)
        {
            if (_openVinoAvailablePreferenceStops.Length == 0)
            {
                return false;
            }

            int current = ResolveNearestOpenVinoPreferenceValue(
                (int)Math.Round(CompatibilityPreferenceSlider.Value),
                _openVinoAvailablePreferenceStops);
            int currentIndex = Array.IndexOf(
                _openVinoAvailablePreferenceStops,
                current);
            int targetIndex = direction switch
            {
                int.MinValue => 0,
                int.MaxValue => _openVinoAvailablePreferenceStops.Length - 1,
                < 0 => Math.Max(0, currentIndex - 1),
                _ => Math.Min(
                    _openVinoAvailablePreferenceStops.Length - 1,
                    currentIndex + 1)
            };
            target = _openVinoAvailablePreferenceStops[targetIndex];
        }
        else
        {
            int current = (int)Math.Round(CompatibilityPreferenceSlider.Value);
            int minimum = (int)Math.Round(CompatibilityPreferenceSlider.Minimum);
            int maximum = (int)Math.Round(CompatibilityPreferenceSlider.Maximum);
            target = direction switch
            {
                int.MinValue => minimum,
                int.MaxValue => maximum,
                < 0 => Math.Max(minimum, current - 1),
                _ => Math.Min(maximum, current + 1)
            };
        }

        CompatibilityPreferenceSlider.Value = target;
        return true;
    }

    private void SelectResolvedOpenVinoPreference(int preferenceValue)
    {
        CompatibilityOptimizationPresentation? resolved =
            ViewModel.Presentation.Optimization;
        if (resolved is not
                { Route: GraniteEdgeAI.ModelHardwareCompatibility.Core.Application
                    .Optimization.OptimizationRoute.OpenVino,
                  IsActionAuthoritative: true })
        {
            return;
        }

        IReadOnlyList<OpenVinoSliderChoice> choices =
            ReleasedOpenVinoSliderChoices(resolved);
        if (choices.Count == 0)
        {
            _openVinoAvailablePreferenceStops = [];
            ViewModel.SelectExactPreference(string.Empty);
            return;
        }

        _openVinoAvailablePreferenceStops = choices
            .Select(choice => choice.PreferenceValue)
            .ToArray();
        _openVinoRequestedPreference = ResolveNearestOpenVinoPreferenceValue(
            NormalizeOpenVinoPreferenceValue(preferenceValue),
            choices);
        if (Math.Abs(CompatibilityPreferenceSlider.Value
                - _openVinoRequestedPreference) > double.Epsilon)
        {
            _applyingOptimization = true;
            try
            {
                CompatibilityPreferenceSlider.Value =
                    _openVinoRequestedPreference;
            }
            finally
            {
                _applyingOptimization = false;
            }
        }

        OpenVinoSliderChoice choice = choices.Single(item =>
            item.PreferenceValue == _openVinoRequestedPreference);
        ViewModel.SelectExactPreference(choice.Candidate.CandidateIdentity);
    }

    private static IReadOnlyList<OpenVinoSliderChoice>
        ReleasedOpenVinoSliderChoices(
            CompatibilityOptimizationPresentation optimization)
    {
        var choices =
            new List<OpenVinoSliderChoice>(
                OpenVinoPreferenceStops.Length);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (int stop in OpenVinoPreferenceStops)
        {
            CompatibilityExactOptimizationModePresentation[] matches =
                ReleasedOpenVinoMatches(optimization, stop);
            if (matches.Length > 1)
            {
                return [];
            }
            if (matches.Length == 0)
            {
                continue;
            }
            if (!identities.Add(matches[0].CandidateIdentity))
            {
                return [];
            }

            choices.Add(new OpenVinoSliderChoice(stop, matches[0]));
        }

        return choices;
    }

    private static int ResolveNearestOpenVinoPreferenceValue(
        int preferenceValue,
        IReadOnlyList<OpenVinoSliderChoice> choices) =>
        choices
            .OrderBy(choice => Math.Abs(choice.PreferenceValue - preferenceValue))
            .ThenBy(choice => choice.PreferenceValue)
            .First()
            .PreferenceValue;

    internal static int ResolveNearestOpenVinoPreferenceValue(
        int preferenceValue,
        IReadOnlyList<int> choices) =>
        choices
            .OrderBy(stop => Math.Abs(stop - preferenceValue))
            .ThenBy(stop => stop)
            .First();

    private static CompatibilityExactOptimizationModePresentation[]
        ReleasedOpenVinoMatches(
            CompatibilityOptimizationPresentation optimization,
            int preferenceValue)
    {
        (string WeightFormat, string CacheFormat)? requiredSetup =
            RequiredOpenVinoExactSetup(preferenceValue);
        if (requiredSetup is not { } required)
        {
            return [];
        }

        return
        [.. optimization.ExactSafeModes.Where(mode =>
                mode.Availability ==
                    CompatibilityExactOptimizationAvailability.Released
                && !mode.Mode.IsExperimental
                && string.Equals(
                    mode.Mode.WeightFormat,
                    required.WeightFormat,
                    StringComparison.Ordinal)
                && string.Equals(
                    mode.Mode.CacheFormat,
                    required.CacheFormat,
                    StringComparison.Ordinal)
                && string.Equals(
                    mode.Mode.ContextText,
                    "4,096 tokens",
                    StringComparison.Ordinal))
            .Take(2)];
    }

    private static (string WeightFormat, string CacheFormat)?
        RequiredOpenVinoExactSetup(int preferenceValue) =>
        preferenceValue switch
        {
            10 => ("INT4", "TurboQuant TBQ3"),
            30 => ("INT4", "TurboQuant TBQ4"),
            50 => ("INT4", "U4"),
            70 => ("INT4", "U8"),
            90 => ("INT8", "Automatic (OpenVINO default)"),
            _ => null
        };

    private bool CanFocusSafeSlider() =>
        _configureStageVisible
        && CompatibilityConfigurePanel.Visibility == Visibility.Visible
        && CompatibilitySafeSliderPanel.Visibility == Visibility.Visible
        && CompatibilityPreferenceSlider.Visibility == Visibility.Visible
        && CompatibilityPreferenceSlider.IsEnabled;

    private bool CanFocusSingleSafeSetup() =>
        _configureStageVisible
        && CompatibilityConfigurePanel.Visibility == Visibility.Visible
        && CompatibilitySafeSliderPanel.Visibility == Visibility.Visible
        && CompatibilitySelectedSetupFocusTarget.Visibility == Visibility.Visible
        && CompatibilitySelectedSetupFocusTarget.IsEnabled
        && CompatibilitySelectedSetupFocusTarget.IsTabStop;

    internal void SetHardwareReport(
        HardwareInspectionPresentationState? presentation,
        HardwareSummaryPresentation? summary,
        HardwareInspectionDetailsState? details)
    {
        CompatibilityHardwareFactsHost.Children.Clear();
        if (presentation is null
            || summary is null
            || details is null
            || !presentation.ReportCreated
            || !presentation.DetailsAvailable
            || presentation.Kind is not (
                HardwareInspectionPresentationKind.Completed or
                HardwareInspectionPresentationKind.CompletedWithWarnings))
        {
            CompatibilityHardwareFactsExpander.IsExpanded = false;
            CompatibilityHardwareFactsExpander.Visibility = Visibility.Collapsed;
            AutomationProperties.SetName(
                CompatibilityHardwareFactsExpander,
                "Hardware facts");
            return;
        }

        var outcome = new StackPanel { Spacing = 4 };
        outcome.Children.Add(new TextBlock
        {
            Text = presentation.Title,
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Brush("CompatibilityTextBrush"),
            TextWrapping = TextWrapping.WrapWholeWords
        });
        outcome.Children.Add(new TextBlock
        {
            Text = presentation.Body,
            Foreground = Brush("CompatibilityMutedTextBrush"),
            TextWrapping = TextWrapping.WrapWholeWords
        });
        outcome.Children.Add(new TextBlock
        {
            Text = details.ReportDescription,
            FontSize = 12,
            Foreground = Brush("CompatibilitySubtleTextBrush"),
            TextWrapping = TextWrapping.WrapWholeWords
        });
        AutomationProperties.SetName(
            outcome,
            $"{presentation.Title}. {presentation.Body}. {details.ReportDescription}");
        CompatibilityHardwareFactsHost.Children.Add(outcome);

        var review = new Grid { ColumnSpacing = 12 };
        review.ColumnDefinitions.Add(new ColumnDefinition());
        review.ColumnDefinitions.Add(new ColumnDefinition());
        AddReviewCount(
            review,
            0,
            "NEEDS REVIEW",
            presentation.UnresolvedReviewCount);
        AddReviewCount(
            review,
            1,
            "INFORMATION",
            presentation.ResolvedInformationCount);
        AutomationProperties.SetName(
            review,
            $"Hardware review. {presentation.UnresolvedReviewCount} items need review. "
            + $"{presentation.ResolvedInformationCount} information items.");
        CompatibilityHardwareFactsHost.Children.Add(review);

        foreach (IGrouping<string, HardwareFactPresentation> group in
                 summary.Facts.GroupBy(fact => fact.Group, StringComparer.Ordinal))
        {
            var section = new StackPanel { Spacing = 8 };
            section.Children.Add(new TextBlock
            {
                Text = group.Key,
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = Brush("CompatibilityTextBrush")
            });
            foreach (HardwareFactPresentation fact in group)
            {
                var row = new Grid { ColumnSpacing = 12 };
                row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = GridLength.Auto
                });
                var label = new TextBlock
                {
                    Text = fact.Label,
                    TextWrapping = TextWrapping.WrapWholeWords,
                    Foreground = Brush("CompatibilityMutedTextBrush")
                };
                var value = new TextBlock
                {
                    Text = fact.Value,
                    TextWrapping = TextWrapping.Wrap,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = Brush("CompatibilityTextBrush")
                };
                Grid.SetColumn(value, 1);
                row.Children.Add(label);
                row.Children.Add(value);
                if (!string.IsNullOrWhiteSpace(fact.Helper))
                {
                    row.RowDefinitions.Add(new RowDefinition());
                    row.RowDefinitions.Add(new RowDefinition());
                    var helper = new TextBlock
                    {
                        Text = fact.Helper,
                        FontSize = 12,
                        TextWrapping = TextWrapping.WrapWholeWords,
                        Foreground = Brush("CompatibilitySubtleTextBrush")
                    };
                    Grid.SetRow(helper, 1);
                    Grid.SetColumnSpan(helper, 2);
                    row.Children.Add(helper);
                }
                AutomationProperties.SetName(
                    row,
                    string.IsNullOrWhiteSpace(fact.Helper)
                        ? $"{fact.Label}: {fact.Value}"
                        : $"{fact.Label}: {fact.Value}. {fact.Helper}");
                section.Children.Add(row);
            }
            CompatibilityHardwareFactsHost.Children.Add(section);
        }

        var checks = new StackPanel { Spacing = 8 };
        checks.Children.Add(ReportSectionHeading("Inspection checks"));
        foreach (HardwareInspectionDetailRow detail in details.Rows)
        {
            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = GridLength.Auto
            });
            var text = new StackPanel { Spacing = 2 };
            text.Children.Add(new TextBlock
            {
                Text = detail.Title,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = Brush("CompatibilityTextBrush"),
                TextWrapping = TextWrapping.WrapWholeWords
            });
            text.Children.Add(new TextBlock
            {
                Text = detail.Sentence,
                FontSize = 12,
                Foreground = Brush("CompatibilityMutedTextBrush"),
                TextWrapping = TextWrapping.WrapWholeWords
            });
            var status = new TextBlock
            {
                Text = detail.Status,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = Brush("CompatibilityTextBrush"),
                TextWrapping = TextWrapping.WrapWholeWords,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(status, 1);
            row.Children.Add(text);
            row.Children.Add(status);
            AutomationProperties.SetName(row, detail.AccessibleName);
            checks.Children.Add(row);
        }
        CompatibilityHardwareFactsHost.Children.Add(checks);

        foreach (HardwareInspectionTechnicalGroup group in details.TechnicalGroups)
        {
            var technical = new StackPanel { Spacing = 6 };
            technical.Children.Add(ReportSectionHeading(group.Title));
            technical.Children.Add(new TextBlock
            {
                Text = group.Helper,
                FontSize = 12,
                Foreground = Brush("CompatibilityMutedTextBrush"),
                TextWrapping = TextWrapping.WrapWholeWords
            });
            foreach (HardwareInspectionTechnicalItem item in group.Items)
            {
                var row = new Grid { ColumnSpacing = 12 };
                row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = GridLength.Auto
                });
                row.Children.Add(new TextBlock
                {
                    Text = item.Label,
                    Foreground = Brush("CompatibilityMutedTextBrush"),
                    TextWrapping = TextWrapping.WrapWholeWords
                });
                var value = new TextBlock
                {
                    Text = item.Value,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = Brush("CompatibilityTextBrush"),
                    TextWrapping = TextWrapping.WrapWholeWords
                };
                Grid.SetColumn(value, 1);
                row.Children.Add(value);
                AutomationProperties.SetName(row, $"{item.Label}: {item.Value}");
                technical.Children.Add(row);
            }
            CompatibilityHardwareFactsHost.Children.Add(technical);
        }

        CompatibilityHardwareFactsExpander.IsExpanded = false;
        CompatibilityHardwareFactsExpander.Visibility = Visibility.Visible;
        AutomationProperties.SetName(
            CompatibilityHardwareFactsExpander,
            $"Hardware facts. {details.ReportBadge}. "
            + $"{presentation.UnresolvedReviewCount} items need review. "
            + $"{presentation.ResolvedInformationCount} information items. "
            + $"{summary.Facts.Count} computer facts. "
            + $"{details.Rows.Count} inspection checks.");
    }

    private void AddReviewCount(Grid host, int column, string label, int value)
    {
        var card = new Border
        {
            Background = Brush("CompatibilitySubtleSurfaceBrush"),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10, 12, 10),
            Child = new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    new TextBlock
                    {
                        Text = label,
                        Style = (Style)Resources["CompatibilityLabelStyle"]
                    },
                    new TextBlock
                    {
                        Text = value.ToString(),
                        Style = (Style)Resources["CompatibilityValueStyle"]
                    }
                }
            }
        };
        Grid.SetColumn(card, column);
        host.Children.Add(card);
    }

    private TextBlock ReportSectionHeading(string text) => new()
    {
        Text = text,
        FontSize = 14,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        Foreground = Brush("CompatibilityTextBrush"),
        TextWrapping = TextWrapping.WrapWholeWords
    };

    private void LayoutRoot_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyConfigureResponsiveLayout(e.NewSize.Width);
        RequestMarkerLayout();
    }

    private void ApplyConfigureResponsiveLayout(double width)
    {
        bool compact = width < 720d || UsesElevatedTextScale();
        CompatibilityTerminalActions.Orientation = compact
            ? Orientation.Vertical
            : Orientation.Horizontal;
        CompatibilityTerminalActions.HorizontalAlignment = compact
            ? HorizontalAlignment.Stretch
            : HorizontalAlignment.Center;
        CompatibilityForwardActions.Orientation = compact
            ? Orientation.Vertical
            : Orientation.Horizontal;
        CompatibilityForwardActions.HorizontalAlignment = compact
            ? HorizontalAlignment.Stretch
            : HorizontalAlignment.Center;
        BtnCompatibilityBack.HorizontalAlignment = compact
            ? HorizontalAlignment.Stretch
            : HorizontalAlignment.Center;
        BtnCompatibilitySecondaryForward.HorizontalAlignment = compact
            ? HorizontalAlignment.Stretch
            : HorizontalAlignment.Center;
        BtnCompatibilityPrimary.HorizontalAlignment = compact
            ? HorizontalAlignment.Stretch
            : HorizontalAlignment.Center;
        CompatibilityConfigureActions.Orientation = compact
            ? Orientation.Vertical
            : Orientation.Horizontal;
        CompatibilityConfigureActions.HorizontalAlignment = compact
            ? HorizontalAlignment.Stretch
            : HorizontalAlignment.Center;
        BtnCompatibilityConfigureBack.HorizontalAlignment = compact
            ? HorizontalAlignment.Stretch
            : HorizontalAlignment.Center;
        BtnCompatibilityConfigurePrimary.HorizontalAlignment = compact
            ? HorizontalAlignment.Stretch
            : HorizontalAlignment.Center;
        CompatibilitySelectedTradeoffSummary.ColumnDefinitions[1].Width =
            compact ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        Grid.SetColumn(CompatibilityExpectedQualityGroup, compact ? 0 : 1);
        Grid.SetRow(CompatibilityExpectedQualityGroup, compact ? 1 : 0);
        CompatibilitySelectedSetupSummary.ColumnDefinitions[1].Width =
            compact ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        CompatibilitySelectedSetupSummary.ColumnDefinitions[2].Width =
            compact ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        Grid.SetColumn(CompatibilitySelectedCacheGroup, compact ? 0 : 1);
        Grid.SetRow(CompatibilitySelectedCacheGroup, compact ? 1 : 0);
        Grid.SetColumn(CompatibilitySelectedContextGroup, compact ? 0 : 2);
        Grid.SetRow(CompatibilitySelectedContextGroup, compact ? 2 : 0);
    }

    private static bool UsesElevatedTextScale()
    {
        try
        {
            return new Windows.UI.ViewManagement.UISettings().TextScaleFactor >= 1.5d;
        }
        catch
        {
            return false;
        }
    }

    private void MarkerLabel_SizeChanged(object sender, SizeChangedEventArgs e) =>
        RequestMarkerLayout();

    private void RequestMarkerLayout()
    {
        if (!_markerLayoutLoaded || _markerLayoutPending)
        {
            return;
        }

        _markerLayoutPending = true;
        long revision = _markerEvidenceRevision;
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            if (!_markerLayoutLoaded)
            {
                _markerLayoutPending = false;
                return;
            }

            if (revision != _markerEvidenceRevision)
            {
                _markerLayoutPending = false;
                RequestMarkerLayout();
                return;
            }

            double trackWidth = CompatibilityBudgetTrack.ActualWidth;
            double? availableMarkerRatio = _availableMarkerRatio;
            double? minimumMarkerRatio = _minimumMarkerRatio;
            ApplyMarkerLabelLayout(
                CompatibilityAvailableRamLabel,
                CompatibilityAvailableRamLabelTransform,
                CompatibilityAvailableRamElbow,
                availableMarkerRatio,
                trackWidth);
            ApplyMarkerLabelLayout(
                CompatibilityMinimumRamLabel,
                CompatibilityMinimumRamLabelTransform,
                CompatibilityMinimumRamElbow,
                minimumMarkerRatio,
                trackWidth);
            _markerLayoutPending = false;
        }))
        {
            _markerLayoutPending = false;
        }
    }

    private void ApplyMarkerLabelLayout(
        TextBlock label,
        TranslateTransform transform,
        Line elbow,
        double? ratio,
        double trackWidth)
    {
        if (ratio is not { } markerRatio ||
            label.Visibility != Visibility.Visible ||
            trackWidth <= 0)
        {
            transform.X = 0;
            elbow.Visibility = Visibility.Collapsed;
            return;
        }

        label.MaxWidth = Math.Min(360d, trackWidth);
        label.Measure(new Windows.Foundation.Size(label.MaxWidth, double.PositiveInfinity));
        double labelWidth = Math.Min(label.DesiredSize.Width, trackWidth);
        double markerX = trackWidth * Math.Clamp(markerRatio, 0d, 1d);
        double labelLeft = Math.Clamp(
            markerX - labelWidth,
            0d,
            Math.Max(0d, trackWidth - labelWidth));
        transform.X = labelLeft;

        double labelRight = labelLeft + labelWidth;
        if (Math.Abs(labelRight - markerX) <= 0.5d)
        {
            elbow.Visibility = Visibility.Collapsed;
            return;
        }

        elbow.Width = trackWidth;
        elbow.Height = 12d;
        elbow.X1 = markerX;
        elbow.X2 = labelRight;
        elbow.Y1 = 4d;
        elbow.Y2 = 4d;
        elbow.Visibility = Visibility.Visible;
    }

    private static string? FindFact(IReadOnlyList<CompatibilityFact> facts, string label) =>
        facts.FirstOrDefault(f => f.Label.Contains(label, StringComparison.OrdinalIgnoreCase))?.Value;

    private static string? FindRuntime(IReadOnlyList<CompatibilityRow> rows, string label)
    {
        CompatibilityRow? row = rows.FirstOrDefault(
            value => value.Title.Contains(label, StringComparison.OrdinalIgnoreCase));
        return row is null ? null : string.IsNullOrWhiteSpace(row.Value) ? row.Subtitle : row.Value;
    }

    private (Brush surface, Brush border, Brush accent) Tone(CompatibilityOutcomeTone tone) =>
        tone switch
        {
            CompatibilityOutcomeTone.Positive => (Brush("CompatibilitySuccessSurfaceBrush"), Brush("CompatibilitySuccessBorderBrush"), Brush("CompatibilitySuccessBrush")),
            CompatibilityOutcomeTone.Caution => (Brush("CompatibilityWarningSurfaceBrush"), Brush("CompatibilityWarningBorderBrush"), Brush("CompatibilityWarningBrush")),
            CompatibilityOutcomeTone.Blocking => (Brush("CompatibilityErrorSurfaceBrush"), Brush("CompatibilityErrorBorderBrush"), Brush("CompatibilityErrorBrush")),
            _ => (Brush("CompatibilityAccentSurfaceBrush"), Brush("CompatibilityAccentBorderBrush"), Brush("CompatibilityAccentBrush"))
        };

    private Brush Brush(string key) => CompatibilityResources.Brush(this, key);
}
