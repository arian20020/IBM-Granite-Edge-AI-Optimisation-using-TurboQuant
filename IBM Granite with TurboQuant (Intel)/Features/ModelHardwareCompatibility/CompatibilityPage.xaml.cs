using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Contracts;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility;

/// <summary>
/// Renders one compatibility presentation onto a stable control tree.
///
/// The tree is built once by XAML and never rebuilt; applying a snapshot writes
/// values into the controls that already exist. That is what keeps the page from
/// flickering between states and what lets a later snapshot change one card
/// without disturbing the rest. There is no business logic here — the page shows
/// what it is given and decides nothing.
/// </summary>
internal sealed partial class CompatibilityPage : Page
{
    private CompatibilityPresentation _presentation = CompatibilityPresentation.Empty;
    private bool _applyingOptimization;
    private bool _compactModeRows;
    private bool _isActive;

    public CompatibilityPage()
        : this(new ViewModels.CompatibilityViewModel())
    {
    }

    internal CompatibilityPage(
        Func<CancellationToken, Task<GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation.CompatibilityScreenModel>> evaluator,
        bool continueDestinationAvailable = true)
        : this(new ViewModels.CompatibilityViewModel(
            evaluator,
            continueDestinationAvailable))
    {
    }

    internal CompatibilityPage(
        Func<IReadOnlySet<string>, CancellationToken,
            Task<CompatibilityEvaluation>> evaluator,
        ICompatibilityActionAuthority actionAuthority,
        Func<CompatibilityEvaluation, CurrentModelLaunchHandoff?>
            currentModelHandoffResolver,
        bool continueDestinationAvailable = true,
        TimeProvider? timeProvider = null)
        : this(new ViewModels.CompatibilityViewModel(
            evaluator,
            continueDestinationAvailable,
            actionAuthority: actionAuthority,
            currentModelHandoffResolver: currentModelHandoffResolver,
            timeProvider: timeProvider))
    {
    }

    private CompatibilityPage(ViewModels.CompatibilityViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        Apply(CompatibilityPresentation.Empty);

        ViewModel.PresentationChanged += (_, presentation) => Apply(presentation);
        ViewModel.ContinueRequested += (_, _) => ContinueRequested?.Invoke(this, EventArgs.Empty);
        ViewModel.OptimizationRequested += (_, args) =>
            OptimizationRequested?.Invoke(this, args);
        ViewModel.CurrentModelChatRequested += (_, args) =>
            CurrentModelChatRequested?.Invoke(this, args);
        ViewModel.BackRequested += (_, _) => BackRequested?.Invoke(this, EventArgs.Empty);
        PrimaryAction.Command = ViewModel.ContinueCommand;
        OptionalOptimizationAction.Command = ViewModel.OptionalOptimizationCommand;
        RefreshMemoryAction.Command = ViewModel.RefreshMemoryCommand;
        OpenTaskManagerAction.Command = ViewModel.OpenTaskManagerCommand;
        ViewModel.AuxiliaryStatusChanged += (_, status) => ApplyAuxiliaryStatus(status);

        // One automatic attempt per navigation: arriving here starts the check,
        // because that is the only reason to be on this page.
        Loaded += Page_Loaded;
        Unloaded += Page_Unloaded;
    }

    internal event EventHandler? ContinueRequested;

    internal event EventHandler<OptimizationRequestedEventArgs>?
        OptimizationRequested;

    internal event EventHandler<CurrentModelChatRequestedEventArgs>?
        CurrentModelChatRequested;

    internal event EventHandler? BackRequested;

    /// <summary>
    /// False only when something else is driving what this page shows — the
    /// fixture gallery, which would otherwise have its chosen state immediately
    /// replaced by a real attempt.
    /// </summary>
    internal bool StartAutomatically { get; set; } = true;

    /// <summary>
    /// Owned by the page for the lifetime of one navigation, so a check started
    /// here cannot outlive the screen that asked for it.
    /// </summary>
    internal ViewModels.CompatibilityViewModel ViewModel { get; }

    private async void Page_Loaded(object sender, RoutedEventArgs e) =>
        await ActivateAsync();

    private void Page_Unloaded(object sender, RoutedEventArgs e) => Deactivate();

    /// <summary>
    /// Begins one host lifetime. Repeated Loaded notifications in the same
    /// lifetime are intentionally idempotent.
    /// </summary>
    private async Task ActivateAsync()
    {
        if (_isActive)
        {
            return;
        }

        _isActive = true;
        if (!StartAutomatically)
        {
            return;
        }

        try
        {
            await ViewModel.StartAsync();
        }
        catch
        {
            // StartAsync has already rendered the path-private safe failure.
            // Loaded is async-void at the framework boundary, so the exception
            // must not escape and terminate the application.
        }
    }

    /// <summary>Ends the active host lifetime and rejects every late post.</summary>
    private void Deactivate()
    {
        if (!_isActive)
        {
            return;
        }

        _isActive = false;
        ViewModel.RetireAttempt();
    }

    /// <summary>
    /// Applies a snapshot. Safe to call with the same value twice.
    /// </summary>
    internal void Apply(CompatibilityPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);

        _presentation = presentation;

        PageTitleText.Text = presentation.PageTitle;
        PageLedeText.Text = presentation.PageLede;

        ModelNameText.Text = presentation.ModelName;
        ModelDetailText.Text = presentation.ModelDetail;
        ModelSummaryCard.Visibility =
            string.IsNullOrWhiteSpace(presentation.ModelName)
            && string.IsNullOrWhiteSpace(presentation.ModelDetail)
                ? Visibility.Collapsed
                : Visibility.Visible;

        ApplyMachineMemory(presentation.MachineMemory);
        ApplyOutcome(presentation);
        ApplyFacts(presentation.Facts);
        ApplyBudget(presentation.Budget);
        ApplyEstimateSummary(presentation.EstimateSummary);
        ApplyOptimization(presentation.Optimization);

        RuntimeCardTitleText.Text = presentation.RuntimeCardTitle;
        ApplyRows(RuntimeRows, presentation.RuntimeRows);

        ChecksCardTitleText.Text = presentation.ChecksCardTitle;
        ApplyRows(CheckRows, presentation.CheckRows);

        bool hasFacts = presentation.Facts.Count > 0 ||
            presentation.Budget.Segments.Count > 0 ||
            presentation.EstimateSummary is not null;
        bool hasRuntimeRows = presentation.RuntimeRows.Count > 0;
        bool hasCheckRows = presentation.CheckRows.Count > 0;
        MainFactsCard.Visibility = hasFacts
            ? Visibility.Visible
            : Visibility.Collapsed;
        RuntimeCard.Visibility = hasRuntimeRows
            ? Visibility.Visible
            : Visibility.Collapsed;
        ChecksCard.Visibility = hasCheckRows
            ? Visibility.Visible
            : Visibility.Collapsed;
        SideCardsGrid.Visibility = hasRuntimeRows || hasCheckRows
            ? Visibility.Visible
            : Visibility.Collapsed;
        AssessmentGrid.Visibility = presentation.Optimization is null &&
            (hasFacts || hasRuntimeRows || hasCheckRows)
                ? Visibility.Visible
                : Visibility.Collapsed;

        ApplyRecoveries(presentation.Recoveries, presentation.MemoryRecoveryReason);
        MemoryRecoveryActions.Visibility = presentation.MemoryRecoveryReason
            == CompatibilityMemoryRecoveryReason.SystemMemoryPressure
                ? Visibility.Visible
                : Visibility.Collapsed;

        DisclosureTitleText.Text = presentation.DisclosureTitle;
        DisclosureDetailText.Text = presentation.DisclosureDetail;

        PrimaryAction.Content = presentation.PrimaryActionText;
        PrimaryAction.IsEnabled = presentation.PrimaryActionEnabled;
        SecondaryAction.Content = presentation.SecondaryActionText;
        SecondaryAction.Command = presentation.SecondaryActionKind switch
        {
            CompatibilitySecondaryActionKind.Back => ViewModel.BackCommand,
            CompatibilitySecondaryActionKind.Cancel => ViewModel.CancelCommand,
            CompatibilitySecondaryActionKind.Retry => ViewModel.RetryCommand,
            _ => null
        };
        SecondaryAction.IsEnabled = presentation.SecondaryActionEnabled
            && SecondaryAction.Command is not null;
        OptionalOptimizationAction.Visibility =
            presentation.Optimization is null
            && ViewModel.CanOptimiseFirst
                ? Visibility.Visible
                : Visibility.Collapsed;
        OptionalOptimizationAction.IsEnabled = ViewModel.CanOptimiseFirst;

        if (PageStack.ActualWidth > 0d)
        {
            ApplyResponsiveLayout(PageStack.ActualWidth);
        }

    }

    private void ApplyAuxiliaryStatus(
        ViewModels.CompatibilityAuxiliaryStatus status)
    {
        MemoryRecoveryStatus.Text = status.Message;
        MemoryRecoveryStatus.Visibility = status.Kind
            == ViewModels.CompatibilityAuxiliaryStatusKind.None
                ? Visibility.Collapsed
                : Visibility.Visible;
    }

    private void ApplyMachineMemory(
        CompatibilityMachineMemoryPresentation? memory)
    {
        if (memory is null || memory.Facts.Count != 4)
        {
            MachineMemoryCard.Visibility = Visibility.Collapsed;
            return;
        }

        ApplyMachineMemoryFact(
            memory.Facts[0],
            MachineMemoryInstalledLabel,
            MachineMemoryInstalledValue,
            MachineMemoryInstalledDetail);
        ApplyMachineMemoryFact(
            memory.Facts[1],
            MachineMemoryAvailableLabel,
            MachineMemoryAvailableValue,
            MachineMemoryAvailableDetail);
        ApplyMachineMemoryFact(
            memory.Facts[2],
            MachineMemoryReserveLabel,
            MachineMemoryReserveValue,
            MachineMemoryReserveDetail);
        ApplyMachineMemoryFact(
            memory.Facts[3],
            MachineMemorySafeLabel,
            MachineMemorySafeValue,
            MachineMemorySafeDetail);
        MachineMemoryCard.Visibility = Visibility.Visible;
    }

    private static void ApplyMachineMemoryFact(
        CompatibilityFact fact,
        TextBlock label,
        TextBlock value,
        TextBlock detail)
    {
        label.Text = fact.Label;
        value.Text = fact.Value;
        detail.Text = fact.Detail;
    }

    private void ApplyOptimization(CompatibilityOptimizationPresentation? optimization)
    {
        OptimizationCard.Visibility = optimization is null
            ? Visibility.Collapsed
            : Visibility.Visible;
        if (optimization is null)
        {
            OptimizationModeRows.Children.Clear();
            OptimizationWarningCard.Visibility = Visibility.Collapsed;
            ExperimentalConsentCard.Visibility = Visibility.Collapsed;
            AutomationProperties.SetName(
                OptimizationSlider,
                "Optimisation preference unavailable");
            AutomationProperties.SetHelpText(OptimizationSlider, string.Empty);
            AutomationProperties.SetItemStatus(OptimizationSlider, string.Empty);
            return;
        }

        _applyingOptimization = true;
        try
        {
            OptimizationInstructionText.Text = optimization.Instruction;
            OptimizationSlider.Value = optimization.SliderValue;
            OptimizationSlider.IsEnabled = optimization.IsActionAuthoritative;
            AutomaticChoice.IsEnabled = optimization.IsActionAuthoritative;
            AutomaticSelectedText.Visibility = optimization.IsAutomatic
                ? Visibility.Visible
                : Visibility.Collapsed;

            CompatibilityOptimizationModePresentation automatic =
                optimization.Modes[0];
            AutomaticQualityText.Text = automatic.ExpectedQualityText;

            CurrentWeightText.Text = $"Weights\n{optimization.CurrentWeightFormat}";
            CurrentCacheText.Text = $"Cache\n{optimization.CurrentCacheFormat}";
            CurrentContextText.Text = $"Context\n{optimization.CurrentContextText}";
            CurrentSystemMemoryText.Text =
                "System/shared RAM\n"
                + $"Needs {optimization.CurrentSystemSharedRequirementText} · "
                + $"safe budget {optimization.CurrentSystemSharedBudgetText} · "
                + $"headroom {optimization.CurrentSystemSharedHeadroomText}";
            CurrentDedicatedMemoryText.Text = DedicatedMemory(
                optimization.CurrentDedicatedRequirementText,
                optimization.CurrentDedicatedBudgetText,
                optimization.CurrentDedicatedHeadroomText);

            CompatibilityOptimizationModePresentation selected =
                optimization.SelectedMode;
            RecommendedModeLabelText.Text = selected.Label;
            RecommendedWeightText.Text = $"Weights\n{selected.WeightFormat}";
            RecommendedCacheText.Text = $"Cache\n{selected.CacheFormat}";
            RecommendedContextText.Text = $"Context\n{selected.ContextText}";
            RecommendedSystemMemoryText.Text =
                "System/shared RAM\n"
                + $"Needs {selected.SystemSharedRequirementText} · "
                + $"safe budget {selected.SystemSharedBudgetText} · "
                + $"headroom {selected.SystemSharedHeadroomText}";
            RecommendedDedicatedMemoryText.Text = DedicatedMemory(
                selected.DedicatedRequirementText,
                selected.DedicatedBudgetText,
                selected.DedicatedHeadroomText);
            RecommendedQualityText.Text = selected.ExpectedQualityText;
            OptimizationSliderLabelText.Text = selected.Label;

            ApplyOptimizationAccessibility(optimization, selected);

            ApplyOptimizationModes(optimization);

            OptimizationWarningText.Text = selected.WarningText;
            OptimizationWarningCard.Visibility =
                string.IsNullOrWhiteSpace(selected.WarningText)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            ApplyExperimentalConsent(selected.IsExperimental);
        }
        finally
        {
            _applyingOptimization = false;
        }
    }

    private void ApplyExperimentalConsent(bool selectedModeIsExperimental)
    {
        string? evidenceId = ViewModel.AvailableExperimentalConsentEvidenceId;
        bool available = evidenceId is not null;
        bool granted = available && ViewModel.IsExperimentalConsentGranted;

        ExperimentalConsentCard.Visibility = available
            ? Visibility.Visible
            : Visibility.Collapsed;
        ExperimentalConsentCheckBox.IsChecked = granted;
        ExperimentalFinalConfirmationCard.Visibility =
            selectedModeIsExperimental && granted
                ? Visibility.Visible
                : Visibility.Collapsed;
        ExperimentalFinalConfirmationCheckBox.IsChecked =
            selectedModeIsExperimental
            && ViewModel.CanConfirmExperimentalPlan;
    }

    private static string DedicatedMemory(
        string requirement,
        string budget,
        string headroom) => requirement == "Not used"
            ? "Dedicated VRAM\nNot used"
            : "Dedicated VRAM\n"
                + $"Needs {requirement} · budget {budget} · headroom {headroom}";

    private void ApplyOptimizationModes(CompatibilityOptimizationPresentation optimization)
    {
        CompatibilityOptimizationModePresentation[] modes = optimization.Modes
            .Where(mode => mode.SliderValue.HasValue)
            .ToArray();
        bool sameTree = OptimizationModeRows.Children.Count == modes.Length;
        for (int index = 0; sameTree && index < modes.Length; index++)
        {
            sameTree = OptimizationModeRows.Children[index] is Border border
                && string.Equals(
                    border.Tag as string,
                    ModeIdentity(modes[index]),
                    StringComparison.Ordinal);
        }

        if (sameTree)
        {
            for (int index = 0; index < modes.Length; index++)
            {
                UpdateOptimizationModeRow(
                    (Border)OptimizationModeRows.Children[index],
                    modes[index],
                    string.Equals(
                        modes[index].Label,
                        optimization.SelectedMode.Label,
                        StringComparison.Ordinal));
            }
            return;
        }

        OptimizationModeRows.Children.Clear();

        foreach (CompatibilityOptimizationModePresentation mode in
            modes)
        {
            bool selected = string.Equals(
                mode.Label,
                optimization.SelectedMode.Label,
                StringComparison.Ordinal);
            Grid content = new()
            {
                MinHeight = 44,
                ColumnSpacing = 12,
                VerticalAlignment = VerticalAlignment.Center,
                ColumnDefinitions =
                {
                    new ColumnDefinition
                    {
                        Width = new GridLength(1, GridUnitType.Star)
                    },
                    new ColumnDefinition { Width = GridLength.Auto }
                }
            };
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            StackPanel words = new()
            {
                VerticalAlignment = VerticalAlignment.Center
            };
            words.Children.Add(new TextBlock
            {
                Text = mode.Label,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = Brush("CompatibilityTextPrimaryBrush")
            });
            words.Children.Add(new TextBlock
            {
                Text = mode.ExpectedQualityText,
                Foreground = Brush("CompatibilityTextMutedBrush"),
                TextWrapping = TextWrapping.Wrap
            });
            Grid.SetColumn(words, 0);
            content.Children.Add(words);

            StackPanel trailing = new()
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            trailing.Children.Add(new TextBlock
            {
                Text = $"{mode.WeightFormat} · {mode.CacheFormat}",
                Foreground = selected
                    ? Brush("CompatibilityPrimaryBlueBrush")
                    : Brush("CompatibilityTextMutedBrush"),
                FontWeight = selected
                    ? Microsoft.UI.Text.FontWeights.SemiBold
                    : Microsoft.UI.Text.FontWeights.Normal,
                TextAlignment = TextAlignment.Right,
                TextWrapping = TextWrapping.Wrap
            });
            trailing.Children.Add(new TextBlock
            {
                Text = mode.HasStrongQualityWarning
                    ? "Strong quality warning"
                    : mode.IsExperimental ? "Experimental opt-in" : string.Empty,
                Foreground = mode.HasStrongQualityWarning
                    ? Brush("CompatibilityErrorTextBrush")
                    : Brush("CompatibilityWarningAccentBrush"),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextAlignment = TextAlignment.Right,
                TextWrapping = TextWrapping.Wrap
            });
            Grid.SetColumn(trailing, _compactModeRows ? 0 : 1);
            Grid.SetRow(trailing, _compactModeRows ? 1 : 0);
            trailing.HorizontalAlignment = _compactModeRows
                ? HorizontalAlignment.Left
                : HorizontalAlignment.Right;
            content.Children.Add(trailing);

            Border root = new()
            {
                Tag = ModeIdentity(mode),
                Background = selected
                    ? Brush("CompatibilityBlueSurfaceBrush")
                    : Brush("CompatibilityCanvasBrush"),
                BorderBrush = selected
                    ? Brush("CompatibilityBlueBorderBrush")
                    : Brush("CompatibilityBorderLightBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = Radius("CompatibilityFactRadius"),
                Padding = new Thickness(14, 6, 14, 6),
                Child = content
            };
            OptimizationModeRows.Children.Add(root);
        }
    }

    private void UpdateOptimizationModeRow(
        Border root,
        CompatibilityOptimizationModePresentation mode,
        bool selected)
    {
        Grid content = (Grid)root.Child;
        StackPanel words = (StackPanel)content.Children[0];
        StackPanel trailing = (StackPanel)content.Children[1];
        ((TextBlock)words.Children[0]).Text = mode.Label;
        ((TextBlock)words.Children[1]).Text = mode.ExpectedQualityText;
        TextBlock setup = (TextBlock)trailing.Children[0];
        setup.Text = $"{mode.WeightFormat} \u00B7 {mode.CacheFormat}";
        setup.Foreground = selected
            ? Brush("CompatibilityPrimaryBlueBrush")
            : Brush("CompatibilityTextMutedBrush");
        setup.FontWeight = selected
            ? Microsoft.UI.Text.FontWeights.SemiBold
            : Microsoft.UI.Text.FontWeights.Normal;
        TextBlock status = (TextBlock)trailing.Children[1];
        status.Text = mode.HasStrongQualityWarning
            ? "Strong quality warning"
            : mode.IsExperimental ? "Experimental opt-in" : string.Empty;
        status.Foreground = mode.HasStrongQualityWarning
            ? Brush("CompatibilityErrorTextBrush")
            : Brush("CompatibilityWarningAccentBrush");
        Grid.SetColumn(trailing, _compactModeRows ? 0 : 1);
        Grid.SetRow(trailing, _compactModeRows ? 1 : 0);
        trailing.HorizontalAlignment = _compactModeRows
            ? HorizontalAlignment.Left
            : HorizontalAlignment.Right;
        root.Background = selected
            ? Brush("CompatibilityBlueSurfaceBrush")
            : Brush("CompatibilityCanvasBrush");
        root.BorderBrush = selected
            ? Brush("CompatibilityBlueBorderBrush")
            : Brush("CompatibilityBorderLightBrush");
    }

    private void ApplyOptimizationAccessibility(
        CompatibilityOptimizationPresentation optimization,
        CompatibilityOptimizationModePresentation selected)
    {
        string choice = optimization.IsAutomatic
            ? $"Automatic ({selected.Label})"
            : selected.Label;
        string release = selected.IsExperimental ? "Experimental opt-in" : "Released";
        string warning = selected.HasStrongQualityWarning
            ? $" Strong quality warning. {selected.WarningText}"
            : string.Empty;
        AutomationProperties.SetName(
            OptimizationSlider,
            $"Optimisation preference: {choice}");
        AutomationProperties.SetHelpText(
            OptimizationSlider,
            $"Selected band: {selected.Label}. "
                + $"{selected.ExpectedQualityText}. {release}.{warning}");
        AutomationProperties.SetItemStatus(
            OptimizationSlider,
            optimization.IsAutomatic
                ? $"Automatic. Recommended band: {selected.Label}. {release}."
                : $"{selected.Label}. {release}.");
        AutomationProperties.SetLiveSetting(
            OptimizationSlider,
            AutomationLiveSetting.Polite);
    }

    private static string ModeIdentity(
        CompatibilityOptimizationModePresentation mode) =>
        $"{mode.Label}|{mode.SliderValue}";


    private void PageStack_SizeChanged(object sender, SizeChangedEventArgs args) =>
        ApplyResponsiveLayout(args.NewSize.Width);

    private void ContentHost_SizeChanged(object sender, SizeChangedEventArgs args) =>
        ApplyViewportWidth(args.NewSize.Width);

    private void ApplyViewportWidth(double viewportWidth)
    {
        double availableWidth = Math.Max(
            0d,
            viewportWidth - ContentHost.Padding.Left - ContentHost.Padding.Right);
        if (availableWidth <= 0d)
        {
            return;
        }

        PageStack.Width = Math.Min(PageStack.MaxWidth, availableWidth);
        ApplyResponsiveLayout(PageStack.Width);
    }

    private void ApplyResponsiveLayout(double availableWidth)
    {
        ApplyMachineMemoryLayout(availableWidth < 760d);
        ApplyAssessmentLayout(availableWidth < 900d);

        bool stackOutcome = availableWidth < 760d;
        Grid.SetColumn(OutcomeBadgeHost, stackOutcome ? 1 : 2);
        Grid.SetRow(OutcomeBadgeHost, stackOutcome ? 1 : 0);
        OutcomeBadgeHost.HorizontalAlignment = stackOutcome
            ? HorizontalAlignment.Left
            : HorizontalAlignment.Right;

        bool stackSetups = availableWidth < 900d;
        OptimizationSetupGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
        OptimizationSetupGrid.ColumnDefinitions[1].Width = stackSetups
            ? new GridLength(0)
            : new GridLength(1, GridUnitType.Star);
        Grid.SetColumn(CurrentSetupCard, 0);
        Grid.SetRow(CurrentSetupCard, 0);
        Grid.SetColumn(RecommendedSetupCard, stackSetups ? 0 : 1);
        Grid.SetRow(RecommendedSetupCard, stackSetups ? 1 : 0);

        bool stackFacts = availableWidth < 620d;
        ApplyAssessmentFactsLayout(stackFacts);
        ApplySetupFactsLayout(
            CurrentSetupFactsGrid,
            CurrentWeightText,
            CurrentCacheText,
            CurrentContextText,
            CurrentSystemMemoryText,
            CurrentDedicatedMemoryText,
            null,
            stackFacts);
        ApplySetupFactsLayout(
            RecommendedSetupFactsGrid,
            RecommendedWeightText,
            RecommendedCacheText,
            RecommendedContextText,
            RecommendedSystemMemoryText,
            RecommendedDedicatedMemoryText,
            RecommendedQualityText,
            stackFacts);

        bool stackLabels = availableWidth < 560d;
        Grid.SetColumn(RecommendedModeLabelText, stackLabels ? 0 : 1);
        Grid.SetRow(RecommendedModeLabelText, stackLabels ? 1 : 0);
        RecommendedModeLabelText.HorizontalAlignment = stackLabels
            ? HorizontalAlignment.Left
            : HorizontalAlignment.Right;
        Grid.SetColumn(AutomaticSelectedText, stackLabels ? 0 : 1);
        Grid.SetRow(AutomaticSelectedText, stackLabels ? 1 : 0);
        AutomaticSelectedText.HorizontalAlignment = stackLabels
            ? HorizontalAlignment.Left
            : HorizontalAlignment.Right;
        Grid.SetColumn(OptimizationSliderLabelText, stackLabels ? 0 : 1);
        Grid.SetRow(OptimizationSliderLabelText, stackLabels ? 1 : 0);
        OptimizationSliderLabelText.HorizontalAlignment = stackLabels
            ? HorizontalAlignment.Left
            : HorizontalAlignment.Right;

        bool stackActions = availableWidth < 520d;
        ActionGrid.HorizontalAlignment = stackActions
            ? HorizontalAlignment.Stretch
            : HorizontalAlignment.Center;
        Grid.SetColumn(SecondaryAction, 0);
        Grid.SetRow(SecondaryAction, 0);
        Grid.SetColumnSpan(SecondaryAction, stackActions ? 2 : 1);
        Grid.SetColumn(PrimaryAction, stackActions ? 0 : 1);
        Grid.SetRow(PrimaryAction, stackActions ? 1 : 0);
        Grid.SetColumnSpan(PrimaryAction, stackActions ? 2 : 1);

        bool compactModes = availableWidth < 620d;
        if (_compactModeRows != compactModes)
        {
            _compactModeRows = compactModes;
            if (_presentation.Optimization is { } optimization)
            {
                ApplyOptimizationModes(optimization);
            }
        }
    }

    private void ApplyAssessmentLayout(bool stacked)
    {
        MainFactsColumn.Width = new GridLength(
            stacked ? 1d : 1.35d,
            GridUnitType.Star);
        SideFactsColumn.Width = stacked
            ? new GridLength(0)
            : new GridLength(0.85d, GridUnitType.Star);
        Grid.SetColumnSpan(MainFactsCard, stacked ? 2 : 1);
        Grid.SetColumn(SideCardsGrid, stacked ? 0 : 1);
        Grid.SetRow(SideCardsGrid, stacked ? 1 : 0);
        Grid.SetColumnSpan(SideCardsGrid, stacked ? 2 : 1);
        SideCardsGrid.Margin = stacked
            ? new Thickness(0, 14, 0, 0)
            : new Thickness(0);
    }

    private void ApplyAssessmentFactsLayout(bool stacked)
    {
        FactsGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
        FactsGrid.ColumnDefinitions[1].Width = stacked
            ? new GridLength(0)
            : new GridLength(1, GridUnitType.Star);

        for (int index = 0; index < FactsGrid.RowDefinitions.Count; index++)
        {
            FactsGrid.RowDefinitions[index].Height = stacked
                ? GridLength.Auto
                : index < 2
                    ? new GridLength(1, GridUnitType.Star)
                    : new GridLength(0);
        }

        for (int index = 0; index < FactsGrid.Children.Count; index++)
        {
            FrameworkElement tile = (FrameworkElement)FactsGrid.Children[index];
            Grid.SetColumn(tile, stacked ? 0 : index % 2);
            Grid.SetRow(tile, stacked ? index : index / 2);
        }
    }

    private static void ApplySetupFactsLayout(
        Grid grid,
        FrameworkElement weights,
        FrameworkElement cache,
        FrameworkElement context,
        FrameworkElement systemMemory,
        FrameworkElement dedicatedMemory,
        FrameworkElement? quality,
        bool stacked)
    {
        grid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
        grid.ColumnDefinitions[1].Width = stacked
            ? new GridLength(0)
            : new GridLength(1, GridUnitType.Star);
        grid.ColumnDefinitions[2].Width = stacked
            ? new GridLength(0)
            : new GridLength(1, GridUnitType.Star);

        FrameworkElement[] fields = [weights, cache, context, systemMemory, dedicatedMemory];
        if (stacked)
        {
            for (int index = 0; index < fields.Length; index++)
            {
                Grid.SetColumn(fields[index], 0);
                Grid.SetColumnSpan(fields[index], 1);
                Grid.SetRow(fields[index], index);
            }
            if (quality is not null)
            {
                Grid.SetColumn(quality, 0);
                Grid.SetColumnSpan(quality, 1);
                Grid.SetRow(quality, 5);
            }
            return;
        }

        Grid.SetColumn(weights, 0);
        Grid.SetRow(weights, 0);
        Grid.SetColumn(cache, 1);
        Grid.SetRow(cache, 0);
        Grid.SetColumn(context, 2);
        Grid.SetRow(context, 0);
        Grid.SetColumn(systemMemory, 0);
        Grid.SetColumnSpan(systemMemory, 2);
        Grid.SetRow(systemMemory, 1);
        Grid.SetColumn(dedicatedMemory, 2);
        Grid.SetColumnSpan(dedicatedMemory, 1);
        Grid.SetRow(dedicatedMemory, 1);
        if (quality is not null)
        {
            Grid.SetColumn(quality, 0);
            Grid.SetColumnSpan(quality, 3);
            Grid.SetRow(quality, 2);
        }
    }

    private void AutomaticChoice_Click(object sender, RoutedEventArgs e)
    {
        if (!_applyingOptimization)
        {
            ViewModel.SelectAutomaticPreference();
        }
    }

    private void OptimizationSlider_ValueChanged(
        object sender,
        RangeBaseValueChangedEventArgs e)
    {
        if (!_applyingOptimization && OptimizationSlider.IsEnabled)
        {
            ViewModel.SelectManualPreference((int)Math.Round(e.NewValue));
        }
    }

    private async void ExperimentalConsentCheckBox_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_applyingOptimization
            || ViewModel.AvailableExperimentalConsentEvidenceId is not { } evidenceId)
        {
            return;
        }

        bool granted = ExperimentalConsentCheckBox.IsChecked == true;
        bool accepted = await ViewModel.SetExperimentalConsentAsync(
            evidenceId,
            granted);
        if (!accepted)
        {
            _applyingOptimization = true;
            ExperimentalConsentCheckBox.IsChecked =
                ViewModel.IsExperimentalConsentGranted;
            _applyingOptimization = false;
        }
    }

    private void ApplyMachineMemoryLayout(bool compact)
    {
        for (int index = 0; index < MachineMemoryFactsGrid.ColumnDefinitions.Count; index++)
        {
            MachineMemoryFactsGrid.ColumnDefinitions[index].Width =
                !compact || index < 2
                    ? new GridLength(1, GridUnitType.Star)
                    : new GridLength(0);
        }

        MachineMemoryFactsGrid.RowDefinitions[0].Height = GridLength.Auto;
        MachineMemoryFactsGrid.RowDefinitions[1].Height = compact
            ? GridLength.Auto
            : new GridLength(0);

        FrameworkElement[] tiles =
        [
            MachineMemoryInstalledTile,
            MachineMemoryAvailableTile,
            MachineMemoryReserveTile,
            MachineMemorySafeTile
        ];
        for (int index = 0; index < tiles.Length; index++)
        {
            Grid.SetColumn(tiles[index], compact ? index % 2 : index);
            Grid.SetRow(tiles[index], compact ? index / 2 : 0);
        }
    }

    private void ExperimentalFinalConfirmationCheckBox_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!_applyingOptimization)
        {
            ViewModel.SetExperimentalFinalConfirmation(
                ExperimentalFinalConfirmationCheckBox.IsChecked == true);
        }
    }

    private void ApplyEstimateSummary(CompatibilityEstimateSummary? summary)
    {
        EstimateBreakdownCard.Visibility = summary is null
            ? Visibility.Collapsed
            : Visibility.Visible;

        if (summary is null)
        {
            EstimateWeightsValue.Text = EstimateKvCacheValue.Text = string.Empty;
            EstimateRuntimeValue.Text = EstimateMarginValue.Text = string.Empty;
            EstimatePeakValue.Text = EstimateSafeValue.Text = string.Empty;
            return;
        }

        EstimateWeightsValue.Text =
            CompatibilityBudget.Describe(summary.ModelWeightsBytes);
        EstimateKvCacheValue.Text =
            CompatibilityBudget.Describe(summary.KvCacheBytes);
        EstimateRuntimeValue.Text =
            CompatibilityBudget.Describe(summary.RuntimeAndBufferBytes);
        EstimateMarginValue.Text =
            CompatibilityBudget.Describe(summary.MarginForErrorBytes);
        EstimatePeakValue.Text =
            CompatibilityBudget.Describe(summary.EstimatedPeakBytes);
        EstimateSafeValue.Text =
            CompatibilityBudget.Describe(summary.SafeMemoryBytes);
    }

    private void ApplyOutcome(CompatibilityPresentation presentation)
    {
        (Brush surface, Brush border, Brush accent, string glyph) = presentation.Tone switch
        {
            CompatibilityOutcomeTone.Positive => (
                Brush("CompatibilitySuccessSurfaceBrush"),
                Brush("CompatibilitySuccessBorderBrush"),
                Brush("CompatibilitySuccessTextBrush"),
                "✓"),
            CompatibilityOutcomeTone.Caution => (
                Brush("CompatibilityWarningSurfaceBrush"),
                Brush("CompatibilityWarningBorderBrush"),
                Brush("CompatibilityWarningAccentBrush"),
                "!"),
            CompatibilityOutcomeTone.Blocking => (
                Brush("CompatibilityErrorSurfaceBrush"),
                Brush("CompatibilityErrorBorderBrush"),
                Brush("CompatibilityErrorTextBrush"),
                "!"),
            _ => (
                Brush("CompatibilityBlueSurfaceBrush"),
                Brush("CompatibilityBlueBorderBrush"),
                Brush("CompatibilityPrimaryBlueBrush"),
                "i")
        };

        OutcomeCard.Background = surface;
        OutcomeCard.BorderBrush = border;
        OutcomeGlyphHost.Background = accent;
        OutcomeGlyphText.Text = glyph;

        OutcomeTitleText.Text = presentation.OutcomeTitle;
        OutcomeDetailText.Text = presentation.OutcomeDetail;

        OutcomeBadgeText.Text = presentation.OutcomeBadge;
        OutcomeBadgeText.Foreground = accent;
        OutcomeBadgeHost.BorderBrush = border;
        OutcomeBadgeHost.Visibility = string.IsNullOrEmpty(presentation.OutcomeBadge)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void ApplyFacts(IReadOnlyList<CompatibilityFact> facts)
    {
        FactsGrid.Children.Clear();

        for (int index = 0; index < facts.Count && index < 4; index++)
        {
            CompatibilityFact fact = facts[index];

            // The detail sits in its own bottom-aligned row so every tile's
            // detail line rests on the same baseline, whatever the value above
            // it wraps to.
            Grid content = new()
            {
                RowDefinitions =
                {
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
                }
            };

            TextBlock label = new()
            {
                Text = fact.Label.ToUpperInvariant(),
                FontSize = Size("CompatibilityFactLabelFontSize"),
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = Brush("CompatibilityTextMutedBrush"),
                TextTrimming = TextTrimming.None,
                TextWrapping = TextWrapping.Wrap
            };

            TextBlock value = new()
            {
                Text = fact.Value,
                FontSize = Size("CompatibilityFactValueFontSize"),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = Brush("CompatibilityTextPrimaryBrush"),
                Margin = new Thickness(0, 5, 0, 0),
                TextTrimming = TextTrimming.None,
                TextWrapping = TextWrapping.Wrap
            };

            TextBlock detail = new()
            {
                Text = fact.Detail,
                FontSize = Size("CompatibilityFactDetailFontSize"),
                Foreground = Brush("CompatibilityTextMutedBrush"),
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };

            Grid.SetRow(label, 0);
            Grid.SetRow(value, 1);
            Grid.SetRow(detail, 2);
            content.Children.Add(label);
            content.Children.Add(value);
            content.Children.Add(detail);

            Border tile = new()
            {
                Background = Brush("CompatibilityCanvasBrush"),
                BorderBrush = Brush("CompatibilityBorderLightBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = Radius("CompatibilityFactRadius"),
                Padding = Pad("CompatibilityFactPadding"),
                Child = content
            };

            Grid.SetColumn(tile, index % 2);
            Grid.SetRow(tile, index / 2);
            FactsGrid.Children.Add(tile);
        }
    }

    /// <summary>
    /// Shows the memory bar, or hides it when there is nothing to draw.
    ///
    /// Hiding matters more than it looks. A bar with no segments renders as an
    /// empty track, and an empty track beside the words "we could not work this
    /// out" reads as a model that needs no memory at all.
    /// </summary>
    private void ApplyBudget(CompatibilityBudget budget)
    {
        Visibility visibility = budget.Segments.Count == 0
            ? Visibility.Collapsed
            : Visibility.Visible;

        BudgetDiagram.Budget = budget;
        BudgetDiagram.Visibility = visibility;

        BudgetLegend.Budget = budget;
        BudgetLegend.Visibility = visibility;
    }

    private void ApplyRows(Panel host, IReadOnlyList<CompatibilityRow> rows)
    {
        host.Children.Clear();

        foreach (CompatibilityRow row in rows)
        {
            Grid line = new()
            {
                ColumnSpacing = 8,
                Padding = new Thickness(0, 5, 0, 5),
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto }
                }
            };

            StackPanel text = new();

            text.Children.Add(new TextBlock
            {
                Text = row.Title,
                FontSize = Size("CompatibilityRowTitleFontSize"),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = Brush("CompatibilityTextPrimaryBrush"),
                TextTrimming = TextTrimming.None,
                TextWrapping = TextWrapping.Wrap
            });

            if (!string.IsNullOrEmpty(row.Subtitle))
            {
                text.Children.Add(new TextBlock
                {
                    Text = row.Subtitle,
                    FontSize = Size("CompatibilityRowSubFontSize"),
                    Foreground = Brush("CompatibilityTextMutedBrush"),
                    TextTrimming = TextTrimming.None,
                    TextWrapping = TextWrapping.Wrap
                });
            }

            Grid.SetColumn(text, 0);
            line.Children.Add(text);

            FrameworkElement trailing = row.ShowPill
                ? BuildPill(row)
                : new TextBlock
                {
                    Text = row.Value,
                    FontSize = Size("CompatibilityRowTitleFontSize"),
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = Brush("CompatibilityTextPrimaryBrush"),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap
                };

            Grid.SetColumn(trailing, 1);
            line.Children.Add(trailing);

            host.Children.Add(line);
        }
    }

    private Border BuildPill(CompatibilityRow row)
    {
        (Brush surface, Brush border, Brush accent) = row.Tone switch
        {
            CompatibilityOutcomeTone.Positive => (
                Brush("CompatibilitySuccessSurfaceBrush"),
                Brush("CompatibilitySuccessBorderBrush"),
                Brush("CompatibilitySuccessTextBrush")),
            CompatibilityOutcomeTone.Caution => (
                Brush("CompatibilityWarningSurfaceBrush"),
                Brush("CompatibilityWarningBorderBrush"),
                Brush("CompatibilityWarningAccentBrush")),
            CompatibilityOutcomeTone.Blocking => (
                Brush("CompatibilityErrorSurfaceBrush"),
                Brush("CompatibilityErrorBorderBrush"),
                Brush("CompatibilityErrorTextBrush")),
            _ => (
                Brush("CompatibilityBlueSurfaceBrush"),
                Brush("CompatibilityBlueBorderBrush"),
                Brush("CompatibilityPrimaryBlueBrush"))
        };

        return new Border
        {
            Background = surface,
            BorderBrush = border,
            BorderThickness = new Thickness(1),
            CornerRadius = Radius("CompatibilityPillRadius"),
            Padding = new Thickness(7, 4, 7, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = row.Value,
                FontSize = Size("CompatibilityPillFontSize"),
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = accent,
                TextWrapping = TextWrapping.Wrap
            }
        };
    }

    private void ApplyRecoveries(
        IReadOnlyList<CompatibilityRecovery> recoveries,
        CompatibilityMemoryRecoveryReason memoryRecoveryReason)
    {
        RecoveryRows.Children.Clear();

        RecoveryCard.Visibility = recoveries.Count == 0
            && memoryRecoveryReason == CompatibilityMemoryRecoveryReason.None
            ? Visibility.Collapsed
            : Visibility.Visible;

        foreach (CompatibilityRecovery recovery in recoveries)
        {
            StackPanel item = new() { Margin = new Thickness(0, 0, 0, 8) };

            item.Children.Add(new TextBlock
            {
                Text = recovery.Title,
                FontSize = Size("CompatibilityRowTitleFontSize"),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = Brush("CompatibilityTextPrimaryBrush"),
                TextWrapping = TextWrapping.Wrap
            });

            item.Children.Add(new TextBlock
            {
                Text = recovery.Detail,
                FontSize = Size("CompatibilityOutcomeBodyFontSize"),
                Foreground = Brush("CompatibilityTextMutedBrush"),
                TextWrapping = TextWrapping.Wrap
            });

            RecoveryRows.Children.Add(item);
        }
    }

    private Brush Brush(string key) =>
        CompatibilityResources.Brush(this, key);

    private double Size(string key) =>
        CompatibilityResources.Value(this, key, 10d);

    private Thickness Pad(string key) =>
        CompatibilityResources.Value(this, key, new Thickness(8));

    private CornerRadius Radius(string key) =>
        CompatibilityResources.Value(this, key, new CornerRadius(8));
}
