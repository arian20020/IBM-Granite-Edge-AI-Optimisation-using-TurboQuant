using GraniteEdgeAI.Features.HardwareInspection.Application;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;

internal interface ICompatibilityFreshMemorySource
{
    ValueTask<AvailableMemorySnapshot> CaptureAsync(CancellationToken cancellationToken);
}
