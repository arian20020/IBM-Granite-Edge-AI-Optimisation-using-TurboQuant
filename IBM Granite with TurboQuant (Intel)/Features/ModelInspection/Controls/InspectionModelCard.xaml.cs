using GraniteEdgeAI.Features.ModelInspection.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using Windows.UI;

namespace GraniteEdgeAI.Features.ModelInspection.Controls
{
    /// <summary>
    /// Displays the selected model and its inspected metadata.
    /// </summary>
    public sealed partial class InspectionModelCard : UserControl
    {
        // Cached status brushes are shared by repeated inspection-check rows.
        private static readonly Brush CheckInformationBackground =
            CreateBrush(0xEE, 0xF5, 0xFF);
        private static readonly Brush CheckInformationForeground =
            CreateBrush(0x0F, 0x62, 0xFE);
        private static readonly Brush CheckSuccessBackground =
            CreateBrush(0xE9, 0xF7, 0xF1);
        private static readonly Brush CheckSuccessForeground =
            CreateBrush(0x06, 0x7A, 0x57);
        private static readonly Brush CheckWarningBackground =
            CreateBrush(0xFF, 0xF6, 0xE0);
        private static readonly Brush CheckWarningForeground =
            CreateBrush(0x9A, 0x67, 0x00);
        private static readonly Brush CheckErrorBackground =
            CreateBrush(0xFF, 0xF0, 0xEF);
        private static readonly Brush CheckErrorForeground =
            CreateBrush(0xB4, 0x23, 0x18);

        // Stores the expander state because the XAML uses a two-way binding.
        private bool _isInspectionDetailsExpanded;

        // Prevents state changes before the named XAML elements exist.
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
            ApplyPresentation(Presentation);
        }

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

        // The following read-only forwarding properties preserve the concise
        // x:Bind expressions already present in InspectionModelCard.xaml.

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
            Presentation.InspectionChecks;

        /// <summary>
        /// Gets or sets whether the inspection-check report is expanded.
        /// </summary>
        public bool IsInspectionDetailsExpanded
        {
            get => _isInspectionDetailsExpanded;
            set
            {
                if (_isInspectionDetailsExpanded == value)
                {
                    return;
                }

                _isInspectionDetailsExpanded = value;

                if (_isInitialized)
                {
                    // Refreshes the action text and accessible name functions.
                    Bindings.Update();
                }
            }
        }

        /// <summary>
        /// Returns the check-row background for the supplied status.
        /// </summary>
        public static Brush GetCheckBackground(InspectionCheckStatus status)
        {
            return status switch
            {
                InspectionCheckStatus.Passed => CheckSuccessBackground,
                InspectionCheckStatus.Warning => CheckWarningBackground,
                InspectionCheckStatus.Error => CheckErrorBackground,
                InspectionCheckStatus.Information => CheckInformationBackground,
                _ => CheckInformationBackground
            };
        }

        /// <summary>
        /// Returns the check-row foreground for the supplied status.
        /// </summary>
        public static Brush GetCheckForeground(InspectionCheckStatus status)
        {
            return status switch
            {
                InspectionCheckStatus.Passed => CheckSuccessForeground,
                InspectionCheckStatus.Warning => CheckWarningForeground,
                InspectionCheckStatus.Error => CheckErrorForeground,
                InspectionCheckStatus.Information => CheckInformationForeground,
                _ => CheckInformationForeground
            };
        }

        /// <summary>
        /// Returns the icon used by one inspection-check row.
        /// </summary>
        public static Symbol GetCheckSymbol(InspectionCheckStatus status)
        {
            return status switch
            {
                InspectionCheckStatus.Passed => Symbol.Accept,
                InspectionCheckStatus.Warning => Symbol.Important,
                InspectionCheckStatus.Error => Symbol.Cancel,
                InspectionCheckStatus.Information => Symbol.Help,
                _ => Symbol.Help
            };
        }

        /// <summary>
        /// Returns the accessible description of the compact badge.
        /// </summary>
        public string GetBadgeAutomationName(InspectionModelBadgeState state)
        {
            return $"Model status: {GetBadgeText(state)}";
        }

        /// <summary>
        /// Returns the compact badge background from the active theme dictionary.
        /// </summary>
        public Brush GetBadgeBackground(InspectionModelBadgeState state)
        {
            return state switch
            {
                InspectionModelBadgeState.Inspected =>
                    GetThemeBrush("InspectionSuccessBackgroundBrush"),
                InspectionModelBadgeState.Incomplete =>
                    GetThemeBrush("InspectionWarningBackgroundBrush"),
                InspectionModelBadgeState.Unsupported =>
                    GetThemeBrush("InspectionErrorBackgroundBrush"),
                InspectionModelBadgeState.Invalid =>
                    GetThemeBrush("InspectionErrorBackgroundBrush"),
                InspectionModelBadgeState.NotInspected =>
                    GetThemeBrush("InspectionNeutralBackgroundBrush"),
                InspectionModelBadgeState.ResultUnknown =>
                    GetThemeBrush("InspectionNeutralBackgroundBrush"),
                _ => GetThemeBrush("InspectionBlueBackgroundBrush")
            };
        }

        /// <summary>
        /// Returns the compact badge border brush.
        /// </summary>
        public Brush GetBadgeBorderBrush(InspectionModelBadgeState state)
        {
            return GetBadgeForeground(state);
        }

        /// <summary>
        /// Returns the compact badge foreground from the active theme dictionary.
        /// </summary>
        public Brush GetBadgeForeground(InspectionModelBadgeState state)
        {
            return state switch
            {
                InspectionModelBadgeState.Inspected =>
                    GetThemeBrush("InspectionSuccessBrush"),
                InspectionModelBadgeState.Incomplete =>
                    GetThemeBrush("InspectionWarningBrush"),
                InspectionModelBadgeState.Unsupported =>
                    GetThemeBrush("InspectionErrorBrush"),
                InspectionModelBadgeState.Invalid =>
                    GetThemeBrush("InspectionErrorBrush"),
                InspectionModelBadgeState.NotInspected =>
                    GetThemeBrush("InspectionNeutralBrush"),
                InspectionModelBadgeState.ResultUnknown =>
                    GetThemeBrush("InspectionNeutralBrush"),
                _ => GetThemeBrush("InspectionBlueBrush")
            };
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
            var control = (InspectionModelCard)dependencyObject;
            var presentation =
                eventArguments.NewValue as InspectionModelCardPresentation
                ?? InspectionModelCardPresentation.Empty;

            control.ApplyPresentation(presentation);
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

            // A new result starts with its details collapsed.
            _isInspectionDetailsExpanded = false;

            // Refreshes every forwarding property used by x:Bind.
            Bindings.Update();

            var stateName = presentation.DisplayMode switch
            {
                InspectionModelCardMode.Compact => "CompactState",
                InspectionModelCardMode.Detailed => "DetailedState",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(presentation),
                    presentation.DisplayMode,
                    "Unknown inspection model-card mode.")
            };

            VisualStateManager.GoToState(this, stateName, false);
        }

        /// <summary>
        /// Resolves one brush from this control's active theme resources.
        /// </summary>
        private Brush GetThemeBrush(string resourceKey)
        {
            return Resources[resourceKey] as Brush
                ?? throw new InvalidOperationException(
                    $"The brush resource '{resourceKey}' was not found.");
        }

        /// <summary>
        /// Creates one opaque solid-colour brush.
        /// </summary>
        private static Brush CreateBrush(byte red, byte green, byte blue)
        {
            return new SolidColorBrush(
                Color.FromArgb(0xFF, red, green, blue));
        }
    }
}