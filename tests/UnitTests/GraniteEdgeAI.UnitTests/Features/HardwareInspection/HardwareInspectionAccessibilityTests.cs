using GraniteEdgeAI.Features.HardwareInspection.Presentation;
using GraniteEdgeAI.Features.HardwareInspection.ViewModels;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection;

[TestClass]
[TestCategory("HardwareInspectionGate8Acceptance")]
public sealed class HardwareInspectionAccessibilityTests
{
    [TestMethod]
    public async Task StagePacer_UsesExactNormalMotionDurationAndCallerToken()
    {
        RecordingMotionSettings settings = new(animationsEnabled: true);
        TimeSpan? delay = null;
        CancellationToken observedToken = default;
        using CancellationTokenSource cancellation = new();
        HardwareInspectionStagePacer pacer = new(
            settings,
            (duration, token) =>
            {
                delay = duration;
                observedToken = token;
                return Task.CompletedTask;
            });

        await pacer.WaitAsync(cancellation.Token);

        Assert.AreEqual(TimeSpan.FromMilliseconds(500), delay);
        Assert.AreEqual(cancellation.Token, observedToken);
    }

    [TestMethod]
    public async Task StagePacer_ReducedMotionSkipsDelayAndReadsEveryStage()
    {
        RecordingMotionSettings settings = new(animationsEnabled: false);
        int delayCalls = 0;
        HardwareInspectionStagePacer pacer = new(
            settings,
            (_, _) =>
            {
                delayCalls++;
                return Task.CompletedTask;
            });

        await pacer.WaitAsync(CancellationToken.None);
        settings.AnimationsEnabled = true;
        await pacer.WaitAsync(CancellationToken.None);

        Assert.AreEqual(2, settings.Reads);
        Assert.AreEqual(1, delayCalls);
    }

    [TestMethod]
    public async Task StagePacer_SettingsFailureFailsSafeToReducedMotion()
    {
        RecordingMotionSettings settings = new(animationsEnabled: true)
        {
            ReadException = new InvalidOperationException("private settings text"),
        };
        int delayCalls = 0;
        HardwareInspectionStagePacer pacer = new(
            settings,
            (_, _) =>
            {
                delayCalls++;
                return Task.CompletedTask;
            });

        await pacer.WaitAsync(CancellationToken.None);

        Assert.AreEqual(0, delayCalls);
    }

    [TestMethod]
    public async Task StagePacer_NormalMotionPropagatesCancellation()
    {
        RecordingMotionSettings settings = new(animationsEnabled: true);
        HardwareInspectionStagePacer pacer = new(
            settings,
            static (_, token) => Task.FromCanceled(token));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => pacer.WaitAsync(cancellation.Token));
    }

    private sealed class RecordingMotionSettings(bool animationsEnabled)
        : IHardwareInspectionMotionSettings
    {
        internal bool AnimationsEnabled { private get; set; } = animationsEnabled;
        bool IHardwareInspectionMotionSettings.AnimationsEnabled
        {
            get
            {
                Reads++;
                if (ReadException is not null)
                {
                    throw ReadException;
                }

                return AnimationsEnabled;
            }
        }

        internal int Reads { get; private set; }
        internal Exception? ReadException { get; init; }
    }
}
