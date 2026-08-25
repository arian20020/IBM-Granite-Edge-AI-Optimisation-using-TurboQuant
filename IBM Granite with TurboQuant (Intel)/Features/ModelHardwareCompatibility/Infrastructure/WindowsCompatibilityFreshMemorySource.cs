#if HARDWARE_INSPECTION_X64
using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Infrastructure;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;

internal sealed class WindowsCompatibilityFreshMemorySource : ICompatibilityFreshMemorySource
{
    private readonly IAvailableMemoryProvider _provider;

    internal WindowsCompatibilityFreshMemorySource()
        : this(new WindowsAvailableMemoryProvider())
    {
    }

    internal WindowsCompatibilityFreshMemorySource(IAvailableMemoryProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public ValueTask<AvailableMemorySnapshot> CaptureAsync(CancellationToken cancellationToken) =>
        _provider.CaptureAsync(cancellationToken);
}
#endif
