using GraniteEdgeAI.Features.ModelInspection.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace GraniteEdgeAI.Features.ModelInspection.Controls
{
    /// <summary>
    /// Displays the high-level result of model inspection.
    /// </summary>
    public sealed partial class InspectionOutcomeCard : UserControl
    {
        // visual states are unavailable until InitializeComponent builds the xaml tree
        private bool _isInitialized;

        /// <summary>
        /// Identifies the bindable Presentation dependency property.
        /// </summary>
        public static readonly DependencyProperty PresentationProperty =
            DependencyProperty.Register(
                nameof(Presentation),
                typeof(InspectionOutcomePresentation),
                typeof(InspectionOutcomeCard),
                new PropertyMetadata(
                    InspectionOutcomePresentation.Hidden,
                    OnPresentationChanged));

        /// <summary>
        /// Identifies the internally controlled CardVisibility dependency property.
        /// </summary>
        public static readonly DependencyProperty CardVisibilityProperty =
            DependencyProperty.Register(
                nameof(CardVisibility),
                typeof(Visibility),
                typeof(InspectionOutcomeCard),
                new PropertyMetadata(Visibility.Collapsed));

        /// <summary>
        /// Creates the control and loads its XAML visual tree.
        /// </summary>
        public InspectionOutcomeCard()
        {
            InitializeComponent();
            _isInitialized = true;
            ApplyPresentation(Presentation);
        }

        /// <summary>
        /// Gets or sets the outcome presentation displayed by this card.
        /// </summary>
        public InspectionOutcomePresentation Presentation
        {
            get => (InspectionOutcomePresentation)GetValue(PresentationProperty);
            set => SetValue(
                PresentationProperty,
                value ?? InspectionOutcomePresentation.Hidden);
        }

        /// <summary>
        /// Gets whether the outcome banner participates in page layout.
        /// </summary>
        public Visibility CardVisibility
        {
            get => (Visibility)GetValue(CardVisibilityProperty);
            private set => SetValue(CardVisibilityProperty, value);
        }

        /// <summary>
        /// Responds whenever the page or ViewModel replaces Presentation.
        /// </summary>
        private static void OnPresentationChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs eventArguments)
        {
            InspectionOutcomeCard control =
                (InspectionOutcomeCard)dependencyObject;
            InspectionOutcomePresentation presentation =
                eventArguments.NewValue as InspectionOutcomePresentation
                ?? InspectionOutcomePresentation.Hidden;

            control.ApplyPresentation(presentation);
        }

        /// <summary>
        /// Converts the semantic presentation into visibility and a tone state.
        /// </summary>
        private void ApplyPresentation(
            InspectionOutcomePresentation presentation)
        {
            CardVisibility =
                presentation.Kind == InspectionOutcomePresentationKind.Hidden
                    ? Visibility.Collapsed
                    : Visibility.Visible;

            // xaml visual states are unavailable during dependency-property initialization
            if (!_isInitialized || CardVisibility == Visibility.Collapsed)
            {
                return;
            }

            string stateName = presentation.Tone switch
            {
                InspectionOutcomeTone.Success => "SuccessTone",
                InspectionOutcomeTone.Warning => "WarningTone",
                InspectionOutcomeTone.Information => "InformationTone",
                InspectionOutcomeTone.Error => "ErrorTone",
                InspectionOutcomeTone.Neutral => "NeutralTone",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(presentation),
                    presentation.Tone,
                    "Unknown inspection outcome tone.")
            };

            bool stateApplied = VisualStateManager.GoToState(
                this,
                stateName,
                false);

            // fail fast if xaml and code-behind state contracts drift apart
            if (!stateApplied)
            {
                throw new InvalidOperationException(
                    $"The outcome-card visual state '{stateName}' was not found.");
            }
        }
    }
}
