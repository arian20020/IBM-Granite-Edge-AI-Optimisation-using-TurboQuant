using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Numerics;

namespace GraniteEdgeAI.Features.ModelInspection.Controls;

/// <summary>
/// Renders one semantic inspection status as application-owned vector geometry.
/// </summary>
public sealed partial class InspectionStatusGlyph : UserControl
{
    private const double DefaultSurfaceSize = 30d;
    private const string RotationPropertyName = "RotationAngleInDegrees";

    private bool _isInitialized;
    private bool _isLoaded;
    private bool _isRestoringKind;
    private bool _isRestoringSurfaceSize;
    private bool _isNormalizingStageNumber;

    /// <summary>
    /// Identifies the semantic glyph-kind dependency property.
    /// </summary>
    public static readonly DependencyProperty KindProperty =
        DependencyProperty.Register(
            nameof(Kind),
            typeof(InspectionStatusGlyphKind),
            typeof(InspectionStatusGlyph),
            new PropertyMetadata(
                InspectionStatusGlyphKind.Success,
                OnKindChanged));

    /// <summary>
    /// Identifies the approved glyph-surface-size dependency property.
    /// </summary>
    public static readonly DependencyProperty SurfaceSizeProperty =
        DependencyProperty.Register(
            nameof(SurfaceSize),
            typeof(double),
            typeof(InspectionStatusGlyph),
            new PropertyMetadata(
                DefaultSurfaceSize,
                OnSurfaceSizeChanged));

    /// <summary>
    /// Identifies the optional waiting-stage-number dependency property.
    /// </summary>
    public static readonly DependencyProperty StageNumberProperty =
        DependencyProperty.Register(
            nameof(StageNumber),
            typeof(string),
            typeof(InspectionStatusGlyph),
            new PropertyMetadata(
                string.Empty,
                OnStageNumberChanged));

    /// <summary>
    /// Identifies the reduced-motion-aware orbit opt-in dependency property.
    /// </summary>
    public static readonly DependencyProperty IsMotionEnabledProperty =
        DependencyProperty.Register(
            nameof(IsMotionEnabled),
            typeof(bool),
            typeof(InspectionStatusGlyph),
            new PropertyMetadata(
                false,
                OnIsMotionEnabledChanged));

    /// <summary>
    /// Creates a static, success glyph on the standard 30-pixel surface.
    /// </summary>
    public InspectionStatusGlyph()
    {
        InitializeComponent();
        _isInitialized = true;

        Loaded += InspectionStatusGlyph_Loaded;
        Unloaded += InspectionStatusGlyph_Unloaded;

        ApplySurfaceSize(SurfaceSize);
        ApplyStageNumber(StageNumber);
        ApplyKind(Kind);
    }

    /// <summary>
    /// Gets or sets the semantic vector mark displayed by the control.
    /// </summary>
    public InspectionStatusGlyphKind Kind
    {
        get => (InspectionStatusGlyphKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>
    /// Gets or sets the exact square surface occupied by the glyph.
    /// </summary>
    public double SurfaceSize
    {
        get => (double)GetValue(SurfaceSizeProperty);
        set => SetValue(SurfaceSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the single decorative character shown by a waiting glyph.
    /// </summary>
    public string StageNumber
    {
        get => (string?)GetValue(StageNumberProperty) ?? string.Empty;
        set => SetValue(StageNumberProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the Active glyph may use its precision orbit.
    /// </summary>
    public bool IsMotionEnabled
    {
        get => (bool)GetValue(IsMotionEnabledProperty);
        set => SetValue(IsMotionEnabledProperty, value);
    }

    internal bool IsPrecisionOrbitRunning { get; private set; }

    internal int PrecisionOrbitStartCount { get; private set; }

    internal int PrecisionOrbitStopCount { get; private set; }

    internal ScalarKeyFrameAnimation? PrecisionOrbitAnimation { get; private set; }

    internal CompositionEasingFunction? PrecisionOrbitEasing { get; private set; }

    private static void OnKindChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArguments)
    {
        var glyph = (InspectionStatusGlyph)dependencyObject;
        if (glyph._isRestoringKind)
        {
            return;
        }

        if (eventArguments.NewValue is not InspectionStatusGlyphKind kind ||
            !Enum.IsDefined(kind))
        {
            InspectionStatusGlyphKind previous =
                eventArguments.OldValue is InspectionStatusGlyphKind oldKind &&
                Enum.IsDefined(oldKind)
                    ? oldKind
                    : InspectionStatusGlyphKind.Success;
            glyph.RestoreKind(previous);
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                eventArguments.NewValue,
                "The inspection status glyph kind is not recognised.");
        }

        if (glyph._isInitialized)
        {
            glyph.ApplyKind(kind);
        }
    }

    private static void OnSurfaceSizeChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArguments)
    {
        var glyph = (InspectionStatusGlyph)dependencyObject;
        if (glyph._isRestoringSurfaceSize)
        {
            return;
        }

        if (eventArguments.NewValue is not double surfaceSize ||
            !IsApprovedSurfaceSize(surfaceSize))
        {
            double previous =
                eventArguments.OldValue is double oldSize &&
                IsApprovedSurfaceSize(oldSize)
                    ? oldSize
                    : DefaultSurfaceSize;
            glyph.RestoreSurfaceSize(previous);
            throw new ArgumentOutOfRangeException(
                nameof(surfaceSize),
                eventArguments.NewValue,
                "The glyph surface size must be 22, 30, 36, or 40 pixels.");
        }

        if (glyph._isInitialized)
        {
            glyph.ApplySurfaceSize(surfaceSize);
        }
    }

    private static void OnStageNumberChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArguments)
    {
        var glyph = (InspectionStatusGlyph)dependencyObject;
        if (glyph._isNormalizingStageNumber)
        {
            return;
        }

        string normalized = NormalizeStageNumber(eventArguments.NewValue as string);
        if (!string.Equals(
                eventArguments.NewValue as string,
                normalized,
                StringComparison.Ordinal))
        {
            glyph._isNormalizingStageNumber = true;
            try
            {
                glyph.SetValue(StageNumberProperty, normalized);
            }
            finally
            {
                glyph._isNormalizingStageNumber = false;
            }
        }

        if (glyph._isInitialized)
        {
            glyph.ApplyStageNumber(normalized);
        }
    }

    private static void OnIsMotionEnabledChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArguments)
    {
        var glyph = (InspectionStatusGlyph)dependencyObject;
        if (glyph._isInitialized)
        {
            glyph.UpdatePrecisionOrbitState();
        }
    }

    private static bool IsApprovedSurfaceSize(double surfaceSize) =>
        surfaceSize is 22d or 30d or 36d or 40d;

    private static string NormalizeStageNumber(string? stageNumber)
    {
        string trimmed = stageNumber?.Trim() ?? string.Empty;
        return trimmed.Length == 0 ? string.Empty : trimmed[..1];
    }

    private void RestoreKind(InspectionStatusGlyphKind previous)
    {
        _isRestoringKind = true;
        try
        {
            SetValue(KindProperty, previous);
        }
        finally
        {
            _isRestoringKind = false;
        }
    }

    private void RestoreSurfaceSize(double previous)
    {
        _isRestoringSurfaceSize = true;
        try
        {
            SetValue(SurfaceSizeProperty, previous);
        }
        finally
        {
            _isRestoringSurfaceSize = false;
        }
    }

    private void ApplySurfaceSize(double surfaceSize)
    {
        Width = surfaceSize;
        Height = surfaceSize;
        VectorViewbox.Width = surfaceSize;
        VectorViewbox.Height = surfaceSize;
    }

    private void ApplyStageNumber(string stageNumber)
    {
        WaitingStageNumber.Text = stageNumber;
    }

    private void ApplyKind(InspectionStatusGlyphKind kind)
    {
        SuccessRoot.Visibility = kind == InspectionStatusGlyphKind.Success
            ? Visibility.Visible
            : Visibility.Collapsed;
        WarningRoot.Visibility = kind == InspectionStatusGlyphKind.Warning
            ? Visibility.Visible
            : Visibility.Collapsed;
        ErrorRoot.Visibility = kind == InspectionStatusGlyphKind.Error
            ? Visibility.Visible
            : Visibility.Collapsed;
        InformationRoot.Visibility = kind == InspectionStatusGlyphKind.Information
            ? Visibility.Visible
            : Visibility.Collapsed;
        WaitingRoot.Visibility = kind == InspectionStatusGlyphKind.Waiting
            ? Visibility.Visible
            : Visibility.Collapsed;
        ActiveRoot.Visibility = kind == InspectionStatusGlyphKind.Active
            ? Visibility.Visible
            : Visibility.Collapsed;
        NotCompleteRoot.Visibility = kind == InspectionStatusGlyphKind.NotComplete
            ? Visibility.Visible
            : Visibility.Collapsed;

        UpdatePrecisionOrbitState();
    }

    private void InspectionStatusGlyph_Loaded(
        object sender,
        RoutedEventArgs eventArguments)
    {
        _isLoaded = true;
        UpdatePrecisionOrbitState();
    }

    private void InspectionStatusGlyph_Unloaded(
        object sender,
        RoutedEventArgs eventArguments)
    {
        _isLoaded = false;
        UpdatePrecisionOrbitState();
    }

    private void UpdatePrecisionOrbitState()
    {
        bool shouldRun =
            _isLoaded &&
            Kind == InspectionStatusGlyphKind.Active &&
            IsMotionEnabled;

        if (shouldRun)
        {
            StartPrecisionOrbit();
        }
        else
        {
            StopPrecisionOrbit();
        }
    }

    private void StartPrecisionOrbit()
    {
        if (IsPrecisionOrbitRunning)
        {
            return;
        }

        Visual orbitVisual = ElementCompositionPreview.GetElementVisual(
            PrecisionOrbitRotationTarget);
        orbitVisual.CenterPoint = new Vector3(12f, 12f, 0f);
        orbitVisual.RotationAngleInDegrees = 0f;

        Compositor compositor = orbitVisual.Compositor;
        CompositionEasingFunction easing =
            compositor.CreateLinearEasingFunction();
        ScalarKeyFrameAnimation animation =
            compositor.CreateScalarKeyFrameAnimation();
        animation.Duration = ModelInspectionMotionSpec.PrecisionOrbitDuration;
        animation.IterationBehavior = AnimationIterationBehavior.Forever;
        animation.InsertKeyFrame(0f, 0f, easing);
        animation.InsertKeyFrame(1f, 360f, easing);

        PrecisionOrbitEasing = easing;
        PrecisionOrbitAnimation = animation;
        orbitVisual.StartAnimation(RotationPropertyName, animation);
        IsPrecisionOrbitRunning = true;
        PrecisionOrbitStartCount++;
    }

    private void StopPrecisionOrbit()
    {
        Visual orbitVisual = ElementCompositionPreview.GetElementVisual(
            PrecisionOrbitRotationTarget);
        if (IsPrecisionOrbitRunning)
        {
            orbitVisual.StopAnimation(RotationPropertyName);
            IsPrecisionOrbitRunning = false;
            PrecisionOrbitStopCount++;
        }

        orbitVisual.RotationAngleInDegrees = 0f;
        PrecisionOrbitAnimation = null;
        PrecisionOrbitEasing = null;
    }
}
