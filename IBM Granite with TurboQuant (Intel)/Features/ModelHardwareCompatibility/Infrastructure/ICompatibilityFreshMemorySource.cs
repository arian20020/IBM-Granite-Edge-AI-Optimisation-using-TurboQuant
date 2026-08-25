using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;

internal interface ICompatibilityFreshResourcesSource
{
    ValueTask<CompatibilityFreshResourcesInput> CaptureAsync(
        CancellationToken cancellationToken);
}
