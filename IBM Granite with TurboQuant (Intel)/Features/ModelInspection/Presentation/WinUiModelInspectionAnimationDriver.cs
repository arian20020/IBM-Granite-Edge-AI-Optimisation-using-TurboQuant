using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Windows.Foundation;

namespace GraniteEdgeAI.Features.ModelInspection.Presentation;

internal enum ModelInspectionAnimatedProperty
{
    Opacity = 0,
    TranslationY = 1,
    RotationInDegrees = 2,
    RevealProgress = 3
}

internal readonly record struct ModelInspectionScalarAnimation(
    UIElement Target,
    ModelInspectionAnimatedProperty Property,
    double From,
    double To,
    TimeSpan Duration,
    bool StartsFromCurrentValue = false,
    double CurrentValueAdjustment = 0d);

internal enum ModelInspectionCompositionTarget
{
    Visual = 0,
    InsetClip = 1
}

internal readonly record struct ModelInspectionCompositionAnimationPlan(
    UIElement Target,
    ModelInspectionCompositionTarget TargetKind,
    string PropertyName,
    double From,
    double To,
    TimeSpan Duration,
    Vector2 ControlPoint1,
    Vector2 ControlPoint2,
    bool StartsFromCurrentValue,
    bool EnablesTranslation,
    double StartingValueAdjustment)
{
    internal const string StartingValueParameterName =
        "startingValueAdjustment";

    internal double BaseValue => To;

    internal string? StartingExpression =>
        !StartsFromCurrentValue
            ? null
            : StartingValueAdjustment == 0d
                ? "this.StartingValue"
                : $"this.StartingValue + {StartingValueParameterName}";
}

internal interface IModelInspectionCompositionAnimationBackend : IDisposable
{
    double GetTop(UIElement target);

    void Start(
        IReadOnlyList<ModelInspectionScalarAnimation> animations,
        Vector2 controlPoint1,
        Vector2 controlPoint2,
        Action completed);

    void CancelAll();
}

internal sealed class WinUiModelInspectionAnimationDriver :
    IModelInspectionAnimationDriver
{
    private readonly ModelInspectionMotionSpec spec;
    private readonly IModelInspectionCompositionAnimationBackend backend;
    private readonly Dictionary<
        UIElement,
        Dictionary<ModelInspectionAnimatedProperty, long>> targetOperations =
        new(ReferenceEqualityComparer.Instance);
    private long nextOperation;
    private long cancellationRevision;
    private bool isDisposed;

    internal WinUiModelInspectionAnimationDriver(ModelInspectionMotionSpec spec)
        : this(spec, new WinUiCompositionAnimationBackend())
    {
    }

    internal WinUiModelInspectionAnimationDriver(
        ModelInspectionMotionSpec spec,
        IModelInspectionCompositionAnimationBackend backend)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
        this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    internal int TrackedTargetCount => targetOperations.Count;

    public void StartStageStatus(
        UIElement target,
        ModelInspectionVisualOperationKey key,
        Action<ModelInspectionVisualOperationKey> completed)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(completed);

        Start(
            [
                new ModelInspectionScalarAnimation(
                    target,
                    ModelInspectionAnimatedProperty.Opacity,
                    spec.StatusOpacityFrom,
                    spec.StatusOpacityTo,
                    spec.FastDuration)
            ],
            () => completed(key));
    }

    public void StartActiveDetail(
        UIElement target,
        ModelInspectionVisualOperationKey key,
        Action<ModelInspectionVisualOperationKey> completed)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(completed);

        Start(
            [
                new ModelInspectionScalarAnimation(
                    target,
                    ModelInspectionAnimatedProperty.Opacity,
                    spec.ActiveDetailOpacityFrom,
                    spec.ActiveDetailOpacityTo,
                    spec.StandardDuration),
                new ModelInspectionScalarAnimation(
                    target,
                    ModelInspectionAnimatedProperty.TranslationY,
                    spec.ActiveDetailOffsetYFrom,
                    spec.ActiveDetailOffsetYTo,
                    spec.StandardDuration)
            ],
            () => completed(key));
    }

    public void StartDisclosure(
        UIElement chevron,
        FrameworkElement viewport,
        IReadOnlyList<UIElement> followingElements,
        bool isExpanded,
        IReadOnlyList<double> previousTopOffsets,
        ModelInspectionVisualOperationKey key,
        Action<ModelInspectionVisualOperationKey> completed)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(chevron);
        ArgumentNullException.ThrowIfNull(viewport);
        ArgumentNullException.ThrowIfNull(followingElements);
        ArgumentNullException.ThrowIfNull(previousTopOffsets);
        ArgumentNullException.ThrowIfNull(completed);
        if (followingElements.Count != previousTopOffsets.Count)
        {
            throw new ArgumentException(
                "A previous top offset is required for every following element.",
                nameof(previousTopOffsets));
        }

        List<ModelInspectionScalarAnimation> animations =
        [
            new ModelInspectionScalarAnimation(
                chevron,
                ModelInspectionAnimatedProperty.RotationInDegrees,
                isExpanded
                    ? spec.CollapsedChevronDegrees
                    : spec.ExpandedChevronDegrees,
                isExpanded
                    ? spec.ExpandedChevronDegrees
                    : spec.CollapsedChevronDegrees,
                spec.DisclosureDuration),
            new ModelInspectionScalarAnimation(
                viewport,
                ModelInspectionAnimatedProperty.Opacity,
                isExpanded
                    ? spec.CollapsedRevealProgress
                    : spec.ExpandedRevealProgress,
                isExpanded
                    ? spec.ExpandedRevealProgress
                    : spec.CollapsedRevealProgress,
                spec.DisclosureDuration),
            new ModelInspectionScalarAnimation(
                viewport,
                ModelInspectionAnimatedProperty.RevealProgress,
                isExpanded
                    ? spec.CollapsedRevealProgress
                    : spec.ExpandedRevealProgress,
                isExpanded
                    ? spec.ExpandedRevealProgress
                    : spec.CollapsedRevealProgress,
                spec.DisclosureDuration)
        ];

        for (int index = 0; index < followingElements.Count; index++)
        {
            UIElement element = followingElements[index] ??
                throw new ArgumentException(
                    "Following elements cannot contain null.",
                    nameof(followingElements));
            double previousTop = previousTopOffsets[index];
            if (!double.IsFinite(previousTop))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(previousTopOffsets),
                    previousTop,
                    "Previous top offsets must be finite.");
            }

            double currentTop = backend.GetTop(element);
            if (!double.IsFinite(currentTop))
            {
                throw new InvalidOperationException(
                    "The post-layout element top must be finite.");
            }

            animations.Add(new ModelInspectionScalarAnimation(
                element,
                ModelInspectionAnimatedProperty.TranslationY,
                previousTop - currentTop,
                0d,
                spec.DisclosureDuration,
                CurrentValueAdjustment: previousTop - currentTop));
        }

        Start(animations, () => completed(key));
    }

    public void StartTerminal(
        UIElement outgoing,
        UIElement incoming,
        ModelInspectionVisualOperationKey key,
        Action<ModelInspectionVisualOperationKey> completed)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(outgoing);
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(completed);

        Start(
            [
                new ModelInspectionScalarAnimation(
                    outgoing,
                    ModelInspectionAnimatedProperty.Opacity,
                    spec.TerminalOutgoingOpacityFrom,
                    spec.TerminalOutgoingOpacityTo,
                    spec.StandardDuration),
                new ModelInspectionScalarAnimation(
                    incoming,
                    ModelInspectionAnimatedProperty.Opacity,
                    spec.TerminalIncomingOpacityFrom,
                    spec.TerminalIncomingOpacityTo,
                    spec.StandardDuration)
            ],
            () => completed(key));
    }

    public void CancelAll()
    {
        ThrowIfDisposed();
        InvalidateOperations();
        backend.CancelAll();
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        InvalidateOperations();
        backend.CancelAll();
        isDisposed = true;
        backend.Dispose();
    }

    private void Start(
        IReadOnlyList<ModelInspectionScalarAnimation> animations,
        Action completed)
    {
        List<ModelInspectionScalarAnimation> retargetableAnimations =
            new(animations.Count);
        Dictionary<UIElement, HashSet<ModelInspectionAnimatedProperty>> slots =
            new(ReferenceEqualityComparer.Instance);
        foreach (ModelInspectionScalarAnimation animation in animations)
        {
            ArgumentNullException.ThrowIfNull(animation.Target);
            if (!Enum.IsDefined(animation.Property))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(animations),
                    animation.Property,
                    "Animation properties must be defined.");
            }

            if (!slots.TryGetValue(animation.Target, out HashSet<
                    ModelInspectionAnimatedProperty>? properties))
            {
                properties = [];
                slots.Add(animation.Target, properties);
            }

            if (!properties.Add(animation.Property))
            {
                throw new ArgumentException(
                    "A batch cannot animate the same target property twice.",
                    nameof(animations));
            }

            bool startsFromCurrentValue = IsTracked(
                animation.Target,
                animation.Property);
            retargetableAnimations.Add(animation with
            {
                StartsFromCurrentValue = startsFromCurrentValue,
                CurrentValueAdjustment = startsFromCurrentValue
                    ? animation.CurrentValueAdjustment
                    : 0d
            });
        }

        long operation = unchecked(++nextOperation);
        long cancellation = cancellationRevision;
        foreach ((UIElement target, HashSet<ModelInspectionAnimatedProperty>
                 properties) in slots)
        {
            if (!targetOperations.TryGetValue(
                    target,
                    out Dictionary<ModelInspectionAnimatedProperty, long>?
                        operations))
            {
                operations = [];
                targetOperations.Add(target, operations);
            }

            foreach (ModelInspectionAnimatedProperty property in properties)
            {
                operations[property] = operation;
            }
        }

        bool completionDelivered = false;
        try
        {
            backend.Start(
                retargetableAnimations,
                spec.EaseOutControlPoint1,
                spec.EaseOutControlPoint2,
                () =>
                {
                    if (completionDelivered)
                    {
                        return;
                    }

                    bool stillOwnsEverySlot = true;
                    foreach ((UIElement target, HashSet<
                             ModelInspectionAnimatedProperty> properties) in
                             slots)
                    {
                        foreach (ModelInspectionAnimatedProperty property in
                                 properties)
                        {
                            if (!RemoveIfOwned(
                                    target,
                                    property,
                                    operation))
                            {
                                stillOwnsEverySlot = false;
                            }
                        }
                    }

                    if (isDisposed ||
                        cancellation != cancellationRevision ||
                        !stillOwnsEverySlot)
                    {
                        return;
                    }

                    completionDelivered = true;
                    completed();
                });
        }
        catch
        {
            foreach ((UIElement target, HashSet<
                     ModelInspectionAnimatedProperty> properties) in slots)
            {
                foreach (ModelInspectionAnimatedProperty property in
                         properties)
                {
                    RemoveIfOwned(target, property, operation);
                }
            }

            throw;
        }
    }

    private bool IsTracked(
        UIElement target,
        ModelInspectionAnimatedProperty property) =>
        targetOperations.TryGetValue(
            target,
            out Dictionary<ModelInspectionAnimatedProperty, long>?
                operations) &&
        operations.ContainsKey(property);

    private bool RemoveIfOwned(
        UIElement target,
        ModelInspectionAnimatedProperty property,
        long operation)
    {
        if (!targetOperations.TryGetValue(
                target,
                out Dictionary<ModelInspectionAnimatedProperty, long>?
                    operations) ||
            !operations.TryGetValue(property, out long current) ||
            current != operation)
        {
            return false;
        }

        operations.Remove(property);
        if (operations.Count == 0)
        {
            targetOperations.Remove(target);
        }

        return true;
    }

    private void InvalidateOperations()
    {
        cancellationRevision = unchecked(cancellationRevision + 1);
        targetOperations.Clear();
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(isDisposed, this);
}

internal sealed class WinUiCompositionAnimationBackend :
    IModelInspectionCompositionAnimationBackend
{
    private readonly List<AnimationSlot> activeSlots = [];
    private readonly List<BatchRegistration> activeBatches = [];
    private long nextBatch;
    private long cancellationRevision;
    private bool isDisposed;

    internal int ActiveBatchCount => activeBatches.Count;

    internal int ActiveSlotCount => activeSlots.Count;

    public double GetTop(UIElement target)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(target);
        UIElement root = target.XamlRoot?.Content as UIElement ??
            throw new InvalidOperationException(
                "A disclosure element must be connected to a XAML root before motion starts.");
        Point origin = target.TransformToVisual(root).TransformPoint(default);
        if (!double.IsFinite(origin.Y))
        {
            throw new InvalidOperationException(
                "The disclosure element produced a non-finite layout position.");
        }

        return origin.Y;
    }

    public void Start(
        IReadOnlyList<ModelInspectionScalarAnimation> animations,
        Vector2 controlPoint1,
        Vector2 controlPoint2,
        Action completed)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(animations);
        ArgumentNullException.ThrowIfNull(completed);
        if (animations.Count == 0)
        {
            throw new ArgumentException(
                "At least one scalar animation is required.",
                nameof(animations));
        }

        ValidateControlPoint(controlPoint1, nameof(controlPoint1));
        ValidateControlPoint(controlPoint2, nameof(controlPoint2));

        List<ModelInspectionCompositionAnimationPlan> plans =
            new(animations.Count);
        foreach (ModelInspectionScalarAnimation animation in animations)
        {
            UIElement target = animation.Target ??
                throw new ArgumentException(
                    "Animation targets cannot be null.",
                    nameof(animations));
            double revealExtent = 0d;
            if (animation.Property ==
                ModelInspectionAnimatedProperty.RevealProgress)
            {
                if (target is not FrameworkElement viewport)
                {
                    throw new ArgumentException(
                        "Reveal progress requires a FrameworkElement target.",
                        nameof(animations));
                }

                revealExtent = viewport.ActualHeight;
            }

            plans.Add(BuildPlan(
                animation,
                revealExtent,
                controlPoint1,
                controlPoint2));
        }

        Visual firstVisual = ElementCompositionPreview.GetElementVisual(
            plans[0].Target);
        Compositor compositor = firstVisual.Compositor;
        CompositionScopedBatch batch = compositor.CreateScopedBatch(
            CompositionBatchTypes.Animation);
        long batchId = unchecked(++nextBatch);
        long cancellation = cancellationRevision;
        TypedEventHandler<object, CompositionBatchCompletedEventArgs> handler =
            (_, _) => CompleteBatch(
                batchId,
                cancellation,
                completed);
        BatchRegistration registration = new(batchId, batch, handler);
        batch.Completed += handler;
        activeBatches.Add(registration);

        bool endAttempted = false;
        try
        {
            foreach (ModelInspectionCompositionAnimationPlan plan in plans)
            {
                StartScalarAnimation(plan, batchId);
            }

            endAttempted = true;
            batch.End();
        }
        catch
        {
            if (!endAttempted)
            {
                try
                {
                    batch.End();
                }
                catch
                {
                    // preserve the original start failure
                }
            }

            DetachBatch(batchId);
            StopBatch(batchId, applyBaseEndpoint: false);
            throw;
        }
    }

    public void CancelAll()
    {
        if (isDisposed)
        {
            return;
        }

        cancellationRevision = unchecked(cancellationRevision + 1);
        foreach (BatchRegistration registration in activeBatches.ToArray())
        {
            DetachBatch(registration.BatchId);
        }

        AnimationSlot[] slots = activeSlots.ToArray();
        activeSlots.Clear();
        foreach (AnimationSlot slot in slots)
        {
            slot.Owner.StopAnimation(slot.PropertyName);
            slot.ApplyBaseEndpoint();
        }
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        CancelAll();
        isDisposed = true;
    }

    internal static ModelInspectionCompositionAnimationPlan BuildPlan(
        ModelInspectionScalarAnimation animation,
        double revealExtent,
        Vector2 controlPoint1,
        Vector2 controlPoint2)
    {
        ArgumentNullException.ThrowIfNull(animation.Target);
        ValidateEndpoint(animation.From, nameof(animation.From));
        ValidateEndpoint(animation.To, nameof(animation.To));
        ValidateEndpoint(
            animation.CurrentValueAdjustment,
            nameof(animation.CurrentValueAdjustment));
        if (animation.Duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(animation.Duration),
                animation.Duration,
                "Animation duration must be positive.");
        }

        ValidateControlPoint(controlPoint1, nameof(controlPoint1));
        ValidateControlPoint(controlPoint2, nameof(controlPoint2));

        ModelInspectionCompositionTarget targetKind;
        string propertyName;
        bool enablesTranslation;
        double from = animation.From;
        double to = animation.To;
        switch (animation.Property)
        {
            case ModelInspectionAnimatedProperty.Opacity:
                ValidateUnitEndpoint(animation.From, nameof(animation.From));
                ValidateUnitEndpoint(animation.To, nameof(animation.To));
                targetKind = ModelInspectionCompositionTarget.Visual;
                propertyName = nameof(Visual.Opacity);
                enablesTranslation = false;
                break;

            case ModelInspectionAnimatedProperty.TranslationY:
                targetKind = ModelInspectionCompositionTarget.Visual;
                propertyName = "Translation.Y";
                enablesTranslation = true;
                break;

            case ModelInspectionAnimatedProperty.RotationInDegrees:
                targetKind = ModelInspectionCompositionTarget.Visual;
                propertyName = nameof(Visual.RotationAngleInDegrees);
                enablesTranslation = false;
                break;

            case ModelInspectionAnimatedProperty.RevealProgress:
                ValidateUnitEndpoint(animation.From, nameof(animation.From));
                ValidateUnitEndpoint(animation.To, nameof(animation.To));
                ValidateEndpoint(revealExtent, nameof(revealExtent));
                if (revealExtent <= 0d)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(revealExtent),
                        revealExtent,
                        "A reveal target must retain a positive render extent.");
                }

                targetKind = ModelInspectionCompositionTarget.InsetClip;
                propertyName = nameof(InsetClip.BottomInset);
                enablesTranslation = false;
                from = revealExtent * (1d - animation.From);
                to = revealExtent * (1d - animation.To);
                ValidateEndpoint(from, nameof(animation.From));
                ValidateEndpoint(to, nameof(animation.To));
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(animation.Property),
                    animation.Property,
                    "Animation property must be defined.");
        }

        return new ModelInspectionCompositionAnimationPlan(
            animation.Target,
            targetKind,
            propertyName,
            from,
            to,
            animation.Duration,
            controlPoint1,
            controlPoint2,
            animation.StartsFromCurrentValue,
            enablesTranslation,
            animation.CurrentValueAdjustment);
    }

    private void StartScalarAnimation(
        ModelInspectionCompositionAnimationPlan plan,
        long batchId)
    {
        UIElement target = plan.Target;
        Visual visual = ElementCompositionPreview.GetElementVisual(plan.Target);
        CompositionObject owner;
        Action applyBaseEndpoint;
        switch (plan.TargetKind)
        {
            case ModelInspectionCompositionTarget.Visual
                when plan.PropertyName == nameof(Visual.Opacity):
                owner = visual;
                applyBaseEndpoint = () =>
                    visual.Opacity = (float)plan.BaseValue;
                break;

            case ModelInspectionCompositionTarget.Visual
                when plan.EnablesTranslation &&
                     plan.PropertyName == "Translation.Y":
                ElementCompositionPreview.SetIsTranslationEnabled(target, true);
                owner = visual;
                applyBaseEndpoint = () =>
                    target.Translation = new Vector3(
                        target.Translation.X,
                        (float)plan.BaseValue,
                        target.Translation.Z);
                break;

            case ModelInspectionCompositionTarget.Visual
                when plan.PropertyName ==
                     nameof(Visual.RotationAngleInDegrees):
                owner = visual;
                visual.CenterPoint = new Vector3(
                    visual.Size.X / 2f,
                    visual.Size.Y / 2f,
                    0f);
                applyBaseEndpoint = () =>
                    visual.RotationAngleInDegrees = (float)plan.BaseValue;
                break;

            case ModelInspectionCompositionTarget.InsetClip
                when plan.PropertyName == nameof(InsetClip.BottomInset):
                InsetClip clip = visual.Clip as InsetClip ??
                    visual.Compositor.CreateInsetClip();
                visual.Clip = clip;
                owner = clip;
                applyBaseEndpoint = () =>
                    clip.BottomInset = (float)plan.BaseValue;
                break;

            default:
                throw new InvalidOperationException(
                    "The composition plan contains an unsupported target mapping.");
        }

        ScalarKeyFrameAnimation scalar = owner.Compositor
            .CreateScalarKeyFrameAnimation();
        scalar.Duration = plan.Duration;
        if (plan.StartingExpression is string startingExpression)
        {
            if (plan.StartingValueAdjustment != 0d)
            {
                scalar.SetScalarParameter(
                    ModelInspectionCompositionAnimationPlan
                        .StartingValueParameterName,
                    (float)plan.StartingValueAdjustment);
            }

            scalar.InsertExpressionKeyFrame(0f, startingExpression);
        }
        else
        {
            scalar.InsertKeyFrame(0f, (float)plan.From);
        }

        scalar.InsertKeyFrame(
            1f,
            (float)plan.To,
            owner.Compositor.CreateCubicBezierEasingFunction(
                plan.ControlPoint1,
                plan.ControlPoint2));
        ReplaceSlot(new AnimationSlot(
            owner,
            plan.PropertyName,
            batchId,
            applyBaseEndpoint));
        owner.StartAnimation(plan.PropertyName, scalar);
    }

    private void CompleteBatch(
        long batchId,
        long cancellation,
        Action completed)
    {
        if (!DetachBatch(batchId))
        {
            return;
        }

        AnimationSlot[] completedSlots = activeSlots
            .Where(slot => slot.BatchId == batchId)
            .ToArray();
        activeSlots.RemoveAll(slot => slot.BatchId == batchId);
        foreach (AnimationSlot slot in completedSlots)
        {
            slot.Owner.StopAnimation(slot.PropertyName);
            slot.ApplyBaseEndpoint();
        }

        if (!isDisposed && cancellation == cancellationRevision)
        {
            completed();
        }
    }

    private void ReplaceSlot(AnimationSlot replacement)
    {
        HashSet<long> supersededBatches = [];
        for (int index = activeSlots.Count - 1; index >= 0; index--)
        {
            AnimationSlot slot = activeSlots[index];
            if (!ReferenceEquals(slot.Owner, replacement.Owner) ||
                slot.PropertyName != replacement.PropertyName)
            {
                continue;
            }

            if (slot.BatchId == replacement.BatchId)
            {
                throw new InvalidOperationException(
                    "A composition batch cannot own the same property twice.");
            }

            supersededBatches.Add(slot.BatchId);
            activeSlots.RemoveAt(index);
        }

        activeSlots.Add(replacement);
        foreach (long batchId in supersededBatches)
        {
            if (!activeSlots.Any(slot => slot.BatchId == batchId))
            {
                DetachBatch(batchId);
            }
        }
    }

    private void StopBatch(long batchId, bool applyBaseEndpoint)
    {
        for (int index = activeSlots.Count - 1; index >= 0; index--)
        {
            AnimationSlot slot = activeSlots[index];
            if (slot.BatchId != batchId)
            {
                continue;
            }

            slot.Owner.StopAnimation(slot.PropertyName);
            if (applyBaseEndpoint)
            {
                slot.ApplyBaseEndpoint();
            }

            activeSlots.RemoveAt(index);
        }
    }

    private bool DetachBatch(long batchId)
    {
        int index = activeBatches.FindIndex(
            registration => registration.BatchId == batchId);
        if (index < 0)
        {
            return false;
        }

        BatchRegistration registration = activeBatches[index];
        activeBatches.RemoveAt(index);
        registration.Batch.Completed -= registration.Handler;
        registration.Batch.Dispose();
        return true;
    }

    private static void ValidateControlPoint(Vector2 value, string parameterName)
    {
        if (!float.IsFinite(value.X) ||
            !float.IsFinite(value.Y) ||
            value.X < 0f ||
            value.X > 1f)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Cubic-bezier control points must be finite and use an X coordinate between zero and one.");
        }
    }

    private static void ValidateEndpoint(double value, string parameterName)
    {
        if (!double.IsFinite(value) || !float.IsFinite((float)value))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Composition scalar endpoints must be finite single-precision values.");
        }
    }

    private static void ValidateUnitEndpoint(
        double value,
        string parameterName)
    {
        ValidateEndpoint(value, parameterName);
        if (value < 0d || value > 1d)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Opacity and reveal progress must be between zero and one.");
        }
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(isDisposed, this);

    private sealed record AnimationSlot(
        CompositionObject Owner,
        string PropertyName,
        long BatchId,
        Action ApplyBaseEndpoint);

    private sealed record BatchRegistration(
        long BatchId,
        CompositionScopedBatch Batch,
        TypedEventHandler<object, CompositionBatchCompletedEventArgs> Handler);
}
