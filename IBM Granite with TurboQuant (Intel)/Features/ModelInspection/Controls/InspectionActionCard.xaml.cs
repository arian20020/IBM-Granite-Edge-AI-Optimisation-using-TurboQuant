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
        // Prevents visual-state changes before InitializeComponent has
        // created the named XAML elements.
        private bool _isInitialized;

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
            // Builds the named elements declared in InspectionActionCard.xaml.
            InitializeComponent();

            // Records that it is now safe to change XAML visual states.
            _isInitialized = true;

            // Applies the safe default or any value assigned during construction.
            ApplyPresentation(Presentation);
        }

        /// <summary>
        /// Gets or sets the complete presentation rendered by this card.
        /// </summary>
        public InspectionActionCardPresentation Presentation
        {
            // Reads the value from WinUI's dependency-property store.
            get => (InspectionActionCardPresentation)GetValue(PresentationProperty);

            // Stores a safe non-null value in WinUI's dependency-property store.
            set => SetValue(
                PresentationProperty,
                value ?? InspectionActionCardPresentation.Hidden);
        }

        /// <summary>
        /// Gets whether the complete action card participates in page layout.
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
            var control = (InspectionActionCard)dependencyObject;

            // Protect every nested x:Bind path from a null presentation.
            var presentation =
                eventArguments.NewValue as InspectionActionCardPresentation
                ?? InspectionActionCardPresentation.Hidden;

            // Apply visibility and the correct structural layout.
            control.ApplyPresentation(presentation);
        }

        /// <summary>
        /// Converts the supplied mode into card visibility and a XAML visual state.
        /// </summary>
        private void ApplyPresentation(
            InspectionActionCardPresentation presentation)
        {
            // Hidden removes the complete control from the parent page layout.
            CardVisibility =
                presentation.Mode == InspectionActionCardMode.Hidden
                    ? Visibility.Collapsed
                    : Visibility.Visible;

            // Named XAML elements do not exist before InitializeComponent completes.
            if (!_isInitialized || CardVisibility == Visibility.Collapsed)
            {
                return;
            }

            // Select only between the two genuinely different card structures.
            var stateName = presentation.Mode switch
            {
                InspectionActionCardMode.Inspecting => "InspectingState",
                InspectionActionCardMode.Result => "ResultState",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(presentation),
                    presentation.Mode,
                    "Unknown inspection action-card mode.")
            };

            // Apply the structural state after the UserControl is fully loaded.
            bool stateApplied = VisualStateManager.GoToState(
                this,
                stateName,
                false);

            // A missing visual state indicates that the XAML and code-behind
            // contracts have drifted apart.
            if (!stateApplied)
            {
                throw new InvalidOperationException(
                    $"The action-card visual state '{stateName}' was not found.");
            }

            // Re-evaluate the compiled bindings so button text, visibility,
            // accessibility labels, and enabled states use the new presentation.
            Bindings.Update();
        }
    }
}
