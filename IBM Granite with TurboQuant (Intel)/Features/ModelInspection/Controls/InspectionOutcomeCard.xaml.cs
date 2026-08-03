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
        // Prevents visual-state changes before the XAML visual tree exists.
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
            // Builds the named elements declared in InspectionOutcomeCard.xaml.
            InitializeComponent();

            // Records that it is now safe to change XAML visual states.
            _isInitialized = true;

            // Applies the safe default or any value assigned during construction.
            ApplyPresentation(Presentation);
        }

        /// <summary>
        /// Gets or sets the outcome presentation displayed by this card.
        /// </summary>
        public InspectionOutcomePresentation Presentation
        {
            // Reads the value from WinUI's dependency-property store.
            get => (InspectionOutcomePresentation)GetValue(PresentationProperty);

            // Stores a safe non-null value in WinUI's dependency-property store.
            set => SetValue(
                PresentationProperty,
                value ?? InspectionOutcomePresentation.Hidden);
        }

        /// <summary>
        /// Gets whether the outcome banner participates in page layout.
        /// </summary>
        public Visibility CardVisibility
        {
            // Reads the current calculated visibility.
            get => (Visibility)GetValue(CardVisibilityProperty);

            // Only this control is allowed to calculate its own visibility.
            private set => SetValue(CardVisibilityProperty, value);
        }

        /// <summary>
        /// Responds whenever the page or ViewModel replaces Presentation.
        /// </summary>
        private static void OnPresentationChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs eventArguments)
        {
            // Recover the specific control instance whose property changed.
            var control = (InspectionOutcomeCard)dependencyObject;

            // Protect every x:Bind path from a null presentation.
            var presentation =
                eventArguments.NewValue as InspectionOutcomePresentation
                ?? InspectionOutcomePresentation.Hidden;

            // Apply visibility and the correct visual tone.
            control.ApplyPresentation(presentation);
        }

        /// <summary>
        /// Converts the semantic presentation into visibility and a tone state.
        /// </summary>
        private void ApplyPresentation(
            InspectionOutcomePresentation presentation)
        {
            // The hidden kind removes the complete banner from page layout.
            CardVisibility =
                presentation.Kind == InspectionOutcomePresentationKind.Hidden
                    ? Visibility.Collapsed
                    : Visibility.Visible;

            // Named XAML elements do not exist before InitializeComponent completes.
            if (!_isInitialized || CardVisibility == Visibility.Collapsed)
            {
                return;
            }

            // Convert the presentation tone into the matching XAML state name.
            var stateName = presentation.Tone switch
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

            // Apply the selected outcome colours without animation.
            VisualStateManager.GoToState(this, stateName, false);
        }
    }
}
