using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GraniteEdgeAI.Features.ModelInspection.Controls;

public sealed partial class InspectionContentCard : UserControl
{
    private bool _isInitialized;
    private bool _isDisclosureAttached;
    private readonly Dictionary<
        InspectionContentItemPresentation,
        ProgressMotionTargets> _progressMotionTargets = [];
    private readonly Dictionary<UIElement, InspectionContentItemPresentation>
        _progressRowsByElement = [];
    private IReadOnlyList<InspectionContentItemPresentation> _expandedItems =
        Array.Empty<InspectionContentItemPresentation>();
    private InspectionProgressRows? _progressRowsOwner;
    private XamlRoot? _observedXamlRoot;
    private string? _responsiveStateName;

    public static readonly DependencyProperty PresentationProperty =
        DependencyProperty.Register(
            nameof(Presentation),
            typeof(InspectionContentCardPresentation),
            typeof(InspectionContentCard),
            new PropertyMetadata(
                InspectionContentCardPresentation.Hidden,
                OnPresentationChanged));

    public static readonly DependencyProperty CardVisibilityProperty =
        DependencyProperty.Register(
            nameof(CardVisibility),
            typeof(Visibility),
            typeof(InspectionContentCard),
            new PropertyMetadata(Visibility.Collapsed));

    public InspectionContentCard()
    {
        InitializeComponent();
        Presentation = InspectionContentCardPresentation.Hidden;
        _isInitialized = true;
        Loaded += Root_Loaded;
        Unloaded += Root_Unloaded;
        AttachDisclosure();
        ApplyResponsiveState("WideContentState");
        ApplyPresentation(Presentation);
    }

    internal event EventHandler<InspectionDisclosureToggleRequestedEventArgs>?
        DisclosureToggleRequested;

    public InspectionContentCardPresentation Presentation
    {
        get => (InspectionContentCardPresentation)GetValue(PresentationProperty);
        set => SetValue(
            PresentationProperty,
            value ?? InspectionContentCardPresentation.Hidden);
    }

    public Visibility CardVisibility
    {
        get => (Visibility)GetValue(CardVisibilityProperty);
        private set => SetValue(CardVisibilityProperty, value);
    }

    public IReadOnlyList<InspectionContentItemPresentation> ExpandedItems =>
        _expandedItems;

    internal InspectionDisclosure? ActiveDisclosure =>
        CardVisibility == Visibility.Visible &&
        Presentation.DisclosureVisibility == Visibility.Visible
            ? FindingsDisclosure
            : null;

    /// <summary>
    /// Selects the production page-owned two-phase disclosure path. Direct
    /// standalone controls retain their synchronous compatibility path.
    /// </summary>
    internal bool IsDisclosureStateExternallyOwned { get; set; }

    internal void PrepareDisclosureTarget(bool isExpanded) =>
        FindingsDisclosure.PrepareTargetState(isExpanded);

    internal void CompleteDisclosureTarget(bool isExpanded) =>
        FindingsDisclosure.CompleteTargetState(isExpanded);

    internal void ClaimDisclosureTarget(bool isExpanded) =>
        FindingsDisclosure.ClaimTargetState(isExpanded);

    internal void RollbackDisclosureTargetClaim(bool isExpanded) =>
        FindingsDisclosure.RollbackTargetStateClaim(isExpanded);

    internal int LiveRegionChangeNotificationCount { get; private set; }

    internal void AnnounceProgress(string automationName)
    {
        ValidateAnnouncement(automationName, nameof(automationName));
        if (Presentation.Mode != InspectionContentCardMode.Progress ||
            CardVisibility != Visibility.Visible)
        {
            return;
        }

        AutomationProperties.SetName(this, automationName);
        RaiseLiveRegionChanged();
    }

    internal void AnimateProgressChanges(
        InspectionProgressRowsApplyResult changes,
        IModelInspectionAnimationDriver driver,
        ModelInspectionVisualOperationKey operationKey,
        Func<ModelInspectionVisualOperationKey, bool> isCurrent)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentNullException.ThrowIfNull(isCurrent);
        if (changes.IsEmpty || !isCurrent(operationKey))
        {
            return;
        }

        IReadOnlyList<InspectionContentItemPresentation> rows =
            Presentation.ProgressRows.Items;
        foreach (InspectionProgressRowChange change in changes.RowChanges)
        {
            if (change.RowIndex >= rows.Count ||
                !_progressMotionTargets.TryGetValue(
                    rows[change.RowIndex],
                    out ProgressMotionTargets? targets) ||
                targets is null)
            {
                continue;
            }

            if (change.StatusChanged && isCurrent(operationKey))
            {
                driver.StartStageStatus(
                    targets.Status,
                    operationKey,
                    completedKey => _ = isCurrent(completedKey));
            }

            if (change.DetailChanged &&
                rows[change.RowIndex].IsActive &&
                rows[change.RowIndex].DetailVisibility == Visibility.Visible &&
                isCurrent(operationKey))
            {
                driver.StartActiveDetail(
                    targets.Detail,
                    operationKey,
                    completedKey => _ = isCurrent(completedKey));
            }
        }
    }

    internal void CancelProgressMotion()
    {
        foreach (ProgressMotionTargets targets in _progressMotionTargets.Values)
        {
            StopProgressMotion(targets);
        }
    }

    public static Visibility GetPassedVisibility(InspectionContentStatus status) =>
        StatusVisibility(status, InspectionContentStatus.Passed);

    public static Visibility GetWarningVisibility(InspectionContentStatus status) =>
        StatusVisibility(status, InspectionContentStatus.Warning);

    public static Visibility GetErrorVisibility(InspectionContentStatus status) =>
        StatusVisibility(status, InspectionContentStatus.Error);

    public static Visibility GetInformationVisibility(InspectionContentStatus status) =>
        StatusVisibility(status, InspectionContentStatus.Information);

    public static Visibility GetNeutralVisibility(InspectionContentStatus status) =>
        StatusVisibility(status, InspectionContentStatus.Neutral);

    public static Visibility GetActiveVisibility(InspectionContentStatus status) =>
        StatusVisibility(status, InspectionContentStatus.Active);

    public static Visibility GetWaitingVisibility(InspectionContentStatus status) =>
        StatusVisibility(status, InspectionContentStatus.Waiting);

    public static Symbol GetStatusSymbol(InspectionContentStatus status) =>
        status switch
        {
            InspectionContentStatus.Passed => Symbol.Accept,
            InspectionContentStatus.Warning => Symbol.Important,
            InspectionContentStatus.Error => Symbol.Cancel,
            InspectionContentStatus.Active => Symbol.Clock,
            InspectionContentStatus.Information => Symbol.Help,
            InspectionContentStatus.Waiting => Symbol.Clock,
            _ => Symbol.Help
        };

    public static Visibility GetConnectorVisibility(bool showConnector) =>
        showConnector ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility GetTerminalMarkerVisibility(
        InspectionContentStatus status) =>
        status is InspectionContentStatus.Passed or
            InspectionContentStatus.Warning or
            InspectionContentStatus.Error or
            InspectionContentStatus.Information
                ? Visibility.Visible
                : Visibility.Collapsed;

    public static bool IsProgressIndeterminate(double? stageFraction) =>
        !stageFraction.HasValue;

    public static double GetProgressPercent(double? stageFraction) =>
        stageFraction.GetValueOrDefault() * 100d;

    public static string GetDisclosureText(
        string collapsedText,
        string expandedText,
        bool isExpanded) =>
        isExpanded ? expandedText : collapsedText;

    public static Visibility GetTechnicalHelpVisibility(
        InspectionContentCardPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        return presentation.TechnicalDetailsVisibility == Visibility.Visible &&
            !presentation.IsTechnicalDetailsEnabled &&
            !string.IsNullOrWhiteSpace(
                presentation.TechnicalDetailsAutomationHelpText)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
    }

    public static string GetTechnicalHelpAutomationName(
        InspectionContentCardPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        return string.IsNullOrWhiteSpace(
            presentation.TechnicalDetailsAutomationHelpText)
                ? string.Empty
                : $"{presentation.TechnicalDetailsAutomationName}. " +
                  presentation.TechnicalDetailsAutomationHelpText;
    }

    private static Visibility StatusVisibility(
        InspectionContentStatus actual,
        InspectionContentStatus expected) =>
        actual == expected ? Visibility.Visible : Visibility.Collapsed;

    private static void OnPresentationChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArguments)
    {
        var control = (InspectionContentCard)dependencyObject;
        var presentation =
            eventArguments.NewValue as InspectionContentCardPresentation
            ?? InspectionContentCardPresentation.Hidden;
        var previous =
            eventArguments.OldValue as InspectionContentCardPresentation
            ?? InspectionContentCardPresentation.Hidden;
        control.UpdateExpandedItems(previous, presentation);
        control.ApplyPresentation(presentation);
    }

    private void UpdateExpandedItems(
        InspectionContentCardPresentation previous,
        InspectionContentCardPresentation current)
    {
        if (CanRetainExpandedItems(previous, current))
        {
            return;
        }

        _expandedItems = current.ExpandedItems;
    }

    private static bool CanRetainExpandedItems(
        InspectionContentCardPresentation previous,
        InspectionContentCardPresentation current)
    {
        if (previous.Mode != current.Mode ||
            current.Mode is not (
                InspectionContentCardMode.Warnings or
                InspectionContentCardMode.ConversionRequired or
                InspectionContentCardMode.Invalid) ||
            previous.DisclosureVisibility != Visibility.Visible ||
            current.DisclosureVisibility != Visibility.Visible ||
            previous.ExpandedItems.Count != current.ExpandedItems.Count)
        {
            return false;
        }

        for (int index = 0; index < previous.ExpandedItems.Count; index++)
        {
            InspectionContentItemPresentation oldItem =
                previous.ExpandedItems[index];
            InspectionContentItemPresentation newItem =
                current.ExpandedItems[index];
            if (oldItem.Status != newItem.Status ||
                oldItem.StageNumber != newItem.StageNumber ||
                oldItem.ShowConnector != newItem.ShowConnector ||
                oldItem.StageFraction != newItem.StageFraction ||
                !string.Equals(
                    oldItem.Title,
                    newItem.Title,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    oldItem.Detail,
                    newItem.Detail,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    oldItem.StatusText,
                    newItem.StatusText,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    oldItem.AutomationName,
                    newItem.AutomationName,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private void ApplyPresentation(
        InspectionContentCardPresentation presentation)
    {
        CardVisibility = presentation.Mode == InspectionContentCardMode.Hidden
            ? Visibility.Collapsed
            : Visibility.Visible;

        if (!_isInitialized)
        {
            return;
        }

        Bindings.Update();

        if (presentation.Mode == InspectionContentCardMode.Progress &&
            !ReferenceEquals(_progressRowsOwner, presentation.ProgressRows))
        {
            CancelProgressMotion();
            _progressMotionTargets.Clear();
            _progressRowsByElement.Clear();
            _progressRowsOwner = presentation.ProgressRows;
        }

        if (CardVisibility == Visibility.Visible)
        {
            ApplyVisualState(
                presentation.Mode == InspectionContentCardMode.Progress
                    ? "ProgressState"
                    : "FindingsState");
        }

        bool disclosureVisible =
            presentation.DisclosureVisibility == Visibility.Visible;
        bool disclosureTarget = disclosureVisible && presentation.IsExpanded;
        if (!IsDisclosureStateExternallyOwned)
        {
            FindingsDisclosure.PrepareTargetState(disclosureTarget);
            FindingsDisclosure.CompleteTargetState(disclosureTarget);
        }
        ContentCardShell.MinHeight = GetStandardMinimumHeight(presentation);
        ExpandedReportViewport.Height = GetStandardViewportHeight(presentation);

        bool isActiveProgress = CardVisibility == Visibility.Visible &&
            presentation.Mode == InspectionContentCardMode.Progress;
        AutomationProperties.SetLiveSetting(
            this,
            isActiveProgress
                ? AutomationLiveSetting.Polite
                : AutomationLiveSetting.Off);

        if (isActiveProgress)
        {
            InspectionContentItemPresentation? current = presentation.Items
                .LastOrDefault(item =>
                    item.Status != InspectionContentStatus.Waiting);
            string automationName = current is null
                ? $"{presentation.SectionTitle}. {presentation.ProgressSummary}"
                : $"{presentation.SectionTitle}. " +
                  $"{presentation.ProgressSummary}. {current.AutomationName}";
            AutomationProperties.SetName(this, automationName);
            RegisterRealizedProgressRows();
        }
        else
        {
            string automationName =
                CardVisibility == Visibility.Visible &&
                !string.IsNullOrWhiteSpace(presentation.SectionTitle)
                    ? presentation.SectionTitle
                    : "Model inspection progress and findings";
            AutomationProperties.SetName(this, automationName);
        }
    }

    private static double GetStandardMinimumHeight(
        InspectionContentCardPresentation presentation) =>
        presentation.Mode switch
        {
            InspectionContentCardMode.Warnings =>
                presentation.IsExpanded ? 380d : 232d,
            InspectionContentCardMode.ConversionRequired =>
                presentation.IsExpanded ? 365d : 232d,
            InspectionContentCardMode.IncompletePackage => 232d,
            InspectionContentCardMode.Unsupported => 248d,
            InspectionContentCardMode.Invalid =>
                presentation.IsExpanded ? 380d : 248d,
            InspectionContentCardMode.Cancelled => 232d,
            InspectionContentCardMode.OperationalFailure => 248d,
            _ => 0d
        };

    private static double GetStandardViewportHeight(
        InspectionContentCardPresentation presentation) =>
        presentation.Mode switch
        {
            InspectionContentCardMode.ConversionRequired or
            InspectionContentCardMode.Invalid => 113d,
            _ => 128d
        };

    private void FindingsDisclosure_ToggleRequested(
        object? sender,
        InspectionDisclosureToggleRequestedEventArgs eventArguments)
    {
        if (!ReferenceEquals(sender, ActiveDisclosure))
        {
            return;
        }

        DisclosureToggleRequested?.Invoke(this, eventArguments);
    }

    private void ProgressItemsRepeater_ElementPrepared(
        ItemsRepeater sender,
        ItemsRepeaterElementPreparedEventArgs eventArguments)
    {
        RegisterProgressElement(eventArguments.Element, eventArguments.Index);
    }

    private void RegisterRealizedProgressRows()
    {
        if (Presentation.Mode != InspectionContentCardMode.Progress)
        {
            return;
        }

        int rowCount = Presentation.ProgressRows.Items.Count;
        for (int index = 0; index < rowCount; index++)
        {
            if (ProgressItemsRepeater.TryGetElement(index) is UIElement element)
            {
                RegisterProgressElement(element, index);
            }
        }
    }

    private void RegisterProgressElement(UIElement element, int rowIndex)
    {
        IReadOnlyList<InspectionContentItemPresentation> rows =
            Presentation.ProgressRows.Items;
        if (rowIndex < 0 ||
            rowIndex >= rows.Count ||
            element is not FrameworkElement rowElement ||
            rowElement.DataContext is InspectionContentItemPresentation dataRow &&
                !ReferenceEquals(dataRow, rows[rowIndex]) ||
            rowElement.FindName("ProgressStatusMotionTarget") is not UIElement status ||
            rowElement.FindName("ProgressDetailMotionTarget") is not UIElement detail)
        {
            return;
        }

        InspectionContentItemPresentation row = rows[rowIndex];
        if (_progressRowsByElement.TryGetValue(element, out var currentRow) &&
            ReferenceEquals(currentRow, row))
        {
            return;
        }

        if (_progressRowsByElement.Remove(
                element,
                out InspectionContentItemPresentation? replacedRow) &&
            !ReferenceEquals(replacedRow, row) &&
            _progressMotionTargets.Remove(
                replacedRow,
                out ProgressMotionTargets? replacedTargets) &&
            replacedTargets is not null)
        {
            StopProgressMotion(replacedTargets);
        }

        if (_progressMotionTargets.Remove(
                row,
                out ProgressMotionTargets? previous) &&
            previous is not null)
        {
            StopProgressMotion(previous);
        }

        _progressRowsByElement[element] = row;
        _progressMotionTargets[row] = new ProgressMotionTargets(status, detail);
    }

    private void ProgressItemsRepeater_ElementClearing(
        ItemsRepeater sender,
        ItemsRepeaterElementClearingEventArgs eventArguments)
    {
        if (!_progressRowsByElement.Remove(
                eventArguments.Element,
                out InspectionContentItemPresentation? row) ||
            !_progressMotionTargets.Remove(
                row,
                out ProgressMotionTargets? targets) ||
            targets is null)
        {
            return;
        }

        StopProgressMotion(targets);
    }

    private static void StopProgressMotion(ProgressMotionTargets targets)
    {
        ElementCompositionPreview.GetElementVisual(targets.Status)
            .StopAnimation("Opacity");
        var detailVisual = ElementCompositionPreview.GetElementVisual(
            targets.Detail);
        detailVisual.StopAnimation("Opacity");
        ElementCompositionPreview.SetIsTranslationEnabled(
            targets.Detail,
            true);
        detailVisual.StopAnimation("Translation.Y");
    }

    private static void ValidateAnnouncement(
        string automationName,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(automationName);
        string projected = ModelInspectionDisplayTextPolicy.ProjectRequiredDetail(
            automationName,
            "Inspection update unavailable.");
        if (!string.Equals(projected, automationName, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Announcement text must already be bounded display-safe text.",
                parameterName);
        }
    }

    private void AttachDisclosure()
    {
        if (_isDisclosureAttached)
        {
            return;
        }

        FindingsDisclosure.ToggleRequested +=
            FindingsDisclosure_ToggleRequested;
        _isDisclosureAttached = true;
    }

    private void DetachDisclosure()
    {
        if (!_isDisclosureAttached)
        {
            return;
        }

        FindingsDisclosure.ToggleRequested -=
            FindingsDisclosure_ToggleRequested;
        _isDisclosureAttached = false;
    }

    private void RaiseLiveRegionChanged()
    {
        AutomationPeer peer =
            FrameworkElementAutomationPeer.FromElement(this) ??
            FrameworkElementAutomationPeer.CreatePeerForElement(this);
        peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        LiveRegionChangeNotificationCount++;
    }

    private void LayoutRoot_SizeChanged(
        object sender,
        SizeChangedEventArgs eventArguments)
    {
        ApplyResponsiveLayout(
            XamlRoot?.Size.Width ?? eventArguments.NewSize.Width);
    }

    private void Root_Loaded(object sender, RoutedEventArgs eventArguments)
    {
        AttachDisclosure();
        if (!ReferenceEquals(_observedXamlRoot, XamlRoot))
        {
            DetachXamlRoot();
            _observedXamlRoot = XamlRoot;
            if (_observedXamlRoot is not null)
            {
                _observedXamlRoot.Changed += XamlRoot_Changed;
            }
        }

        ApplyResponsiveLayout(
            _observedXamlRoot?.Size.Width ?? ActualWidth);
        RegisterRealizedProgressRows();
    }

    private void Root_Unloaded(object sender, RoutedEventArgs eventArguments)
    {
        CancelProgressMotion();
        _progressMotionTargets.Clear();
        _progressRowsByElement.Clear();
        _progressRowsOwner = null;
        DetachDisclosure();
        DetachXamlRoot();
    }

    private void XamlRoot_Changed(
        XamlRoot sender,
        XamlRootChangedEventArgs eventArguments)
    {
        ApplyResponsiveLayout(sender.Size.Width);
    }

    private void DetachXamlRoot()
    {
        if (_observedXamlRoot is null)
        {
            return;
        }

        _observedXamlRoot.Changed -= XamlRoot_Changed;
        _observedXamlRoot = null;
    }

    private void ApplyResponsiveLayout(double width)
    {
        ApplyResponsiveState(width >= 888d
            ? "WideContentState"
            : width >= 600d
                ? "MediumContentState"
                : "NarrowContentState");
    }

    private void ApplyResponsiveState(string stateName)
    {
        if (!_isInitialized ||
            string.Equals(_responsiveStateName, stateName, StringComparison.Ordinal))
        {
            return;
        }

        ApplyVisualState(stateName);
        _responsiveStateName = stateName;
    }

    private void ApplyVisualState(string stateName)
    {
        if (!VisualStateManager.GoToState(this, stateName, false))
        {
            throw new InvalidOperationException(
                $"The content-card visual state '{stateName}' was not found.");
        }
    }

    private sealed record ProgressMotionTargets(
        UIElement Status,
        UIElement Detail);
}
