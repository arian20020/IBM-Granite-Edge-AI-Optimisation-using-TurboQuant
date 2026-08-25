#if HARDWARE_INSPECTION_X64
using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Infrastructure;
using GraniteEdgeAI.HardwareInspection.Foundation.Windows;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;

internal sealed class WindowsCompatibilityFreshResourcesSource : ICompatibilityFreshResourcesSource
{
    private readonly IAvailableMemoryProvider _provider;
    private readonly Func<CancellationToken, ValueTask<WindowsStorageEvidence>> _captureStorage;
    private readonly TimeProvider _timeProvider;

    internal WindowsCompatibilityFreshResourcesSource()
        : this(
            new WindowsAvailableMemoryProvider(),
            new WindowsStorageEvidenceProvider().CaptureAsync,
            TimeProvider.System)
    {
    }

    internal WindowsCompatibilityFreshResourcesSource(
        IAvailableMemoryProvider provider,
        Func<CancellationToken, ValueTask<WindowsStorageEvidence>> captureStorage,
        TimeProvider timeProvider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _captureStorage = captureStorage ?? throw new ArgumentNullException(nameof(captureStorage));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async ValueTask<CompatibilityFreshResourcesInput> CaptureAsync(
        CancellationToken cancellationToken)
    {
        AvailableMemorySnapshot memory = await _provider.CaptureAsync(cancellationToken);
        WindowsStorageEvidence storage = await _captureStorage(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (storage.State != WindowsStorageEvidenceState.Available
            || storage.AvailableToCallerBytes is not { } availableStorage)
        {
            throw new InvalidOperationException("Fresh storage evidence is unavailable.");
        }

        return CompatibilityFreshResourcesInput.Create(
            memory.AvailablePhysicalBytes,
            availableDedicatedDeviceMemoryBytes: null,
            availableStorage,
            _timeProvider.GetUtcNow().ToUniversalTime());
    }
}
#endif
