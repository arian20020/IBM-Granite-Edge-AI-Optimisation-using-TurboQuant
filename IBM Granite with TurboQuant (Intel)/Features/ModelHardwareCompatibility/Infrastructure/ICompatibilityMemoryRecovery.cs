using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;

/// <summary>
/// The complete memory-recovery authority exposed to compatibility UI code.
/// It intentionally cannot enumerate, inspect, or terminate another process.
/// </summary>
internal interface ICompatibilityMemoryRecovery
{
    Task ReleaseApplicationMemoryAsync(CancellationToken cancellationToken);

    Task OpenTaskManagerAsync(CancellationToken cancellationToken);
}
