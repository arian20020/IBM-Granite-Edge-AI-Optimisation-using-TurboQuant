using GraniteEdgeAI.Features.ModelInspection.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace GraniteEdgeAI.Features.ModelInspection.Controls
{
    /// <summary>
    /// Displays the actions available for the current model-inspection state.
    /// </summary>
    public sealed partial class InspectionActionCard : UserControl
    {
        // visual states are unavailable until InitializeComponent builds the xaml tree
        private bool _isInitialized;
        private XamlRoot? _observedXamlRoot;
        private string? _responsiveStateName;

        /// <summary>
        /// Identifies the bindable Presentation dependency property.
        /// </summary>
        public static readonly DependencyProperty PresentationProperty =
            DependencyProperty.Register(
                nameof(Presentation),
                typeof(InspectionActionCardPresentation),
                typeof(InspectionActionCard),
                new PropertyMetadata(
                    InspectionActionCardPresentation.Hidden,
                    OnPresentationChanged));

        /// <summary>
        /// Identifies the internally controlled CardVisibility dependency property.
        /// </summary>
        public static readonly DependencyProperty CardVisibilityProperty =
            DependencyProperty.Register(
                nameof(CardVisibility),
                typeof(Visibility),
                typeof(InspectionActionCard),
                new PropertyMetadata(Visibility.Collapsed));

        /// <summary>
        /// Creates the control and loads its XAML visual tree.
        /// </summary>
        public InspectionActionCard()
        {
            InitializeComponent();
            _isInitialized = true;
            Loaded += Root_Loaded;
            Unloaded += Root_Unloaded;
            ApplyResponsiveState("WideActionState");
            ApplyPresentation(Presentation);
        }

        /// <summary>
        /// Gets or sets the complete presentation rendered by this card.
        /// </summary>
        public InspectionActionCardPresentation Presentation
        {
            get => (InspectionActionCardPresentation)GetValue(PresentationProperty);
            set => SetValue(
                PresentationProperty,
                value ?? InspectionActionCardPresentation.Hidden);
        }

        /// <summary>
        /// Gets whether the complete action card participates in page layout.
        /// </summary>
        public Visibility CardVisibility
        {
            get => (Visibility)GetValue(CardVisibilityProperty);
            private set => SetValue(CardVisibilityProperty, value);
        }

        public static Visibility GetFutureHelpVisibility(
            InspectionActionPresentation action)
        {
            ArgumentNullException.ThrowIfNull(action);
            return action.Visibility == Visibility.Visible &&
                !action.IsEnabled &&
                !string.IsNullOrWhiteSpace(action.AutomationHelpText)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        public static string GetFutureHelpAutomationName(
            InspectionActionPresentation action)
        {
            ArgumentNullException.ThrowIfNull(action);
            return string.IsNullOrWhiteSpace(action.AutomationHelpText)
                ? string.Empty
                : $"{action.AutomationName}. {action.AutomationHelpText}";
        }

        /// <summary>
        /// Responds whenever the page or ViewModel replaces Presentation.
        /// </summary>
        private static void OnPresentationChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs eventArguments)
        {
            InspectionActionCard control =
                (InspectionActionCard)dependencyObject;
            InspectionActionCardPresentation presentation =
                eventArguments.NewValue as InspectionActionCardPresentation
                ?? InspectionActionCardPresentation.Hidden;

            control.ApplyPresentation(presentation);
        }

        /// <summary>
        /// Converts the supplied mode into card visibility and a XAML visual state.
        /// </summary>
        private void ApplyPresentation(
            InspectionActionCardPresentation presentation)
        {
            CardVisibility =
                presentation.Mode == InspectionActionCardMode.Hidden
                    ? Visibility.Collapsed
                    : Visibility.Visible;

            // xaml visual states are unavailable during dependency-property initialization
            if (!_isInitialized || CardVisibility == Visibility.Collapsed)
            {
                return;
            }

            string stateName = presentation.Mode switch
            {
                InspectionActionCardMode.Inspecting => "InspectingState",
                InspectionActionCardMode.Result => "ResultState",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(presentation),
                    presentation.Mode,
                    "Unknown inspection action-card mode.")
            };

            bool stateApplied = VisualStateManager.GoToState(
                this,
                stateName,
                false);

            // fail fast if xaml and code-behind state contracts drift apart
            if (!stateApplied)
            {
                throw new InvalidOperationException(
                    $"The action-card visual state '{stateName}' was not found.");
            }

            // refresh compiled bindings after the presentation instance changes
            Bindings.Update();
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
            OverrideResponsiveWidthForFixture(ref width);
            ApplyResponsiveState(width >= 888d
                ? "WideActionState"
                : width >= 600d
                    ? "MediumActionState"
                    : "NarrowActionState");
        }

        partial void OverrideResponsiveWidthForFixture(ref double width);

        private void ApplyResponsiveState(string stateName)
        {
            if (!_isInitialized ||
                string.Equals(_responsiveStateName, stateName, StringComparison.Ordinal))
            {
                return;
            }

            if (!VisualStateManager.GoToState(this, stateName, false))
            {
                throw new InvalidOperationException(
                    $"The action-card visual state '{stateName}' was not found.");
            }

            _responsiveStateName = stateName;
        }
    }
}
