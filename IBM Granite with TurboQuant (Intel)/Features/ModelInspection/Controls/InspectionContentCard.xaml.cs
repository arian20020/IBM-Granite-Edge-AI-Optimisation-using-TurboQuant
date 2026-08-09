using GraniteEdgeAI.Features.ModelInspection.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;

namespace GraniteEdgeAI.Features.ModelInspection.Controls;

public sealed partial class InspectionContentCard : UserControl
{
    private bool _isInitialized;
    private bool _isDisclosureAttached;
    private string? _lastAnnouncedAutomationName;
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

    internal InspectionDisclosure? ActiveDisclosure =>
        CardVisibility == Visibility.Visible &&
        Presentation.DisclosureVisibility == Visibility.Visible
            ? FindingsDisclosure
            : null;

    internal int LiveRegionChangeNotificationCount { get; private set; }

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
        control.ApplyPresentation(presentation);
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
        FindingsDisclosure.PrepareTargetState(disclosureTarget);
        FindingsDisclosure.CompleteTargetState(disclosureTarget);
        ContentCardShell.MinHeight = GetStandardMinimumHeight(presentation);
        ExpandedReportViewport.Height = GetStandardViewportHeight(presentation);

        if (CardVisibility == Visibility.Visible &&
            presentation.Mode == InspectionContentCardMode.Progress)
        {
            InspectionContentItemPresentation? current = presentation.Items
                .LastOrDefault(item =>
                    item.Status != InspectionContentStatus.Waiting);
            string automationName = current is null
                ? $"{presentation.SectionTitle}. {presentation.ProgressSummary}"
                : $"{presentation.SectionTitle}. " +
                  $"{presentation.ProgressSummary}. {current.AutomationName}";
            AutomationProperties.SetName(this, automationName);

            if (!string.Equals(
                    automationName,
                    _lastAnnouncedAutomationName,
                    StringComparison.Ordinal))
            {
                _lastAnnouncedAutomationName = automationName;
                RaiseLiveRegionChanged();
            }
        }
        else
        {
            _lastAnnouncedAutomationName = null;
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
    }

    private void Root_Unloaded(object sender, RoutedEventArgs eventArguments)
    {
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
}
