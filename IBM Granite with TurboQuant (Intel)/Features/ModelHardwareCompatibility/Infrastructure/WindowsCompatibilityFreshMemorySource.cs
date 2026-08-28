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
    private static readonly TimeSpan MaximumEvidenceAge = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaximumFutureSkew = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaximumProviderSkew = TimeSpan.FromSeconds(5);

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

        DateTimeOffset now = _timeProvider.GetUtcNow().ToUniversalTime();
        ValidateCurrent(memory.CapturedAtUtc, now);
        ValidateCurrent(storage.CapturedAtUtc, now);
        TimeSpan providerSkew = memory.CapturedAtUtc - storage.CapturedAtUtc;
        if (providerSkew.Duration() > MaximumProviderSkew)
        {
            throw new InvalidOperationException(
                "Fresh resource observations are not temporally consistent.");
        }

        // The oldest accepted observation is the conservative aggregate time.
        // A newer clock reading would conceal the age of one of the values.
        DateTimeOffset observedAtUtc = memory.CapturedAtUtc <= storage.CapturedAtUtc
            ? memory.CapturedAtUtc
            : storage.CapturedAtUtc;

        return CompatibilityFreshResourcesInput.Create(
            CurrentlyAvailableMemory.FromBytes(memory.AvailablePhysicalBytes),
            availableDedicatedDeviceMemoryBytes: null,
            availableStorage,
            observedAtUtc);
    }

    private static void ValidateCurrent(DateTimeOffset capturedAtUtc, DateTimeOffset nowUtc)
    {
        TimeSpan age = nowUtc - capturedAtUtc;
        if (age > MaximumEvidenceAge || age < -MaximumFutureSkew)
        {
            throw new InvalidOperationException("Fresh resource evidence is not current.");
        }
    }
}
#endif
