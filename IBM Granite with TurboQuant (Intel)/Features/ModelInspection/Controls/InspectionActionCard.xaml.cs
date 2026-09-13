using GraniteEdgeAI.Features.ModelInspection.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;

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
        private ActionResponsiveBand _responsiveBand = ActionResponsiveBand.Wide;

        private enum ActionResponsiveBand
        {
            Wide,
            Medium,
            Narrow
        }

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
            ApplyActionLayout(presentation);
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
            _responsiveBand = width >= 888d
                ? ActionResponsiveBand.Wide
                : width >= 600d
                    ? ActionResponsiveBand.Medium
                    : ActionResponsiveBand.Narrow;
            ApplyResponsiveState(_responsiveBand switch
            {
                ActionResponsiveBand.Wide => "WideActionState",
                ActionResponsiveBand.Medium => "MediumActionState",
                _ => "NarrowActionState"
            });
            ApplyActionLayout(Presentation);
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

        private void ApplyActionLayout(
            InspectionActionCardPresentation presentation)
        {
            if (!_isInitialized ||
                presentation.Mode != InspectionActionCardMode.Result)
            {
                return;
            }

            var candidates = new[]
            {
                (presentation.SecondaryActionOne, (FrameworkElement)SecondaryActionOneHost),
                (presentation.SecondaryActionTwo, (FrameworkElement)SecondaryActionTwoHost),
                (presentation.PrimaryAction, (FrameworkElement)PrimaryActionHost)
            };
            var visible = new List<FrameworkElement>(capacity: 3);
            foreach (var candidate in candidates)
            {
                if (candidate.Item2.Visibility != candidate.Item1.Visibility)
                {
                    candidate.Item2.Visibility = candidate.Item1.Visibility;
                }
                SetMargin(candidate.Item2, new Thickness(0d));
                if (candidate.Item1.Visibility == Visibility.Visible)
                {
                    visible.Add(candidate.Item2);
                }
            }

            if (_responsiveBand == ActionResponsiveBand.Narrow)
            {
                ConfigureColumns(Star, Star, Star);
                for (int index = 0; index < visible.Count; index++)
                {
                    FrameworkElement host = visible[index];
                    Place(host, row: index, column: 0, columnSpan: 3);
                    SetMargin(host, new Thickness(
                        0d,
                        index == 0 ? 0d : 12d,
                        0d,
                        0d));
                    ConfigureHostWidth(
                        host,
                        HorizontalAlignment.Stretch,
                        double.PositiveInfinity);
                }

                return;
            }

            switch (visible.Count)
            {
                case 1:
                    ConfigureColumns(Star, GridLength.Auto, Star);
                    Place(visible[0], row: 0, column: 1, columnSpan: 1);
                    ConfigureHostWidth(
                        visible[0],
                        HorizontalAlignment.Center,
                        280d);
                    break;
                case 2:
                    ConfigureColumns(Star, Star, Zero);
                    Place(visible[0], row: 0, column: 0, columnSpan: 1);
                    Place(visible[1], row: 0, column: 1, columnSpan: 1);
                    ConfigureEqualHorizontalHosts(visible);
                    SetMargin(visible[0], new Thickness(0d, 0d, 6d, 0d));
                    SetMargin(visible[1], new Thickness(6d, 0d, 0d, 0d));
                    break;
                case 3:
                    ConfigureColumns(Star, Star, Star);
                    for (int index = 0; index < visible.Count; index++)
                    {
                        Place(visible[index], row: 0, column: index, columnSpan: 1);
                    }
                    ConfigureEqualHorizontalHosts(visible);
                    SetMargin(visible[0], new Thickness(0d, 0d, 8d, 0d));
                    SetMargin(visible[1], new Thickness(4d, 0d, 4d, 0d));
                    SetMargin(visible[2], new Thickness(8d, 0d, 0d, 0d));
                    break;
            }
        }

        private static GridLength Star =>
            new(1d, GridUnitType.Star);

        private static GridLength Zero =>
            new(0d, GridUnitType.Pixel);

        private void ConfigureColumns(
            GridLength first,
            GridLength second,
            GridLength third)
        {
            if (!ActionColumnOne.Width.Equals(first))
            {
                ActionColumnOne.Width = first;
            }
            if (!ActionColumnTwo.Width.Equals(second))
            {
                ActionColumnTwo.Width = second;
            }
            if (!ActionColumnThree.Width.Equals(third))
            {
                ActionColumnThree.Width = third;
            }
        }

        private static void ConfigureEqualHorizontalHosts(
            IEnumerable<FrameworkElement> hosts)
        {
            foreach (FrameworkElement host in hosts)
            {
                ConfigureHostWidth(
                    host,
                    HorizontalAlignment.Stretch,
                    double.PositiveInfinity);
            }
        }

        private static void ConfigureHostWidth(
            FrameworkElement host,
            HorizontalAlignment alignment,
            double maxWidth)
        {
            if (host.HorizontalAlignment != alignment)
            {
                host.HorizontalAlignment = alignment;
            }
            if (!host.MaxWidth.Equals(maxWidth))
            {
                host.MaxWidth = maxWidth;
            }
        }

        private static void SetMargin(
            FrameworkElement host,
            Thickness margin)
        {
            if (!host.Margin.Equals(margin))
            {
                host.Margin = margin;
            }
        }

        private static void Place(
            FrameworkElement host,
            int row,
            int column,
            int columnSpan)
        {
            if (Grid.GetRow(host) != row)
            {
                Grid.SetRow(host, row);
            }
            if (Grid.GetColumn(host) != column)
            {
                Grid.SetColumn(host, column);
            }
            if (Grid.GetColumnSpan(host) != columnSpan)
            {
                Grid.SetColumnSpan(host, columnSpan);
            }
        }
    }
}
