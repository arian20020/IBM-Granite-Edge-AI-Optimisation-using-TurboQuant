using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using Windows.UI.ViewManagement;
using GraniteEdgeAI.Features.Onboarding;
using GraniteEdgeAI.Features.ModelInspection.Controls;
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
        private bool _hasAppliedStage;
        private readonly UISettings _uiSettings = new();
        private Storyboard? _chooseToInspectConnectorStoryboard;
        private Storyboard? _inspectToFitConnectorStoryboard;
        private Storyboard? _fitToConfigureConnectorStoryboard;
        private Storyboard? _configureToReadyConnectorStoryboard;

        // Resource keys used when applying the visual state of each step.
        private const string ActiveBrushKey =
            "OnboardingIndicatorActiveBrush";

        private const string ActiveSurfaceBrushKey =
            "OnboardingIndicatorActiveSurfaceBrush";

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

        private const string SuccessSurfaceBrushKey =
            "OnboardingIndicatorSuccessSurfaceBrush";

        private const string SuccessBorderBrushKey =
            "OnboardingIndicatorSuccessBorderBrush";

        private const string SuccessTextBrushKey =
            "OnboardingIndicatorSuccessTextBrush";

        private const string NotCompleteSurfaceBrushKey =
            "OnboardingIndicatorNotCompleteSurfaceBrush";

        private const string NotCompleteBorderBrushKey =
            "OnboardingIndicatorNotCompleteBorderBrush";

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
            ActualThemeChanged += (_, _) => ApplyStage(CurrentStage);
            _uiSettings.AnimationsEnabledChanged +=
                UiSettings_AnimationsEnabledChanged;
            Unloaded += OnboardingStageIndicator_Unloaded;
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

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new OnboardingStageIndicatorAutomationPeer(this);
        }

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

            // ensure the new value is a valid onboarding-stage value
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
            AutomationPeer? peer =
                FrameworkElementAutomationPeer.FromElement(this) ??
                FrameworkElementAutomationPeer.CreatePeerForElement(this);

            // a retained shell can advance while this control is temporarily
            // disconnected from the visual tree. WinUI is then allowed to
            // return no automation peer; accessibility notification is
            // best-effort and must never terminate the onboarding journey
            if (peer is null)
            {
                return;
            }

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
                if (eventArguments.OldValue is InspectionFooterStatus previousStatus &&
                    previousStatus == status)
                {
                    return;
                }

                indicator.ApplyInspectionStatus(status);
                indicator.RaiseLiveRegionChanged();
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
                ChooseModelStepItem,
                ChooseModelStepBox,
                ChooseModelStepValue,
                ChooseModelStepGlyph,
                ChooseModelStepMark,
                ChooseModelStepLabel,
                stepNumber: 1,
                currentStepNumber);

            ApplyStepState(
                InspectModelStepItem,
                InspectModelStepBox,
                InspectModelStepValue,
                InspectModelStepGlyph,
                InspectModelStepMark,
                InspectModelStepLabel,
                stepNumber: 2,
                currentStepNumber);

            ApplyStepState(
                CheckFitStepItem,
                CheckFitStepBox,
                CheckFitStepValue,
                CheckFitStepGlyph,
                CheckFitStepMark,
                CheckFitStepLabel,
                stepNumber: 3,
                currentStepNumber);

            ApplyStepState(
                ConfigureModelStepItem,
                ConfigureModelStepBox,
                ConfigureModelStepValue,
                ConfigureModelStepGlyph,
                ConfigureModelStepMark,
                ConfigureModelStepLabel,
                stepNumber: 4,
                currentStepNumber);

            ApplyStepState(
                ReadyToChatStepItem,
                ReadyToChatStepBox,
                ReadyToChatStepValue,
                ReadyToChatStepGlyph,
                ReadyToChatStepMark,
                ReadyToChatStepLabel,
                stepNumber: 5,
                currentStepNumber);

            // a connector is filled after the user reaches its destination stage
            ApplyConnectorProgress(
                ChooseToInspectConnectorScale,
                currentStepNumber >= 2,
                ref _chooseToInspectConnectorStoryboard,
                _hasAppliedStage);
            ApplyConnectorProgress(
                InspectToFitConnectorScale,
                currentStepNumber >= 3,
                ref _inspectToFitConnectorStoryboard,
                _hasAppliedStage);
            ApplyConnectorProgress(
                FitToConfigureConnectorScale,
                currentStepNumber >= 4,
                ref _fitToConfigureConnectorStoryboard,
                _hasAppliedStage);
            ApplyConnectorProgress(
                ConfigureToReadyConnectorScale,
                currentStepNumber >= 5,
                ref _configureToReadyConnectorStoryboard,
                _hasAppliedStage);
            _hasAppliedStage = true;

            // update the small heading above the progress indicator
            StageEyebrowText.Text =
                $"MODEL SETUP · STEP {currentStepNumber} OF 5";

            // update the description exposed to accessibility software
            AutomationProperties.SetName(
                this,
                $"Model setup. {GetStageDisplayName(stage)}, step {currentStepNumber} of 5.");
            AutomationProperties.SetItemStatus(this, string.Empty);

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
                        InspectModelStepGlyph,
                        InspectModelStepMark,
                        InspectModelStepLabel,
                        stepNumber: 2);
                    eyebrowStatus = "IN PROGRESS";
                    automationStatus = "Inspection in progress";
                    break;

                case InspectionFooterStatus.Complete:
                    ApplyActiveState(
                        InspectModelStepBox,
                        InspectModelStepValue,
                        InspectModelStepGlyph,
                        InspectModelStepMark,
                        InspectModelStepLabel,
                        stepNumber: 2);
                    eyebrowStatus = "COMPLETE";
                    automationStatus = "Inspection complete";
                    break;

                case InspectionFooterStatus.NotComplete:
                    ApplyFutureState(
                        InspectModelStepBox,
                        InspectModelStepValue,
                        InspectModelStepGlyph,
                        InspectModelStepMark,
                        InspectModelStepLabel,
                        stepNumber: 2);
                    InspectModelStepBox.Background =
                        GetBrush(NotCompleteSurfaceBrushKey);
                    InspectModelStepBox.BorderBrush =
                        GetBrush(NotCompleteBorderBrushKey);
                    InspectModelStepBox.BorderThickness = new Thickness(2);
                    InspectModelStepBox.Opacity = 1;
                    InspectModelStepValue.Visibility = Visibility.Collapsed;
                    InspectModelStepGlyph.Kind =
                        InspectionStatusGlyphKind.NotComplete;
                    InspectModelStepGlyph.Visibility = Visibility.Visible;
                    InspectModelStepGlyph.Opacity = 0;
                    InspectModelStepMark.Glyph = "\uE769";
                    InspectModelStepMark.Foreground =
                        GetBrush(NotCompleteBorderBrushKey);
                    InspectModelStepMark.Visibility = Visibility.Visible;
                    InspectModelStepLabel.FontWeight = FontWeights.SemiBold;
                    eyebrowStatus = "NOT COMPLETE";
                    automationStatus = "Inspection not complete";
                    break;

                case InspectionFooterStatus.Interrupted:
                    SolidColorBrush error = GetBrush(ErrorBrushKey);
                    InspectModelStepBox.Background = GetBrush(SurfaceBrushKey);
                    InspectModelStepBox.BorderBrush = error;
                    InspectModelStepBox.BorderThickness = new Thickness(2);
                    InspectModelStepBox.Opacity = 1;
                    InspectModelStepValue.Visibility = Visibility.Collapsed;
                    InspectModelStepGlyph.Kind = InspectionStatusGlyphKind.Error;
                    InspectModelStepGlyph.Visibility = Visibility.Visible;
                    InspectModelStepGlyph.Opacity = 0;
                    InspectModelStepMark.Glyph = "\uE711";
                    InspectModelStepMark.Foreground = error;
                    InspectModelStepMark.Visibility = Visibility.Visible;
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
            AutomationProperties.SetItemStatus(this, automationStatus);

            // the journey stage remains current even when the inspection's
            // internal operation reaches a terminal result
            AutomationProperties.SetName(
                InspectModelStepItem,
                "Inspect model. Current. Step 2 of 5.");
            AutomationProperties.SetItemStatus(InspectModelStepItem, "Current");
            AutomationProperties.SetPositionInSet(InspectModelStepItem, 2);
            AutomationProperties.SetSizeOfSet(InspectModelStepItem, 5);
        }

        /// <summary>
        /// reports whether a stage belongs to the five-stage onboarding flow
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
            ListViewItem stepItem,
            Border stepBox,
            TextBlock stepValue,
            InspectionStatusGlyph stepGlyph,
            FontIcon stepMark,
            TextBlock stepLabel,
            int stepNumber,
            int currentStepNumber)
        {
            string itemStatus;
            if (stepNumber < currentStepNumber)
            {
                ApplyCompletedState(
                    stepBox,
                    stepValue,
                    stepGlyph,
                    stepMark,
                    stepLabel);

                itemStatus = "Completed";
            }
            else if (stepNumber == currentStepNumber)
            {
                ApplyActiveState(
                    stepBox,
                    stepValue,
                    stepGlyph,
                    stepMark,
                    stepLabel,
                    stepNumber);

                itemStatus = "Current";
            }
            else
            {
                ApplyFutureState(
                    stepBox,
                    stepValue,
                    stepGlyph,
                    stepMark,
                    stepLabel,
                    stepNumber);
                itemStatus = "Not started";
            }

            string title = GetStageDisplayName((OnboardingStage)stepNumber);
            AutomationProperties.SetName(
                stepItem,
                $"{title}. {itemStatus}. Step {stepNumber} of 5.");
            AutomationProperties.SetItemStatus(stepItem, itemStatus);
            AutomationProperties.SetPositionInSet(stepItem, stepNumber);
            AutomationProperties.SetSizeOfSet(stepItem, 5);
            FrameworkElementAutomationPeer.FromElement(stepItem)?.InvalidatePeer();
        }

        /// <summary>
        /// Displays a completed step using a semantic success surface.
        /// </summary>
        private void ApplyCompletedState(
            Border stepBox,
            TextBlock stepValue,
            InspectionStatusGlyph stepGlyph,
            FontIcon stepMark,
            TextBlock stepLabel)
        {
            stepBox.Background = GetBrush(SuccessSurfaceBrushKey);
            stepBox.BorderBrush = GetBrush(SuccessBorderBrushKey);
            stepBox.BorderThickness = new Thickness(2);
            stepBox.Opacity = 1;

            stepValue.Visibility = Visibility.Collapsed;
            stepGlyph.Kind = InspectionStatusGlyphKind.Success;
            stepGlyph.Visibility = Visibility.Visible;
            stepGlyph.Opacity = 0;
            stepMark.Glyph = "\uE73E";
            stepMark.Foreground = GetBrush(SuccessTextBrushKey);
            stepMark.Visibility = Visibility.Visible;

            stepLabel.Foreground = GetBrush(SuccessTextBrushKey);
            stepLabel.FontWeight = FontWeights.SemiBold;
        }

        /// <summary>
        /// Displays the stage currently being performed.
        /// </summary>
        private void ApplyActiveState(
            Border stepBox,
            TextBlock stepValue,
            InspectionStatusGlyph stepGlyph,
            FontIcon stepMark,
            TextBlock stepLabel,
            int stepNumber)
        {
            stepBox.Background = GetBrush(ActiveSurfaceBrushKey);
            stepBox.BorderBrush = GetBrush(ActiveBrushKey);
            stepBox.BorderThickness = new Thickness(2);
            stepBox.Opacity = 1;

            stepValue.Text = stepNumber.ToString();
            stepValue.Foreground = GetBrush(ActiveBrushKey);
            stepValue.Visibility = Visibility.Visible;
            stepGlyph.Visibility = Visibility.Collapsed;
            stepGlyph.Opacity = 1;
            stepMark.Visibility = Visibility.Collapsed;

            stepLabel.Foreground = GetBrush(PrimaryTextBrushKey);
            stepLabel.FontWeight = FontWeights.SemiBold;
        }

        /// <summary>
        /// Displays a stage that has not yet been reached.
        /// </summary>
        private void ApplyFutureState(
            Border stepBox,
            TextBlock stepValue,
            InspectionStatusGlyph stepGlyph,
            FontIcon stepMark,
            TextBlock stepLabel,
            int stepNumber)
        {
            stepBox.Background = GetBrush(InactiveSurfaceBrushKey);
            stepBox.BorderBrush = GetBrush(InactiveBorderBrushKey);
            stepBox.BorderThickness = new Thickness(2);
            stepBox.Opacity = 1;

            stepValue.Text = stepNumber.ToString();
            stepValue.Foreground = GetBrush(MutedTextBrushKey);
            stepValue.Visibility = Visibility.Visible;
            stepGlyph.Visibility = Visibility.Collapsed;
            stepGlyph.Opacity = 1;
            stepMark.Visibility = Visibility.Collapsed;

            stepLabel.Foreground = GetBrush(SecondaryTextBrushKey);
            stepLabel.FontWeight = FontWeights.Normal;
        }

        private void ApplyConnectorProgress(
            ScaleTransform connectorScale,
            bool isComplete,
            ref Storyboard? activeStoryboard,
            bool animate)
        {
            double target = isComplete ? 1d : 0d;
            if (Math.Abs(connectorScale.ScaleX - target) < 0.001d)
            {
                return;
            }

            double start = connectorScale.ScaleX;
            activeStoryboard?.Stop();
            activeStoryboard = null;

            if (!animate || !_uiSettings.AnimationsEnabled)
            {
                connectorScale.ScaleX = target;
                return;
            }

            var animation = new DoubleAnimation
            {
                From = start,
                To = target,
                Duration = new Duration(TimeSpan.FromMilliseconds(220)),
                EnableDependentAnimation = true,
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            };
            Storyboard.SetTarget(animation, connectorScale);
            Storyboard.SetTargetProperty(animation, nameof(ScaleTransform.ScaleX));
            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            connectorScale.ScaleX = target;
            activeStoryboard = storyboard;
            storyboard.Begin();
        }

        private void UiSettings_AnimationsEnabledChanged(
            UISettings sender,
            UISettingsAnimationsEnabledChangedEventArgs args)
        {
            if (!sender.AnimationsEnabled)
            {
                StopConnectorAnimationsAndSnap();
            }
        }

        private void OnboardingStageIndicator_Unloaded(
            object sender,
            RoutedEventArgs args)
        {
            _uiSettings.AnimationsEnabledChanged -=
                UiSettings_AnimationsEnabledChanged;
            Unloaded -= OnboardingStageIndicator_Unloaded;
            StopConnectorAnimationsAndSnap();
        }

        private void StopConnectorAnimationsAndSnap()
        {
            _chooseToInspectConnectorStoryboard?.Stop();
            _inspectToFitConnectorStoryboard?.Stop();
            _fitToConfigureConnectorStoryboard?.Stop();
            _configureToReadyConnectorStoryboard?.Stop();
            _chooseToInspectConnectorStoryboard = null;
            _inspectToFitConnectorStoryboard = null;
            _fitToConfigureConnectorStoryboard = null;
            _configureToReadyConnectorStoryboard = null;

            int currentStepNumber = (int)CurrentStage;
            ChooseToInspectConnectorScale.ScaleX =
                currentStepNumber >= 2 ? 1d : 0d;
            InspectToFitConnectorScale.ScaleX =
                currentStepNumber >= 3 ? 1d : 0d;
            FitToConfigureConnectorScale.ScaleX =
                currentStepNumber >= 4 ? 1d : 0d;
            ConfigureToReadyConnectorScale.ScaleX =
                currentStepNumber >= 5 ? 1d : 0d;
        }

        /// <summary>
        /// Retrieves a SolidColorBrush declared in the control's XAML resources.
        /// </summary>
        private SolidColorBrush GetBrush(string resourceKey)
        {
            string semanticKey = resourceKey switch
            {
                ActiveBrushKey => "GraniteAppAccentBrush",
                ActiveSurfaceBrushKey => "GraniteAppAccentSurfaceBrush",
                SurfaceBrushKey => "GraniteAppSurfaceBrush",
                InactiveSurfaceBrushKey => "GraniteAppMutedSurfaceBrush",
                InactiveBorderBrushKey => "GraniteAppStrokeBrush",
                PrimaryTextBrushKey => "GraniteAppTextPrimaryBrush",
                SecondaryTextBrushKey => "GraniteAppTextSecondaryBrush",
                MutedTextBrushKey => "GraniteAppPendingBrush",
                ErrorBrushKey => "InspectionErrorTextBrush",
                SuccessSurfaceBrushKey => "InspectionSuccessSurfaceBrush",
                SuccessBorderBrushKey => "InspectionSuccessBorderBrush",
                SuccessTextBrushKey => "InspectionSuccessTextBrush",
                NotCompleteSurfaceBrushKey => "InspectionSurfaceMutedBrush",
                NotCompleteBorderBrushKey => "InspectionBorderMutedBrush",
                _ => resourceKey
            };
            string theme = new AccessibilitySettings().HighContrast ? "HighContrast" : ActualTheme.ToString();
            object? resource = FindThemeResource(Application.Current.Resources, semanticKey, theme);

            if (resource is SolidColorBrush brush)
            {
                return brush;
            }

            throw new InvalidOperationException(
                $"The onboarding indicator resource '{resourceKey}' " +
                "is missing or is not a SolidColorBrush.");
        }

        /// <summary>
        /// returns the readable stage name used by accessibility software
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

                // reject any stage that was not defined by the onboarding flow
                _ => throw new ArgumentOutOfRangeException(
                    nameof(stage),
                    stage,
                    "The onboarding stage is not recognised.")
            };
        }

        private static object? FindThemeResource(ResourceDictionary dictionary, string key, string theme)
        {
            if (dictionary.ThemeDictionaries.TryGetValue(theme, out object? themed)
                && themed is ResourceDictionary themeDictionary && themeDictionary.TryGetValue(key, out object? value))
                return value;
            for (int index = dictionary.MergedDictionaries.Count - 1; index >= 0; index--)
            {
                object? found = FindThemeResource(dictionary.MergedDictionaries[index], key, theme);
                if (found is not null) return found;
            }
            return dictionary.TryGetValue(key, out object? fallback) ? fallback : null;
        }

        private sealed class OnboardingStageIndicatorAutomationPeer
            : FrameworkElementAutomationPeer
        {
            internal OnboardingStageIndicatorAutomationPeer(
                OnboardingStageIndicator owner)
                : base(owner)
            {
            }

            protected override AutomationControlType GetAutomationControlTypeCore()
            {
                return AutomationControlType.Group;
            }

            protected override string GetClassNameCore()
            {
                return nameof(OnboardingStageIndicator);
            }
        }
    }

    internal sealed class OnboardingStageListItem : ListViewItem
    {
        protected override AutomationPeer OnCreateAutomationPeer() =>
            new OnboardingStageListItemAutomationPeer(this);

        private sealed class OnboardingStageListItemAutomationPeer
            : ListViewItemAutomationPeer
        {
            private readonly OnboardingStageListItem owner;

            internal OnboardingStageListItemAutomationPeer(OnboardingStageListItem owner)
                : base(owner)
            {
                this.owner = owner;
            }

            protected override string GetNameCore() =>
                AutomationProperties.GetName(owner) ?? string.Empty;

            protected override string GetItemStatusCore() =>
                AutomationProperties.GetItemStatus(owner) ?? string.Empty;

            protected override int GetPositionInSetCore() =>
                AutomationProperties.GetPositionInSet(owner);

            protected override int GetSizeOfSetCore() =>
                AutomationProperties.GetSizeOfSet(owner);
        }
    }
}
