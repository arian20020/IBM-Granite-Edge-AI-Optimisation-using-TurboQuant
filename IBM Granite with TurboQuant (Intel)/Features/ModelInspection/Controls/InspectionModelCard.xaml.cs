using GraniteEdgeAI.Features.ModelInspection.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;

namespace GraniteEdgeAI.Features.ModelInspection.Controls
{
    /// <summary>
    /// Displays the selected model and its inspected metadata.
    /// </summary>
    public sealed partial class InspectionModelCard : UserControl
    {
        private bool _isDisclosureAttached;

        private IReadOnlyList<InspectionCheckPresentation> _inspectionChecks =
            Array.Empty<InspectionCheckPresentation>();

        private string? _responsiveStateName;

        private XamlRoot? _observedXamlRoot;

        // visual states are unavailable until InitializeComponent builds the xaml tree
        private bool _isInitialized;

        /// <summary>
        /// Identifies the bindable presentation dependency property.
        /// </summary>
        public static readonly DependencyProperty PresentationProperty =
            DependencyProperty.Register(
                nameof(Presentation),
                typeof(InspectionModelCardPresentation),
                typeof(InspectionModelCard),
                new PropertyMetadata(
                    InspectionModelCardPresentation.Empty,
                    OnPresentationChanged));

        /// <summary>
        /// Creates the model card and loads its XAML layout.
        /// </summary>
        public InspectionModelCard()
        {
            InitializeComponent();
            _isInitialized = true;
            Loaded += Root_Loaded;
            Unloaded += Root_Unloaded;
            AttachDisclosure();
            ApplyResponsiveState("WideModelState");
            ApplyPresentation(Presentation);
        }

        /// <summary>
        /// Raised only when the retained shared disclosure requests a new target.
        /// </summary>
        internal event EventHandler<InspectionDisclosureToggleRequestedEventArgs>?
            DisclosureToggleRequested;

        /// <summary>
        /// Gets or sets all model-card display data.
        /// </summary>
        public InspectionModelCardPresentation Presentation
        {
            get => (InspectionModelCardPresentation)GetValue(PresentationProperty);
            set => SetValue(
                PresentationProperty,
                value ?? InspectionModelCardPresentation.Empty);
        }

        // read-only forwarding properties keep existing x:Bind expressions concise

        public InspectionModelCardMode DisplayMode => Presentation.DisplayMode;

        public InspectionModelBadgeState BadgeState => Presentation.BadgeState;

        public string ModelName => Presentation.ModelName;

        public string CompactSummary => Presentation.CompactSummary;

        public string FormatShortName => Presentation.FormatShortName;

        public string OverviewFormatBadgeText =>
            Presentation.OverviewFormatBadgeText;

        public string Publisher => Presentation.Publisher;

        public string FormatName => Presentation.FormatName;

        public string Quantisation => Presentation.Quantisation;

        public string ParameterCount => Presentation.ParameterCount;

        public string ModelType => Presentation.ModelType;

        public string DeclaredContext => Presentation.DeclaredContext;

        public string FileSize => Presentation.FileSize;

        public string InspectionChecksSummary =>
            Presentation.InspectionChecksSummary;

        public IReadOnlyList<InspectionCheckPresentation> InspectionChecks =>
            _inspectionChecks;

        public Visibility InspectionDetailsVisibility =>
            Presentation.InspectionDetailsVisibility;

        /// <summary>
        /// Exposes the retained disclosure to the later page-owned motion bridge.
        /// </summary>
        internal InspectionDisclosure? ActiveDisclosure =>
            Presentation.DisplayMode == InspectionModelCardMode.Detailed &&
            Presentation.InspectionDetailsVisibility == Visibility.Visible
                ? InspectionDetailsDisclosure
                : null;

        /// <summary>
        /// Selects the production page-owned two-phase disclosure path. Direct
        /// standalone controls retain their synchronous compatibility path.
        /// </summary>
        internal bool IsDisclosureStateExternallyOwned { get; set; }

        internal void PrepareDisclosureTarget(bool isExpanded) =>
            InspectionDetailsDisclosure.PrepareTargetState(isExpanded);

        internal void CompleteDisclosureTarget(bool isExpanded) =>
            InspectionDetailsDisclosure.CompleteTargetState(isExpanded);

        internal void ClaimDisclosureTarget(bool isExpanded) =>
            InspectionDetailsDisclosure.ClaimTargetState(isExpanded);

        internal void RollbackDisclosureTargetClaim(bool isExpanded) =>
            InspectionDetailsDisclosure.RollbackTargetStateClaim(isExpanded);

        public static InspectionStatusGlyphKind GetStatusGlyphKind(
            InspectionCheckStatus status) =>
            status switch
            {
                InspectionCheckStatus.Passed =>
                    InspectionStatusGlyphKind.Success,
                InspectionCheckStatus.Warning =>
                    InspectionStatusGlyphKind.Warning,
                InspectionCheckStatus.Error => InspectionStatusGlyphKind.Error,
                InspectionCheckStatus.Information =>
                    InspectionStatusGlyphKind.Information,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(status),
                    status,
                    "The inspection-check status is not recognised.")
            };

        public static Visibility GetPassedVisibility(
            InspectionCheckStatus status) =>
            status == InspectionCheckStatus.Passed
                ? Visibility.Visible
                : Visibility.Collapsed;

        public static Visibility GetWarningVisibility(
            InspectionCheckStatus status) =>
            status == InspectionCheckStatus.Warning
                ? Visibility.Visible
                : Visibility.Collapsed;

        public static Visibility GetErrorVisibility(
            InspectionCheckStatus status) =>
            status == InspectionCheckStatus.Error
                ? Visibility.Visible
                : Visibility.Collapsed;

        public static Visibility GetInformationVisibility(
            InspectionCheckStatus status) =>
            status is InspectionCheckStatus.Passed or
                InspectionCheckStatus.Warning or
                InspectionCheckStatus.Error
                    ? Visibility.Collapsed
                    : Visibility.Visible;

        /// <summary>
        /// Returns the accessible description of the compact badge.
        /// </summary>
        public string GetBadgeAutomationName(InspectionModelBadgeState state)
        {
            return $"Model status: {GetBadgeText(state)}";
        }

        /// <summary>
        /// Returns the explicit user-facing compact badge text.
        /// </summary>
        public string GetBadgeText(InspectionModelBadgeState state)
        {
            return state switch
            {
                InspectionModelBadgeState.ModelSelected => "MODEL SELECTED",
                InspectionModelBadgeState.Inspected => "INSPECTED",
                InspectionModelBadgeState.SourceModel => "SOURCE MODEL",
                InspectionModelBadgeState.Incomplete => "INCOMPLETE",
                InspectionModelBadgeState.Unsupported => "UNSUPPORTED",
                InspectionModelBadgeState.Invalid => "INVALID",
                InspectionModelBadgeState.NotInspected => "NOT INSPECTED",
                InspectionModelBadgeState.ResultUnknown => "RESULT UNKNOWN",
                _ => "RESULT UNKNOWN"
            };
        }

        /// <summary>
        /// Returns the accessible expander name for its current state.
        /// </summary>
        public string GetInspectionDetailsAutomationName(bool isExpanded)
        {
            return isExpanded
                ? "Hide model inspection details"
                : "View model inspection details";
        }

        /// <summary>
        /// Returns the visible expander action text for its current state.
        /// </summary>
        public string GetInspectionDetailsActionText(bool isExpanded)
        {
            return isExpanded
                ? "Hide inspection details"
                : "View inspection details";
        }

        /// <summary>
        /// Responds when the parent page replaces the model presentation.
        /// </summary>
        private static void OnPresentationChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs eventArguments)
        {
            InspectionModelCard control =
                (InspectionModelCard)dependencyObject;
            InspectionModelCardPresentation presentation =
                eventArguments.NewValue as InspectionModelCardPresentation
                ?? InspectionModelCardPresentation.Empty;
            InspectionModelCardPresentation previous =
                eventArguments.OldValue as InspectionModelCardPresentation
                ?? InspectionModelCardPresentation.Empty;

            control.UpdateInspectionChecks(previous, presentation);
            control.ApplyPresentation(presentation);
        }

        private void UpdateInspectionChecks(
            InspectionModelCardPresentation previous,
            InspectionModelCardPresentation current)
        {
            if (CanRetainInspectionChecks(previous, current))
            {
                return;
            }

            _inspectionChecks = current.InspectionChecks;
        }

        private static bool CanRetainInspectionChecks(
            InspectionModelCardPresentation previous,
            InspectionModelCardPresentation current)
        {
            if (previous.DisplayMode != InspectionModelCardMode.Detailed ||
                current.DisplayMode != InspectionModelCardMode.Detailed ||
                previous.InspectionDetailsVisibility != Visibility.Visible ||
                current.InspectionDetailsVisibility != Visibility.Visible ||
                previous.BadgeState != current.BadgeState ||
                !string.Equals(
                    previous.ModelName,
                    current.ModelName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    previous.CompactSummary,
                    current.CompactSummary,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    previous.FormatShortName,
                    current.FormatShortName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    previous.OverviewFormatBadgeText,
                    current.OverviewFormatBadgeText,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    previous.Publisher,
                    current.Publisher,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    previous.FormatName,
                    current.FormatName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    previous.Quantisation,
                    current.Quantisation,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    previous.ParameterCount,
                    current.ParameterCount,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    previous.ModelType,
                    current.ModelType,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    previous.DeclaredContext,
                    current.DeclaredContext,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    previous.FileSize,
                    current.FileSize,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    previous.InspectionChecksSummary,
                    current.InspectionChecksSummary,
                    StringComparison.Ordinal) ||
                previous.InspectionChecks.Count !=
                    current.InspectionChecks.Count)
            {
                return false;
            }

            for (int index = 0;
                index < previous.InspectionChecks.Count;
                index++)
            {
                InspectionCheckPresentation oldCheck =
                    previous.InspectionChecks[index];
                InspectionCheckPresentation newCheck =
                    current.InspectionChecks[index];
                if (oldCheck.Status != newCheck.Status ||
                    !string.Equals(
                        oldCheck.Title,
                        newCheck.Title,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        oldCheck.Detail,
                        newCheck.Detail,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        oldCheck.StatusText,
                        newCheck.StatusText,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        oldCheck.AutomationName,
                        newCheck.AutomationName,
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Refreshes bindings and applies the compact or detailed layout.
        /// </summary>
        private void ApplyPresentation(
            InspectionModelCardPresentation presentation)
        {
            if (!_isInitialized)
            {
                return;
            }

            Bindings.Update();
            AutomationProperties.SetName(this, presentation.ModelName);
            if (!IsDisclosureStateExternallyOwned)
            {
                InspectionDetailsDisclosure.PrepareTargetState(
                    presentation.IsInspectionDetailsExpanded);
                InspectionDetailsDisclosure.CompleteTargetState(
                    presentation.IsInspectionDetailsExpanded);
            }

            string displayStateName = presentation.DisplayMode switch
            {
                InspectionModelCardMode.Compact => "CompactState",
                InspectionModelCardMode.Detailed => "DetailedState",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(presentation),
                    presentation.DisplayMode,
                    "Unknown inspection model-card mode.")
            };
            ApplyVisualState(displayStateName);
            ApplyVisualState(presentation.BadgeState switch
            {
                InspectionModelBadgeState.Inspected =>
                    "InspectedBadgeState",
                InspectionModelBadgeState.Incomplete =>
                    "WarningBadgeState",
                InspectionModelBadgeState.Unsupported or
                    InspectionModelBadgeState.Invalid =>
                        "ErrorBadgeState",
                InspectionModelBadgeState.NotInspected or
                    InspectionModelBadgeState.ResultUnknown =>
                        "NeutralBadgeState",
                _ => "InformationBadgeState"
            });
            ApplyVisualState(
                presentation.IsInspectionDetailsExpanded
                    ? "ExpandedDetailsState"
                    : "CollapsedDetailsState");
            BalanceDetailedHeader();
        }

        private void InspectionDetailsDisclosure_ToggleRequested(
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

            InspectionDetailsDisclosure.ToggleRequested +=
                InspectionDetailsDisclosure_ToggleRequested;
            _isDisclosureAttached = true;
        }

        private void DetachDisclosure()
        {
            if (!_isDisclosureAttached)
            {
                return;
            }

            InspectionDetailsDisclosure.ToggleRequested -=
                InspectionDetailsDisclosure_ToggleRequested;
            _isDisclosureAttached = false;
        }

        /// <summary>
        /// Applies the model layout endpoint selected from the current client
        /// width. XamlRoot supplies production window breakpoints; an isolated
        /// control uses its measured width so geometry tests remain deterministic.
        /// </summary>
        private void LayoutRoot_SizeChanged(
            object sender,
            SizeChangedEventArgs eventArguments)
        {
            double width = XamlRoot?.Size.Width ?? eventArguments.NewSize.Width;
            ApplyResponsiveLayout(width);
            BalanceDetailedHeader();
        }

        private void OverviewFormatChip_SizeChanged(
            object sender,
            SizeChangedEventArgs eventArguments) => BalanceDetailedHeader();

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

            if (_observedXamlRoot is not null)
            {
                ApplyResponsiveLayout(_observedXamlRoot.Size.Width);
            }
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
            if (_observedXamlRoot is not null)
            {
                _observedXamlRoot.Changed -= XamlRoot_Changed;
                _observedXamlRoot = null;
            }
        }

        private void ApplyResponsiveLayout(double width)
        {
            OverrideResponsiveWidthForFixture(ref width);
            string stateName = width >= 888
                ? "WideModelState"
                : width >= 600
                    ? "MediumModelState"
                    : "NarrowModelState";
            ApplyResponsiveState(stateName);
            BalanceDetailedHeader();
        }

        private void BalanceDetailedHeader()
        {
            if (!_isInitialized || OverviewFormatChip.Visibility != Visibility.Visible)
            {
                return;
            }

            OverviewFormatChip.Measure(new Windows.Foundation.Size(
                double.PositiveInfinity,
                double.PositiveInfinity));
            double balanceWidth = Math.Clamp(
                OverviewFormatChip.DesiredSize.Width,
                OverviewFormatChip.MinWidth,
                OverviewFormatChip.MaxWidth);
            GridLength balanced = new(balanceWidth, GridUnitType.Pixel);
            if (!DetailedHeaderLeadingColumn.Width.Equals(balanced))
            {
                DetailedHeaderLeadingColumn.Width = balanced;
                DetailedHeaderTrailingColumn.Width = balanced;
            }
        }

        private void ApplyResponsiveState(string stateName)
        {
            if (!_isInitialized ||
                string.Equals(
                    _responsiveStateName,
                    stateName,
                    StringComparison.Ordinal))
            {
                return;
            }

            ApplyVisualState(stateName);
            _responsiveStateName = stateName;
        }

        partial void OverrideResponsiveWidthForFixture(ref double width);

        private void ApplyVisualState(string stateName)
        {
            if (!VisualStateManager.GoToState(this, stateName, false))
            {
                throw new InvalidOperationException(
                    $"The model-card visual state '{stateName}' was not found.");
            }
        }

    }
}
