using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using GraniteEdgeAI.Features.Onboarding;
using GraniteEdgeAI.Features.ModelInspection.Models;


namespace GraniteEdgeAI.Features.Onboarding.Controls
{
    /// <summary>
    /// Displays progress through the five-stage model onboarding process.
    /// </summary>
    public sealed partial class OnboardingStageIndicator : UserControl
    {
        private bool _isRestoringCurrentStage;
        private bool _isRestoringInspectionStatus;

        // Resource keys used when applying the visual state of each step.
        private const string ActiveBrushKey =
            "OnboardingIndicatorActiveBrush";

        private const string SurfaceBrushKey =
            "OnboardingIndicatorSurfaceBrush";

        private const string InactiveSurfaceBrushKey =
            "OnboardingIndicatorInactiveSurfaceBrush";

        private const string InactiveBorderBrushKey =
            "OnboardingIndicatorInactiveBorderBrush";

        private const string PrimaryTextBrushKey =
            "OnboardingIndicatorPrimaryTextBrush";

        private const string SecondaryTextBrushKey =
            "OnboardingIndicatorSecondaryTextBrush";

        private const string MutedTextBrushKey =
            "OnboardingIndicatorMutedTextBrush";

        private const string ErrorBrushKey =
            "OnboardingIndicatorErrorBrush";

        /// <summary>
        /// Identifies the dependency property used by the CurrentStage property.
        /// </summary>
        public static readonly DependencyProperty CurrentStageProperty =
            DependencyProperty.Register(
                nameof(CurrentStage),
                typeof(OnboardingStage),
                typeof(OnboardingStageIndicator),
                new PropertyMetadata(
                    OnboardingStage.ImportModel,
                    OnCurrentStageChanged));

        public static readonly DependencyProperty InspectionStatusProperty =
            DependencyProperty.Register(
                nameof(InspectionStatus),
                typeof(InspectionFooterStatus),
                typeof(OnboardingStageIndicator),
                new PropertyMetadata(
                    InspectionFooterStatus.InProgress,
                    OnInspectionStatusChanged));

        /// <summary>
        /// Creates the stage indicator in its initial Import Model state.
        /// </summary>
        public OnboardingStageIndicator()
        {
            InitializeComponent();
            ApplyStage(CurrentStage);
        }

        /// <summary>
        /// Gets or sets the onboarding stage displayed by the indicator.
        /// </summary>
        public OnboardingStage CurrentStage
        {
            get => (OnboardingStage)GetValue(CurrentStageProperty);
            set => SetValue(CurrentStageProperty, value);
        }

        public InspectionFooterStatus InspectionStatus
        {
            get => (InspectionFooterStatus)GetValue(InspectionStatusProperty);
            set => SetValue(InspectionStatusProperty, value);
        }

        internal int LiveRegionChangeNotificationCount { get; private set; }

        /// <summary>
        /// Responds whenever CurrentStage receives a different value.
        /// </summary>
        private static void OnCurrentStageChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs eventArguments)
        {
            if (dependencyObject is not OnboardingStageIndicator indicator)
            {
                return;
            }

            if (indicator._isRestoringCurrentStage)
            {
                return;
            }

            // Ensure the new value is a valid onboarding-stage value.
            if (eventArguments.NewValue is not OnboardingStage newStage)
            {
                throw new InvalidOperationException(
                    "The onboarding stage indicator received an invalid stage.");
            }

            if (!IsValidStage(newStage))
            {
                if (eventArguments.OldValue is OnboardingStage oldStage &&
                    IsValidStage(oldStage))
                {
                    indicator.RestoreCurrentStage(oldStage);
                }

                throw new ArgumentOutOfRangeException(
                    nameof(newStage),
                    newStage,
                    "The onboarding stage must be between one and five.");
            }

            if (eventArguments.OldValue is OnboardingStage previousStage &&
                IsValidStage(previousStage) &&
                previousStage == newStage)
            {
                return;
            }

            indicator.ApplyStage(newStage);

            if (eventArguments.OldValue is OnboardingStage validPreviousStage &&
                IsValidStage(validPreviousStage))
            {
                indicator.RaiseLiveRegionChanged();
            }
        }

        /// <summary>
        /// Restores a rejected CurrentStage value without reapplying control state.
        /// </summary>
        private void RestoreCurrentStage(OnboardingStage previousStage)
        {
            _isRestoringCurrentStage = true;

            try
            {
                SetValue(CurrentStageProperty, previousStage);
            }
            finally
            {
                _isRestoringCurrentStage = false;
            }
        }

        /// <summary>
        /// Notifies accessibility clients after the live-region text changes.
        /// </summary>
        private void RaiseLiveRegionChanged()
        {
            AutomationPeer peer =
                FrameworkElementAutomationPeer.FromElement(this) ??
                FrameworkElementAutomationPeer.CreatePeerForElement(this);

            peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
            LiveRegionChangeNotificationCount++;
        }

        private static void OnInspectionStatusChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs eventArguments)
        {
            var indicator = (OnboardingStageIndicator)dependencyObject;
            if (indicator._isRestoringInspectionStatus)
            {
                return;
            }

            if (eventArguments.NewValue is not InspectionFooterStatus)
            {
                throw new InvalidOperationException(
                    "The inspection footer received a non-status value.");
            }

            InspectionFooterStatus status =
                (InspectionFooterStatus)eventArguments.NewValue;
            if (!Enum.IsDefined(status))
            {
                if (eventArguments.OldValue is InspectionFooterStatus previous &&
                    Enum.IsDefined(previous))
                {
                    indicator.RestoreInspectionStatus(previous);
                }

                throw new ArgumentOutOfRangeException(
                    nameof(status),
                    status,
                    "The inspection footer status is not recognised.");
            }

            if (indicator.CurrentStage == OnboardingStage.InspectModel)
            {
                indicator.ApplyInspectionStatus(status);
            }
        }

        private void RestoreInspectionStatus(InspectionFooterStatus previous)
        {
            _isRestoringInspectionStatus = true;
            try
            {
                SetValue(InspectionStatusProperty, previous);
            }
            finally
            {
                _isRestoringInspectionStatus = false;
            }
        }

        /// <summary>
        /// Updates every step, connector and accessible description.
        /// </summary>
        private void ApplyStage(OnboardingStage stage)
        {
            int currentStepNumber = (int)stage;

            // Detect accidental enum values that are outside the onboarding flow.
            if (currentStepNumber < 1 || currentStepNumber > 5)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(stage),
                    stage,
                    "The onboarding stage must be between one and five.");
            }

            ApplyStepState(
                ChooseModelStepBox,
                ChooseModelStepValue,
                ChooseModelStepLabel,
                stepNumber: 1,
                currentStepNumber);

            ApplyStepState(
                InspectModelStepBox,
                InspectModelStepValue,
                InspectModelStepLabel,
                stepNumber: 2,
                currentStepNumber);

            ApplyStepState(
                CheckFitStepBox,
                CheckFitStepValue,
                CheckFitStepLabel,
                stepNumber: 3,
                currentStepNumber);

            ApplyStepState(
                ConfigureModelStepBox,
                ConfigureModelStepValue,
                ConfigureModelStepLabel,
                stepNumber: 4,
                currentStepNumber);

            ApplyStepState(
                ReadyToChatStepBox,
                ReadyToChatStepValue,
                ReadyToChatStepLabel,
                stepNumber: 5,
                currentStepNumber);

            // A connector is filled after the user reaches its destination stage.
            ChooseToInspectConnectorScale.ScaleX =
                currentStepNumber >= 2 ? 1 : 0;

            InspectToFitConnectorScale.ScaleX =
                currentStepNumber >= 3 ? 1 : 0;

            FitToConfigureConnectorScale.ScaleX =
                currentStepNumber >= 4 ? 1 : 0;

            ConfigureToReadyConnectorScale.ScaleX =
                currentStepNumber >= 5 ? 1 : 0;

            // Update the small heading above the progress indicator.
            StageEyebrowText.Text =
                $"MODEL SETUP · STEP {currentStepNumber} OF 5";

            // Update the description exposed to accessibility software.
            AutomationProperties.SetName(
                this,
                $"Model setup progress. Step {currentStepNumber} of 5: " +
                $"{GetStageDisplayName(stage)}.");

            if (stage == OnboardingStage.InspectModel)
            {
                ApplyInspectionStatus(InspectionStatus);
            }
        }

        private void ApplyInspectionStatus(InspectionFooterStatus status)
        {
            string eyebrowStatus;
            string automationStatus;

            switch (status)
            {
                case InspectionFooterStatus.InProgress:
                    ApplyActiveState(
                        InspectModelStepBox,
                        InspectModelStepValue,
                        InspectModelStepLabel,
                        stepNumber: 2);
                    eyebrowStatus = "IN PROGRESS";
                    automationStatus = "Inspection in progress";
                    break;

                case InspectionFooterStatus.Complete:
                    ApplyCompletedState(
                        InspectModelStepBox,
                        InspectModelStepValue,
                        InspectModelStepLabel);
                    eyebrowStatus = "COMPLETE";
                    automationStatus = "Inspection complete";
                    break;

                case InspectionFooterStatus.NotComplete:
                    ApplyFutureState(
                        InspectModelStepBox,
                        InspectModelStepValue,
                        InspectModelStepLabel,
                        stepNumber: 2);
                    InspectModelStepValue.Text = "\u2016";
                    InspectModelStepLabel.FontWeight = FontWeights.SemiBold;
                    eyebrowStatus = "NOT COMPLETE";
                    automationStatus = "Inspection not complete";
                    break;

                case InspectionFooterStatus.Interrupted:
                    SolidColorBrush error = GetBrush(ErrorBrushKey);
                    InspectModelStepBox.Background = GetBrush(SurfaceBrushKey);
                    InspectModelStepBox.BorderBrush = error;
                    InspectModelStepValue.Text = "\u2715";
                    InspectModelStepValue.Foreground = error;
                    InspectModelStepLabel.Foreground = error;
                    InspectModelStepLabel.FontWeight = FontWeights.SemiBold;
                    eyebrowStatus = "INTERRUPTED";
                    automationStatus = "Inspection interrupted";
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(status),
                        status,
                        "The inspection footer status is not recognised.");
            }

            StageEyebrowText.Text =
                $"MODEL SETUP · STEP 2 OF 5 {eyebrowStatus}";
            AutomationProperties.SetName(
                this,
                $"Model setup progress. Step 2 of 5: Inspect model. " +
                $"{automationStatus}.");
        }

        /// <summary>
        /// Reports whether a stage belongs to the five-stage onboarding flow.
        /// </summary>
        private static bool IsValidStage(OnboardingStage stage)
        {
            int stageNumber = (int)stage;
            return stageNumber >= 1 && stageNumber <= 5;
        }

        /// <summary>
        /// Selects the completed, active or future appearance for one step.
        /// </summary>
        private void ApplyStepState(
            Border stepBox,
            TextBlock stepValue,
            TextBlock stepLabel,
            int stepNumber,
            int currentStepNumber)
        {
            if (stepNumber < currentStepNumber)
            {
                ApplyCompletedState(
                    stepBox,
                    stepValue,
                    stepLabel);

                return;
            }

            if (stepNumber == currentStepNumber)
            {
                ApplyActiveState(
                    stepBox,
                    stepValue,
                    stepLabel,
                    stepNumber);

                return;
            }

            ApplyFutureState(
                stepBox,
                stepValue,
                stepLabel,
                stepNumber);
        }

        /// <summary>
        /// Displays a completed step using a blue checkmark state.
        /// </summary>
        private void ApplyCompletedState(
            Border stepBox,
            TextBlock stepValue,
            TextBlock stepLabel)
        {
            stepBox.Background = GetBrush(ActiveBrushKey);
            stepBox.BorderBrush = GetBrush(ActiveBrushKey);

            // Replace the stage number with a completion checkmark.
            stepValue.Text = "✓";
            stepValue.Foreground = GetBrush(SurfaceBrushKey);

            stepLabel.Foreground = GetBrush(ActiveBrushKey);
            stepLabel.FontWeight = FontWeights.SemiBold;
        }

        /// <summary>
        /// Displays the stage currently being performed.
        /// </summary>
        private void ApplyActiveState(
            Border stepBox,
            TextBlock stepValue,
            TextBlock stepLabel,
            int stepNumber)
        {
            stepBox.Background = GetBrush(ActiveBrushKey);
            stepBox.BorderBrush = GetBrush(ActiveBrushKey);

            stepValue.Text = stepNumber.ToString();
            stepValue.Foreground = GetBrush(SurfaceBrushKey);

            stepLabel.Foreground = GetBrush(PrimaryTextBrushKey);
            stepLabel.FontWeight = FontWeights.SemiBold;
        }

        /// <summary>
        /// Displays a stage that has not yet been reached.
        /// </summary>
        private void ApplyFutureState(
            Border stepBox,
            TextBlock stepValue,
            TextBlock stepLabel,
            int stepNumber)
        {
            stepBox.Background = GetBrush(InactiveSurfaceBrushKey);
            stepBox.BorderBrush = GetBrush(InactiveBorderBrushKey);

            stepValue.Text = stepNumber.ToString();
            stepValue.Foreground = GetBrush(MutedTextBrushKey);

            stepLabel.Foreground = GetBrush(SecondaryTextBrushKey);
            stepLabel.FontWeight = FontWeights.Normal;
        }

        /// <summary>
        /// Retrieves a SolidColorBrush declared in the control's XAML resources.
        /// </summary>
        private SolidColorBrush GetBrush(string resourceKey)
        {
            object resource = Resources[resourceKey];

            if (resource is SolidColorBrush brush)
            {
                return brush;
            }

            throw new InvalidOperationException(
                $"The onboarding indicator resource '{resourceKey}' " +
                "is missing or is not a SolidColorBrush.");
        }

        /// <summary>
        /// Returns the readable stage name used by accessibility software.
        /// </summary>
        private static string GetStageDisplayName(OnboardingStage stage)
        {
            return stage switch
            {
                OnboardingStage.ImportModel => "Choose model",
                OnboardingStage.InspectModel => "Inspect model",
                OnboardingStage.CheckHardwareFit => "Check hardware fit",
                OnboardingStage.ConfigureModel => "Configure model",
                OnboardingStage.ReadyToChat => "Ready to chat",

                // Reject any stage that was not defined by the onboarding flow.
                _ => throw new ArgumentOutOfRangeException(
                    nameof(stage),
                    stage,
                    "The onboarding stage is not recognised.")
            };
        }
    }
}
