using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.Numerics;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation;

[TestClass]
[TestCategory("WinUI")]
public sealed class ModelInspectionMotionTests
{
    [TestMethod]
    public void ApprovedSpec_UsesExactDurationsEasingAndEndpoints()
    {
        ModelInspectionMotionSpec spec = ModelInspectionMotionSpec.Approved;

        Assert.AreEqual(TimeSpan.FromMilliseconds(160), spec.FastDuration);
        Assert.AreEqual(TimeSpan.FromMilliseconds(180), spec.StandardDuration);
        Assert.AreEqual(TimeSpan.FromMilliseconds(240), spec.DisclosureDuration);
        Assert.AreEqual(new Vector2(0f, 0f), spec.EaseOutControlPoint1);
        Assert.AreEqual(new Vector2(0.2f, 1f), spec.EaseOutControlPoint2);
        Assert.AreEqual(0d, spec.StatusOpacityFrom);
        Assert.AreEqual(1d, spec.StatusOpacityTo);
        Assert.AreEqual(0d, spec.ActiveDetailOpacityFrom);
        Assert.AreEqual(1d, spec.ActiveDetailOpacityTo);
        Assert.AreEqual(8d, spec.ActiveDetailOffsetYFrom);
        Assert.AreEqual(0d, spec.ActiveDetailOffsetYTo);
        Assert.AreEqual(1d, spec.TerminalOutgoingOpacityFrom);
        Assert.AreEqual(0d, spec.TerminalOutgoingOpacityTo);
        Assert.AreEqual(0d, spec.TerminalIncomingOpacityFrom);
        Assert.AreEqual(1d, spec.TerminalIncomingOpacityTo);
        Assert.AreEqual(0d, spec.CollapsedChevronDegrees);
        Assert.AreEqual(180d, spec.ExpandedChevronDegrees);
        Assert.AreEqual(0d, spec.CollapsedRevealProgress);
        Assert.AreEqual(1d, spec.ExpandedRevealProgress);
    }

    [TestMethod]
    [DataRow("fast", 0)]
    [DataRow("fast", -1)]
    [DataRow("standard", 0)]
    [DataRow("standard", -1)]
    [DataRow("disclosure", 0)]
    [DataRow("disclosure", -1)]
    public void Spec_NonPositiveDurationIsRejected(
        string durationName,
        int milliseconds)
    {
        TimeSpan fast = TimeSpan.FromMilliseconds(160);
        TimeSpan standard = TimeSpan.FromMilliseconds(180);
        TimeSpan disclosure = TimeSpan.FromMilliseconds(240);
        TimeSpan invalid = TimeSpan.FromMilliseconds(milliseconds);
        switch (durationName)
        {
            case "fast":
                fast = invalid;
                break;
            case "standard":
                standard = invalid;
                break;
            case "disclosure":
                disclosure = invalid;
                break;
            default:
                Assert.Fail($"Unknown duration fixture: {durationName}");
                break;
        }

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            CreateSpec(
                fastDuration: fast,
                standardDuration: standard,
                disclosureDuration: disclosure));
    }

    [TestMethod]
    public void Spec_NonFiniteEndpointIsRejectedForEveryEndpoint()
    {
        Func<double, ModelInspectionMotionSpec>[] invalidEndpointFactories =
        [
            value => CreateSpec(statusOpacityFrom: value),
            value => CreateSpec(statusOpacityTo: value),
            value => CreateSpec(activeDetailOpacityFrom: value),
            value => CreateSpec(activeDetailOpacityTo: value),
            value => CreateSpec(activeDetailOffsetYFrom: value),
            value => CreateSpec(activeDetailOffsetYTo: value),
            value => CreateSpec(terminalOutgoingOpacityFrom: value),
            value => CreateSpec(terminalOutgoingOpacityTo: value),
            value => CreateSpec(terminalIncomingOpacityFrom: value),
            value => CreateSpec(terminalIncomingOpacityTo: value),
            value => CreateSpec(collapsedChevronDegrees: value),
            value => CreateSpec(expandedChevronDegrees: value),
            value => CreateSpec(collapsedRevealProgress: value),
            value => CreateSpec(expandedRevealProgress: value)
        ];

        foreach (Func<double, ModelInspectionMotionSpec> create in
            invalidEndpointFactories)
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                create(double.NaN));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                create(double.PositiveInfinity));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                create(double.NegativeInfinity));
        }
    }

    [TestMethod]
    public void Spec_OpacityAndRevealEndpointsOutsideUnitRangeAreRejected()
    {
        Func<double, ModelInspectionMotionSpec>[] unitEndpointFactories =
        [
            value => CreateSpec(statusOpacityFrom: value),
            value => CreateSpec(statusOpacityTo: value),
            value => CreateSpec(activeDetailOpacityFrom: value),
            value => CreateSpec(activeDetailOpacityTo: value),
            value => CreateSpec(terminalOutgoingOpacityFrom: value),
            value => CreateSpec(terminalOutgoingOpacityTo: value),
            value => CreateSpec(terminalIncomingOpacityFrom: value),
            value => CreateSpec(terminalIncomingOpacityTo: value),
            value => CreateSpec(collapsedRevealProgress: value),
            value => CreateSpec(expandedRevealProgress: value)
        ];

        foreach (Func<double, ModelInspectionMotionSpec> create in
                 unitEndpointFactories)
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                create(-0.01d));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                create(1.01d));
        }
    }

    [TestMethod]
    public void Spec_InvalidCubicControlPointIsRejected()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            CreateSpec(easeOutControlPoint1: new Vector2(float.NaN, 0f)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            CreateSpec(easeOutControlPoint2: new Vector2(0.2f, float.PositiveInfinity)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            CreateSpec(easeOutControlPoint1: new Vector2(-0.01f, 0f)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            CreateSpec(easeOutControlPoint2: new Vector2(1.01f, 1f)));
    }

    [UITestMethod]
    public void StageAndActiveDetail_UseApprovedEndpoints()
    {
        ModelInspectionMotionSpec spec = ModelInspectionMotionSpec.Approved;
        RecordingCompositionBackend backend = new();
        using WinUiModelInspectionAnimationDriver driver = new(spec, backend);
        Border status = new();
        TextBlock detail = new();

        driver.StartStageStatus(status, Key(1), _ => { });
        driver.StartActiveDetail(detail, Key(2), _ => { });

        AssertBatch(
            backend.Batches[0],
            spec.EaseOutControlPoint1,
            spec.EaseOutControlPoint2,
            new ExpectedAnimation(
                status,
                ModelInspectionAnimatedProperty.Opacity,
                0d,
                1d,
                TimeSpan.FromMilliseconds(160)));
        AssertBatch(
            backend.Batches[1],
            spec.EaseOutControlPoint1,
            spec.EaseOutControlPoint2,
            new ExpectedAnimation(
                detail,
                ModelInspectionAnimatedProperty.Opacity,
                0d,
                1d,
                TimeSpan.FromMilliseconds(180)),
            new ExpectedAnimation(
                detail,
                ModelInspectionAnimatedProperty.TranslationY,
                8d,
                0d,
                TimeSpan.FromMilliseconds(180)));
    }

    [UITestMethod]
    public void Disclosure_UsesApprovedBidirectionalEndpoints()
    {
        ModelInspectionMotionSpec spec = ModelInspectionMotionSpec.Approved;
        RecordingCompositionBackend backend = new();
        using WinUiModelInspectionAnimationDriver driver = new(spec, backend);
        Border chevron = new();
        Border viewport = new();
        Border following = new();
        backend.TopOffsets[following] = 184d;

        driver.StartDisclosure(
            chevron,
            viewport,
            [following],
            isExpanded: true,
            previousTopOffsets: [160d],
            Key(1),
            _ => { });
        backend.TopOffsets[following] = 160d;
        driver.StartDisclosure(
            chevron,
            viewport,
            [following],
            isExpanded: false,
            previousTopOffsets: [184d],
            Key(2),
            _ => { });

        TimeSpan duration = TimeSpan.FromMilliseconds(240);
        AssertBatch(
            backend.Batches[0],
            spec.EaseOutControlPoint1,
            spec.EaseOutControlPoint2,
            new ExpectedAnimation(
                chevron,
                ModelInspectionAnimatedProperty.RotationInDegrees,
                0d,
                180d,
                duration),
            new ExpectedAnimation(
                viewport,
                ModelInspectionAnimatedProperty.Opacity,
                0d,
                1d,
                duration),
            new ExpectedAnimation(
                viewport,
                ModelInspectionAnimatedProperty.RevealProgress,
                0d,
                1d,
                duration),
            new ExpectedAnimation(
                following,
                ModelInspectionAnimatedProperty.TranslationY,
                -24d,
                0d,
                duration));
        AssertBatch(
            backend.Batches[1],
            spec.EaseOutControlPoint1,
            spec.EaseOutControlPoint2,
            new ExpectedAnimation(
                chevron,
                ModelInspectionAnimatedProperty.RotationInDegrees,
                180d,
                0d,
                duration,
                StartsFromCurrentValue: true),
            new ExpectedAnimation(
                viewport,
                ModelInspectionAnimatedProperty.Opacity,
                1d,
                0d,
                duration,
                StartsFromCurrentValue: true),
            new ExpectedAnimation(
                viewport,
                ModelInspectionAnimatedProperty.RevealProgress,
                1d,
                0d,
                duration,
                StartsFromCurrentValue: true),
            new ExpectedAnimation(
                following,
                ModelInspectionAnimatedProperty.TranslationY,
                24d,
                0d,
                duration,
                StartsFromCurrentValue: true,
                CurrentValueAdjustment: 24d));
    }

    [UITestMethod]
    public void ProductionMapping_UsesExactPropertiesEndpointsAndEasing()
    {
        Border target = new();
        Vector2 controlPoint1 = new(0f, 0f);
        Vector2 controlPoint2 = new(0.2f, 1f);
        TimeSpan duration = TimeSpan.FromMilliseconds(240);

        ModelInspectionCompositionAnimationPlan opacity =
            WinUiCompositionAnimationBackend.BuildPlan(
                new ModelInspectionScalarAnimation(
                    target,
                    ModelInspectionAnimatedProperty.Opacity,
                    0.25d,
                    0.75d,
                    duration),
                revealExtent: 0d,
                controlPoint1,
                controlPoint2);
        ModelInspectionCompositionAnimationPlan translation =
            WinUiCompositionAnimationBackend.BuildPlan(
                new ModelInspectionScalarAnimation(
                    target,
                    ModelInspectionAnimatedProperty.TranslationY,
                    8d,
                    0d,
                    duration),
                revealExtent: 0d,
                controlPoint1,
                controlPoint2);
        ModelInspectionCompositionAnimationPlan translationRetarget =
            WinUiCompositionAnimationBackend.BuildPlan(
                new ModelInspectionScalarAnimation(
                    target,
                    ModelInspectionAnimatedProperty.TranslationY,
                    24d,
                    0d,
                    duration,
                    StartsFromCurrentValue: true,
                    CurrentValueAdjustment: 24d),
                revealExtent: 0d,
                controlPoint1,
                controlPoint2);
        ModelInspectionCompositionAnimationPlan rotation =
            WinUiCompositionAnimationBackend.BuildPlan(
                new ModelInspectionScalarAnimation(
                    target,
                    ModelInspectionAnimatedProperty.RotationInDegrees,
                    0d,
                    180d,
                    duration,
                    StartsFromCurrentValue: true),
                revealExtent: 0d,
                controlPoint1,
                controlPoint2);
        ModelInspectionCompositionAnimationPlan revealExpand =
            WinUiCompositionAnimationBackend.BuildPlan(
                new ModelInspectionScalarAnimation(
                    target,
                    ModelInspectionAnimatedProperty.RevealProgress,
                    0d,
                    1d,
                    duration),
                revealExtent: 120d,
                controlPoint1,
                controlPoint2);
        ModelInspectionCompositionAnimationPlan revealCollapse =
            WinUiCompositionAnimationBackend.BuildPlan(
                new ModelInspectionScalarAnimation(
                    target,
                    ModelInspectionAnimatedProperty.RevealProgress,
                    1d,
                    0d,
                    duration,
                    StartsFromCurrentValue: true),
                revealExtent: 120d,
                controlPoint1,
                controlPoint2);

        AssertPlan(
            opacity,
            ModelInspectionCompositionTarget.Visual,
            "Opacity",
            0.25d,
            0.75d,
            duration,
            controlPoint1,
            controlPoint2,
            startsFromCurrentValue: false,
            enablesTranslation: false);
        AssertPlan(
            translation,
            ModelInspectionCompositionTarget.Visual,
            "Translation.Y",
            8d,
            0d,
            duration,
            controlPoint1,
            controlPoint2,
            startsFromCurrentValue: false,
            enablesTranslation: true);
        AssertPlan(
            translationRetarget,
            ModelInspectionCompositionTarget.Visual,
            "Translation.Y",
            24d,
            0d,
            duration,
            controlPoint1,
            controlPoint2,
            startsFromCurrentValue: true,
            enablesTranslation: true,
            expectedStartingValueAdjustment: 24d,
            expectedStartingExpression:
                "this.StartingValue + startingValueAdjustment");
        AssertPlan(
            rotation,
            ModelInspectionCompositionTarget.Visual,
            "RotationAngleInDegrees",
            0d,
            180d,
            duration,
            controlPoint1,
            controlPoint2,
            startsFromCurrentValue: true,
            enablesTranslation: false,
            expectedStartingExpression: "this.StartingValue");
        AssertPlan(
            revealExpand,
            ModelInspectionCompositionTarget.InsetClip,
            "BottomInset",
            120d,
            0d,
            duration,
            controlPoint1,
            controlPoint2,
            startsFromCurrentValue: false,
            enablesTranslation: false);
        AssertPlan(
            revealCollapse,
            ModelInspectionCompositionTarget.InsetClip,
            "BottomInset",
            0d,
            120d,
            duration,
            controlPoint1,
            controlPoint2,
            startsFromCurrentValue: true,
            enablesTranslation: false,
            expectedStartingExpression: "this.StartingValue");
    }

    [UITestMethod]
    public void ProductionMapping_RejectsAZeroRevealExtent()
    {
        Border target = new();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            WinUiCompositionAnimationBackend.BuildPlan(
                new ModelInspectionScalarAnimation(
                    target,
                    ModelInspectionAnimatedProperty.RevealProgress,
                    0d,
                    1d,
                    TimeSpan.FromMilliseconds(240)),
                revealExtent: 0d,
                new Vector2(0f, 0f),
                new Vector2(0.2f, 1f)));
    }

    [UITestMethod]
    public void ProductionBackend_RetargetAndCancelReleaseBatchHandlers()
    {
        using WinUiCompositionAnimationBackend backend = new();
        Border target = new();
        Vector2 controlPoint1 = new(0f, 0f);
        Vector2 controlPoint2 = new(0.2f, 1f);
        int firstCompletionCount = 0;
        int secondCompletionCount = 0;

        backend.Start(
            [
                new ModelInspectionScalarAnimation(
                    target,
                    ModelInspectionAnimatedProperty.Opacity,
                    0d,
                    1d,
                    TimeSpan.FromSeconds(10))
            ],
            controlPoint1,
            controlPoint2,
            () => firstCompletionCount++);

        Assert.AreEqual(1, backend.ActiveBatchCount);
        Assert.AreEqual(1, backend.ActiveSlotCount);

        backend.Start(
            [
                new ModelInspectionScalarAnimation(
                    target,
                    ModelInspectionAnimatedProperty.Opacity,
                    1d,
                    0d,
                    TimeSpan.FromSeconds(10),
                    StartsFromCurrentValue: true)
            ],
            controlPoint1,
            controlPoint2,
            () => secondCompletionCount++);

        Assert.AreEqual(
            1,
            backend.ActiveBatchCount,
            "The superseded batch registration must be detached immediately.");
        Assert.AreEqual(1, backend.ActiveSlotCount);

        backend.CancelAll();

        Assert.AreEqual(0, backend.ActiveBatchCount);
        Assert.AreEqual(0, backend.ActiveSlotCount);
        Assert.AreEqual(0, firstCompletionCount);
        Assert.AreEqual(0, secondCompletionCount);
        Assert.AreEqual(
            0f,
            ElementCompositionPreview.GetElementVisual(target).Opacity,
            0.001f,
            "Cancellation must retain the selected direct endpoint.");
    }

    [UITestMethod]
    public void Terminal_UsesApprovedCrossfadeEndpoints()
    {
        ModelInspectionMotionSpec spec = ModelInspectionMotionSpec.Approved;
        RecordingCompositionBackend backend = new();
        using WinUiModelInspectionAnimationDriver driver = new(spec, backend);
        Border outgoing = new();
        Border incoming = new();

        driver.StartTerminal(outgoing, incoming, Key(1), _ => { });

        TimeSpan duration = TimeSpan.FromMilliseconds(180);
        AssertBatch(
            backend.Batches[0],
            spec.EaseOutControlPoint1,
            spec.EaseOutControlPoint2,
            new ExpectedAnimation(
                outgoing,
                ModelInspectionAnimatedProperty.Opacity,
                1d,
                0d,
                duration),
            new ExpectedAnimation(
                incoming,
                ModelInspectionAnimatedProperty.Opacity,
                0d,
                1d,
                duration));
    }

    [UITestMethod]
    public void StageActiveThenCompleted_OlderCompletionCannotRestoreActive()
    {
        RecordingCompositionBackend backend = new();
        using WinUiModelInspectionAnimationDriver driver = new(
            ModelInspectionMotionSpec.Approved,
            backend);
        Border target = new();
        List<ModelInspectionVisualOperationKey> completed = [];
        ModelInspectionVisualOperationKey active = Key(1);
        ModelInspectionVisualOperationKey passed = Key(2);

        driver.StartStageStatus(target, active, completed.Add);
        driver.StartStageStatus(target, passed, completed.Add);
        backend.Complete(0);
        backend.Complete(1);

        CollectionAssert.AreEqual(
            new[] { passed },
            completed);
    }

    [UITestMethod]
    public void Retarget_UsesCurrentValueOnlyForAnOverlappingProperty()
    {
        RecordingCompositionBackend backend = new();
        using WinUiModelInspectionAnimationDriver driver = new(
            ModelInspectionMotionSpec.Approved,
            backend);
        Border target = new();

        driver.StartStageStatus(target, Key(1), _ => { });
        driver.StartActiveDetail(target, Key(2), _ => { });

        Assert.IsTrue(backend.Batches[1].Animations[0]
            .StartsFromCurrentValue);
        Assert.IsFalse(backend.Batches[1].Animations[1]
            .StartsFromCurrentValue);
    }

    [UITestMethod]
    public void TerminalRetry_OlderCrossfadeCannotHideNewAttempt()
    {
        RecordingCompositionBackend backend = new();
        using WinUiModelInspectionAnimationDriver driver = new(
            ModelInspectionMotionSpec.Approved,
            backend);
        Border outgoing = new();
        Border incoming = new();
        List<ModelInspectionVisualOperationKey> completed = [];
        ModelInspectionVisualOperationKey firstAttempt = Key(1, generation: 1);
        ModelInspectionVisualOperationKey retry = Key(1, generation: 2);

        driver.StartTerminal(outgoing, incoming, firstAttempt, completed.Add);
        driver.StartTerminal(outgoing, incoming, retry, completed.Add);
        backend.Complete(0);
        backend.Complete(1);

        CollectionAssert.AreEqual(
            new[] { retry },
            completed);
    }

    [UITestMethod]
    public void Disclosure_RapidReverseRejectsOlderCompletion()
    {
        RecordingCompositionBackend backend = new();
        using WinUiModelInspectionAnimationDriver driver = new(
            ModelInspectionMotionSpec.Approved,
            backend);
        Border chevron = new();
        Border viewport = new();
        Border following = new();
        backend.TopOffsets[following] = 200d;
        List<ModelInspectionVisualOperationKey> completed = [];
        ModelInspectionVisualOperationKey expand = Key(1);
        ModelInspectionVisualOperationKey collapse = Key(2);

        driver.StartDisclosure(
            chevron,
            viewport,
            [following],
            true,
            [160d],
            expand,
            completed.Add);
        backend.TopOffsets[following] = 160d;
        driver.StartDisclosure(
            chevron,
            viewport,
            [following],
            false,
            [200d],
            collapse,
            completed.Add);

        Assert.IsTrue(backend.Batches[1].Animations.All(
            animation => animation.StartsFromCurrentValue));
        Assert.AreEqual(
            40d,
            backend.Batches[1].Animations[^1].CurrentValueAdjustment);

        backend.Complete(0);
        backend.Complete(1);

        CollectionAssert.AreEqual(
            new[] { collapse },
            completed);
    }

    [UITestMethod]
    public void CompletionAndStaleCompletion_ReleaseOnlyOwnedTargets()
    {
        RecordingCompositionBackend backend = new();
        using WinUiModelInspectionAnimationDriver driver = new(
            ModelInspectionMotionSpec.Approved,
            backend);
        Border outgoing = new();
        Border incoming = new();

        driver.StartTerminal(outgoing, incoming, Key(1), _ => { });
        driver.StartStageStatus(outgoing, Key(2), _ => { });

        Assert.AreEqual(2, driver.TrackedTargetCount);

        backend.Complete(0);

        Assert.AreEqual(1, driver.TrackedTargetCount);

        backend.Complete(1);

        Assert.AreEqual(0, driver.TrackedTargetCount);
    }

    [UITestMethod]
    public void BackendDuplicateCompletion_IsDeliveredOnce()
    {
        RecordingCompositionBackend backend = new();
        using WinUiModelInspectionAnimationDriver driver = new(
            ModelInspectionMotionSpec.Approved,
            backend);
        int completionCount = 0;
        driver.StartStageStatus(
            new Border(),
            Key(1),
            _ => completionCount++);

        backend.Complete(0);
        backend.Complete(0);

        Assert.AreEqual(1, completionCount);
        Assert.AreEqual(0, driver.TrackedTargetCount);
    }

    [UITestMethod]
    public void CancelAll_SuppressesPendingCompletionAndCancelsBackend()
    {
        RecordingCompositionBackend backend = new();
        using WinUiModelInspectionAnimationDriver driver = new(
            ModelInspectionMotionSpec.Approved,
            backend);
        bool completed = false;
        driver.StartStageStatus(new Border(), Key(1), _ => completed = true);

        driver.CancelAll();
        backend.Complete(0);

        Assert.IsFalse(completed);
        Assert.AreEqual(1, backend.CancelAllCount);
        Assert.AreEqual(0, driver.TrackedTargetCount);
    }

    [UITestMethod]
    public void BackendStartFailure_InvalidatesPendingCompletion()
    {
        RecordingCompositionBackend backend = new()
        {
            ThrowAfterRecordingStart = true
        };
        using WinUiModelInspectionAnimationDriver driver = new(
            ModelInspectionMotionSpec.Approved,
            backend);
        bool completed = false;

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            driver.StartStageStatus(
                new Border(),
                Key(1),
                _ => completed = true));
        backend.Complete(0);

        Assert.IsFalse(completed);
        Assert.AreEqual(0, driver.TrackedTargetCount);
    }

    [UITestMethod]
    public void Disclosure_InvalidOffsetsAreRejectedBeforeBackendStarts()
    {
        RecordingCompositionBackend backend = new();
        using WinUiModelInspectionAnimationDriver driver = new(
            ModelInspectionMotionSpec.Approved,
            backend);
        Border chevron = new();
        Border viewport = new();
        Border following = new();

        Assert.ThrowsExactly<ArgumentException>(() =>
            driver.StartDisclosure(
                chevron,
                viewport,
                [following],
                true,
                [],
                Key(1),
                _ => { }));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            driver.StartDisclosure(
                chevron,
                viewport,
                [following],
                true,
                [double.NaN],
                Key(2),
                _ => { }));
        Assert.HasCount(0, backend.Batches);
    }

    [UITestMethod]
    public void Driver_NullDependenciesAndDisposedUseAreRejected()
    {
        RecordingCompositionBackend backend = new();
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new WinUiModelInspectionAnimationDriver(null!, backend));
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new WinUiModelInspectionAnimationDriver(
                ModelInspectionMotionSpec.Approved,
                null!));

        WinUiModelInspectionAnimationDriver driver = new(
            ModelInspectionMotionSpec.Approved,
            backend);
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            driver.StartStageStatus(null!, Key(1), _ => { }));
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            driver.StartStageStatus(new Border(), Key(1), null!));

        driver.Dispose();

        Assert.IsTrue(backend.IsDisposed);
        Assert.ThrowsExactly<ObjectDisposedException>(() =>
            driver.StartStageStatus(new Border(), Key(2), _ => { }));
    }

    [UITestMethod]
    public void Driver_DisposeCancelsAndSuppressesPendingCompletion()
    {
        RecordingCompositionBackend backend = new();
        WinUiModelInspectionAnimationDriver driver = new(
            ModelInspectionMotionSpec.Approved,
            backend);
        int completionCount = 0;
        driver.StartStageStatus(
            new Border(),
            Key(1),
            _ => completionCount++);

        driver.Dispose();
        backend.Complete(0);
        driver.Dispose();

        Assert.AreEqual(0, completionCount);
        Assert.AreEqual(1, backend.CancelAllCount);
        Assert.IsTrue(backend.IsDisposed);
        Assert.AreEqual(0, driver.TrackedTargetCount);
    }

    [TestMethod]
    public void MotionSettings_ForwardsPolicyChangesAndReleasesWatcher()
    {
        RecordingAnimationsEnabledSource source = new(false);
        UiSettingsModelInspectionMotionSettings settings = new(source);
        List<object?> senders = [];
        settings.AnimationsEnabledChanged += (sender, _) => senders.Add(sender);

        Assert.IsFalse(settings.AnimationsEnabled);
        source.SetAnimationsEnabled(true);

        Assert.IsTrue(settings.AnimationsEnabled);
        CollectionAssert.AreEqual(new object?[] { settings }, senders);

        settings.Dispose();
        source.SetAnimationsEnabled(false);

        Assert.IsTrue(source.IsDisposed);
        Assert.HasCount(1, senders);
    }

    [TestMethod]
    public void FooterStatusChangedEventArgs_RejectsUndefinedStatus()
    {
        InspectionFooterStatusChangedEventArgs valid = new(
            InspectionFooterStatus.Complete);

        Assert.AreEqual(InspectionFooterStatus.Complete, valid.Status);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new InspectionFooterStatusChangedEventArgs(
                (InspectionFooterStatus)int.MaxValue));
    }

    private static ModelInspectionVisualOperationKey Key(
        long interactionRevision,
        long generation = 1) =>
        new(
            new ModelInspectionRenderKey(generation, interactionRevision),
            interactionRevision);

    private static ModelInspectionMotionSpec CreateSpec(
        TimeSpan? fastDuration = null,
        TimeSpan? standardDuration = null,
        TimeSpan? disclosureDuration = null,
        Vector2? easeOutControlPoint1 = null,
        Vector2? easeOutControlPoint2 = null,
        double statusOpacityFrom = 0d,
        double statusOpacityTo = 1d,
        double activeDetailOpacityFrom = 0d,
        double activeDetailOpacityTo = 1d,
        double activeDetailOffsetYFrom = 8d,
        double activeDetailOffsetYTo = 0d,
        double terminalOutgoingOpacityFrom = 1d,
        double terminalOutgoingOpacityTo = 0d,
        double terminalIncomingOpacityFrom = 0d,
        double terminalIncomingOpacityTo = 1d,
        double collapsedChevronDegrees = 0d,
        double expandedChevronDegrees = 180d,
        double collapsedRevealProgress = 0d,
        double expandedRevealProgress = 1d) =>
        new(
            fastDuration ?? TimeSpan.FromMilliseconds(160),
            standardDuration ?? TimeSpan.FromMilliseconds(180),
            disclosureDuration ?? TimeSpan.FromMilliseconds(240),
            easeOutControlPoint1 ?? new Vector2(0f, 0f),
            easeOutControlPoint2 ?? new Vector2(0.2f, 1f),
            statusOpacityFrom,
            statusOpacityTo,
            activeDetailOpacityFrom,
            activeDetailOpacityTo,
            activeDetailOffsetYFrom,
            activeDetailOffsetYTo,
            terminalOutgoingOpacityFrom,
            terminalOutgoingOpacityTo,
            terminalIncomingOpacityFrom,
            terminalIncomingOpacityTo,
            collapsedChevronDegrees,
            expandedChevronDegrees,
            collapsedRevealProgress,
            expandedRevealProgress);

    private static void AssertBatch(
        RecordedBatch actual,
        Vector2 expectedControlPoint1,
        Vector2 expectedControlPoint2,
        params ExpectedAnimation[] expectedAnimations)
    {
        Assert.AreEqual(expectedControlPoint1, actual.ControlPoint1);
        Assert.AreEqual(expectedControlPoint2, actual.ControlPoint2);
        Assert.AreEqual(expectedAnimations.Length, actual.Animations.Count);
        for (int index = 0; index < expectedAnimations.Length; index++)
        {
            ExpectedAnimation expected = expectedAnimations[index];
            ModelInspectionScalarAnimation observed = actual.Animations[index];
            Assert.AreSame(expected.Target, observed.Target);
            Assert.AreEqual(expected.Property, observed.Property);
            Assert.AreEqual(expected.From, observed.From);
            Assert.AreEqual(expected.To, observed.To);
            Assert.AreEqual(expected.Duration, observed.Duration);
            Assert.AreEqual(
                expected.StartsFromCurrentValue,
                observed.StartsFromCurrentValue);
            Assert.AreEqual(
                expected.CurrentValueAdjustment,
                observed.CurrentValueAdjustment);
        }
    }

    private static void AssertPlan(
        ModelInspectionCompositionAnimationPlan actual,
        ModelInspectionCompositionTarget expectedTarget,
        string expectedPropertyName,
        double expectedFrom,
        double expectedTo,
        TimeSpan expectedDuration,
        Vector2 expectedControlPoint1,
        Vector2 expectedControlPoint2,
        bool startsFromCurrentValue,
        bool enablesTranslation,
        double expectedStartingValueAdjustment = 0d,
        string? expectedStartingExpression = null)
    {
        Assert.AreEqual(expectedTarget, actual.TargetKind);
        Assert.AreEqual(expectedPropertyName, actual.PropertyName);
        Assert.AreEqual(expectedFrom, actual.From);
        Assert.AreEqual(expectedTo, actual.To);
        Assert.AreEqual(expectedTo, actual.BaseValue);
        Assert.AreEqual(expectedDuration, actual.Duration);
        Assert.AreEqual(expectedControlPoint1, actual.ControlPoint1);
        Assert.AreEqual(expectedControlPoint2, actual.ControlPoint2);
        Assert.AreEqual(
            startsFromCurrentValue,
            actual.StartsFromCurrentValue);
        Assert.AreEqual(enablesTranslation, actual.EnablesTranslation);
        Assert.AreEqual(
            expectedStartingValueAdjustment,
            actual.StartingValueAdjustment);
        Assert.AreEqual(
            expectedStartingExpression,
            actual.StartingExpression);
    }

    private sealed record ExpectedAnimation(
        UIElement Target,
        ModelInspectionAnimatedProperty Property,
        double From,
        double To,
        TimeSpan Duration,
        bool StartsFromCurrentValue = false,
        double CurrentValueAdjustment = 0d);

    private sealed record RecordedBatch(
        IReadOnlyList<ModelInspectionScalarAnimation> Animations,
        Vector2 ControlPoint1,
        Vector2 ControlPoint2,
        Action Completed);

    private sealed class RecordingCompositionBackend :
        IModelInspectionCompositionAnimationBackend
    {
        internal Dictionary<UIElement, double> TopOffsets { get; } = [];

        internal List<RecordedBatch> Batches { get; } = [];

        internal int CancelAllCount { get; private set; }

        internal bool IsDisposed { get; private set; }

        internal bool ThrowAfterRecordingStart { get; init; }

        public double GetTop(UIElement target) => TopOffsets[target];

        public void Start(
            IReadOnlyList<ModelInspectionScalarAnimation> animations,
            Vector2 controlPoint1,
            Vector2 controlPoint2,
            Action completed)
        {
            Batches.Add(new RecordedBatch(
                animations.ToArray(),
                controlPoint1,
                controlPoint2,
                completed));
            if (ThrowAfterRecordingStart)
            {
                throw new InvalidOperationException(
                    "The recording backend rejected the animation batch.");
            }
        }

        public void CancelAll() => CancelAllCount++;

        public void Dispose() => IsDisposed = true;

        internal void Complete(int index) => Batches[index].Completed();
    }

    private sealed class RecordingAnimationsEnabledSource :
        IModelInspectionAnimationsEnabledSource
    {
        internal RecordingAnimationsEnabledSource(bool animationsEnabled)
        {
            AnimationsEnabled = animationsEnabled;
        }

        public bool AnimationsEnabled { get; private set; }

        public event EventHandler? AnimationsEnabledChanged;

        internal bool IsDisposed { get; private set; }

        internal void SetAnimationsEnabled(bool animationsEnabled)
        {
            AnimationsEnabled = animationsEnabled;
            AnimationsEnabledChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose() => IsDisposed = true;
    }
}
