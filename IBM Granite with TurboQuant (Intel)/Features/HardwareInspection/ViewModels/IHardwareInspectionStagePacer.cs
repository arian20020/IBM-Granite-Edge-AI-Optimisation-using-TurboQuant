using GraniteEdgeAI.Features.HardwareInspection.Presentation;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.HardwareInspection.ViewModels;

public interface IHardwareInspectionStagePacer
{
    Task WaitAsync(CancellationToken cancellationToken);
}

internal sealed class HardwareInspectionStagePacer : IHardwareInspectionStagePacer
{
    private static readonly TimeSpan MinimumVisibleDuration =
        TimeSpan.FromMilliseconds(500);
    private readonly IHardwareInspectionMotionSettings _motionSettings;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    internal HardwareInspectionStagePacer()
        : this(
            new UiSettingsHardwareInspectionMotionSettings(),
            static (duration, token) => Task.Delay(duration, token))
    {
    }

    internal HardwareInspectionStagePacer(
        IHardwareInspectionMotionSettings motionSettings,
        Func<TimeSpan, CancellationToken, Task> delay)
    {
        _motionSettings = motionSettings
            ?? throw new ArgumentNullException(nameof(motionSettings));
        _delay = delay ?? throw new ArgumentNullException(nameof(delay));
    }

    public Task WaitAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        try
        {
            return _motionSettings.AnimationsEnabled
                ? _delay(MinimumVisibleDuration, cancellationToken)
                : Task.CompletedTask;
        }
        catch (Exception exception) when (
            !cancellationToken.IsCancellationRequested && IsRecoverable(exception))
        {
            return Task.CompletedTask;
        }
    }

    private static bool IsRecoverable(Exception exception) => exception is not
        (OutOfMemoryException or StackOverflowException or AccessViolationException);
}
