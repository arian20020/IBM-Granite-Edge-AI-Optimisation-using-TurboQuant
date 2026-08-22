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

    public Task WaitAsync(CancellationToken cancellationToken) =>
        Task.Delay(MinimumVisibleDuration, cancellationToken);
}
