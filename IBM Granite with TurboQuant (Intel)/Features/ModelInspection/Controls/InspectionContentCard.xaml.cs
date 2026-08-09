using GraniteEdgeAI.Features.ModelInspection.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Linq;
using Windows.UI;

namespace GraniteEdgeAI.Features.ModelInspection.Controls
{
    /// <summary>
    /// Displays inspection progress, warnings and diagnostic content.
    /// </summary>
    public sealed partial class InspectionContentCard : UserControl
    {
        // Cached brushes avoid allocating a new brush for every repeated row.
        private static readonly Brush NeutralBackground =
            CreateBrush(0xF2, 0xF4, 0xF7);
        private static readonly Brush NeutralForeground =
            CreateBrush(0x66, 0x70, 0x85);
        private static readonly Brush InformationBackground =
            CreateBrush(0xEE, 0xF5, 0xFF);
        private static readonly Brush InformationForeground =
            CreateBrush(0x0F, 0x62, 0xFE);
        private static readonly Brush SuccessBackground =
            CreateBrush(0xE9, 0xF7, 0xF1);
        private static readonly Brush SuccessForeground =
            CreateBrush(0x06, 0x7A, 0x57);
        private static readonly Brush WarningBackground =
            CreateBrush(0xFF, 0xF6, 0xE0);
        private static readonly Brush WarningForeground =
            CreateBrush(0x9A, 0x67, 0x00);
        private static readonly Brush ErrorBackground =
            CreateBrush(0xFF, 0xF0, 0xEF);
        private static readonly Brush ErrorForeground =
            CreateBrush(0xB4, 0x23, 0x18);

        // Prevents generated binding updates before InitializeComponent completes.
        private bool _isInitialized;
        private string? _lastAnnouncedAutomationName;

        /// <summary>
        /// Identifies the bindable presentation dependency property.
        /// </summary>
        public static readonly DependencyProperty PresentationProperty =
            DependencyProperty.Register(
                nameof(Presentation),
                typeof(InspectionContentCardPresentation),
                typeof(InspectionContentCard),
                new PropertyMetadata(
                    InspectionContentCardPresentation.Hidden,
                    OnPresentationChanged));

        /// <summary>
        /// Identifies the internally calculated card-visibility property.
        /// </summary>
        public static readonly DependencyProperty CardVisibilityProperty =
            DependencyProperty.Register(
                nameof(CardVisibility),
                typeof(Visibility),
                typeof(InspectionContentCard),
                new PropertyMetadata(Visibility.Collapsed));

        /// <summary>
        /// Creates the content card and loads its XAML layout.
        /// </summary>
        public InspectionContentCard()
        {
            InitializeComponent();
            _isInitialized = true;
            Presentation = InspectionContentCardPresentation.Hidden;
        }

        /// <summary>
        /// Gets or sets the complete presentation rendered by the card.
        /// </summary>
        public InspectionContentCardPresentation Presentation
        {
            get => (InspectionContentCardPresentation)GetValue(PresentationProperty);
            set => SetValue(
                PresentationProperty,
                value ?? InspectionContentCardPresentation.Hidden);
        }

        /// <summary>
        /// Gets whether this card participates in the parent page layout.
        /// </summary>
        public Visibility CardVisibility
        {
            get => (Visibility)GetValue(CardVisibilityProperty);
            private set => SetValue(CardVisibilityProperty, value);
        }

        internal int LiveRegionChangeNotificationCount { get; private set; }

        /// <summary>
        /// Returns the background used by a status marker or badge.
        /// </summary>
        public static Brush GetStatusBackground(
            InspectionContentStatus status)
        {
            return status switch
            {
                InspectionContentStatus.Passed => SuccessBackground,
                InspectionContentStatus.Warning => WarningBackground,
                InspectionContentStatus.Error => ErrorBackground,
                InspectionContentStatus.Active => InformationBackground,
                InspectionContentStatus.Information => InformationBackground,
                InspectionContentStatus.Waiting => NeutralBackground,
                InspectionContentStatus.Neutral => NeutralBackground,
                _ => NeutralBackground
            };
        }

        /// <summary>
        /// Returns the foreground used by a status marker, label or badge.
        /// </summary>
        public static Brush GetStatusForeground(
            InspectionContentStatus status)
        {
            return status switch
            {
                InspectionContentStatus.Passed => SuccessForeground,
                InspectionContentStatus.Warning => WarningForeground,
                InspectionContentStatus.Error => ErrorForeground,
                InspectionContentStatus.Active => InformationForeground,
                InspectionContentStatus.Information => InformationForeground,
                InspectionContentStatus.Waiting => NeutralForeground,
                InspectionContentStatus.Neutral => NeutralForeground,
                _ => NeutralForeground
            };
        }

        /// <summary>
        /// Returns a WinUI symbol that matches the supplied semantic status.
        /// </summary>
        public static Symbol GetStatusSymbol(
            InspectionContentStatus status)
        {
            return status switch
            {
                InspectionContentStatus.Passed => Symbol.Accept,
                InspectionContentStatus.Warning => Symbol.Important,
                InspectionContentStatus.Error => Symbol.Cancel,
                InspectionContentStatus.Active => Symbol.Clock,
                InspectionContentStatus.Information => Symbol.Help,
                InspectionContentStatus.Waiting => Symbol.Clock,
                InspectionContentStatus.Neutral => Symbol.Help,
                _ => Symbol.Help
            };
        }

        /// <summary>
        /// Converts a connector flag into XAML visibility.
        /// </summary>
        public static Visibility GetConnectorVisibility(bool showConnector)
        {
            return showConnector
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        /// <summary>
        /// Displays a terminal glyph only for explicit completed, warning,
        /// error or informational stage states.
        /// </summary>
        public static Visibility GetTerminalMarkerVisibility(
            InspectionContentStatus status)
        {
            return status is InspectionContentStatus.Passed or
                InspectionContentStatus.Warning or
                InspectionContentStatus.Error or
                InspectionContentStatus.Information
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        /// <summary>
        /// Displays the progress ring only for the active stage.
        /// </summary>
        public static Visibility GetActiveVisibility(
            InspectionContentStatus status)
        {
            return status == InspectionContentStatus.Active
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        /// <summary>
        /// Keeps an active ring indeterminate unless the runtime supplied a
        /// genuine measurable fraction.
        /// </summary>
        public static bool IsProgressIndeterminate(double? stageFraction)
        {
            return !stageFraction.HasValue;
        }

        /// <summary>
        /// Converts the validated zero-to-one domain fraction into the
        /// percentage scale used by WinUI ProgressRing.
        /// </summary>
        public static double GetProgressPercent(double? stageFraction)
        {
            return stageFraction.GetValueOrDefault() * 100d;
        }

        /// <summary>
        /// Displays the stage number only before the stage starts.
        /// </summary>
        public static Visibility GetWaitingVisibility(
            InspectionContentStatus status)
        {
            return status == InspectionContentStatus.Waiting
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        /// <summary>
        /// Selects the correct disclosure label for the current expanded state.
        /// </summary>
        public static string GetDisclosureText(
            string collapsedText,
            string expandedText,
            bool isExpanded)
        {
            return isExpanded
                ? expandedText
                : collapsedText;
        }

        /// <summary>
        /// Responds when the parent page replaces the presentation.
        /// </summary>
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

        /// <summary>
        /// Applies visibility and refreshes every compiled binding.
        /// </summary>
        private void ApplyPresentation(
            InspectionContentCardPresentation presentation)
        {
            CardVisibility =
                presentation.Mode == InspectionContentCardMode.Hidden
                    ? Visibility.Collapsed
                    : Visibility.Visible;

            if (!_isInitialized)
            {
                return;
            }

            // Refreshes nested x:Bind paths after the presentation object changes.
            Bindings.Update();

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

        private void RaiseLiveRegionChanged()
        {
            AutomationPeer peer =
                FrameworkElementAutomationPeer.FromElement(this) ??
                FrameworkElementAutomationPeer.CreatePeerForElement(this);
            peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
            LiveRegionChangeNotificationCount++;
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
