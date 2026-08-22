using GraniteEdgeAI.HardwareInspection.Foundation.Validation;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Dxgi;

internal enum DxgiAdapterApiStatus
{
    Success,
    FactoryUnavailable,
    EnumerationFailed,
    InvalidDescription,
    AdapterLimitExceeded,
    NativeApiUnavailable,
}

internal enum DxgiAdapterApiKind
{
    Hardware,
    Software,
    Remote,
}

internal sealed record DxgiAdapterApiEntry(
    string? Name,
    DxgiAdapterApiKind Kind,
    uint VendorId,
    uint DeviceId,
    ulong DedicatedVideoMemoryBytes,
    ulong DedicatedSystemMemoryBytes,
    ulong SharedSystemMemoryBytes);

internal sealed record DxgiAdapterApiResult(
    DxgiAdapterApiStatus Status,
    IEnumerable<DxgiAdapterApiEntry> Adapters);

internal interface IDxgiAdapterApi
{
    DxgiAdapterApiResult Capture();
}

public sealed class DxgiGraphicsEvidenceProvider
{
    private readonly IDxgiAdapterApi _adapterApi;
    private readonly TimeProvider _timeProvider;

    internal DxgiGraphicsEvidenceProvider(
        IDxgiAdapterApi adapterApi,
        TimeProvider timeProvider)
    {
        _adapterApi = adapterApi ?? throw new ArgumentNullException(nameof(adapterApi));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public ValueTask<DxgiGraphicsEvidence> CaptureAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DxgiAdapterApiResult result = _adapterApi.Capture();
        DateTimeOffset capturedAtUtc = _timeProvider.GetUtcNow().ToUniversalTime();

        if (result.Status != DxgiAdapterApiStatus.Success)
        {
            return Unavailable(MapFailure(result.Status), capturedAtUtc);
        }

        if (result.Adapters is null)
        {
            return Unavailable(DxgiGraphicsDiagnosticCode.EnumerationFailed, capturedAtUtc);
        }

        List<DxgiAdapterEvidence> adapters = new(capacity: 64);
        foreach (DxgiAdapterApiEntry entry in result.Adapters)
        {
            if (adapters.Count == 64)
            {
                return Unavailable(DxgiGraphicsDiagnosticCode.AdapterLimitExceeded, capturedAtUtc);
            }

            if (entry is null || !Enum.IsDefined(entry.Kind))
            {
                return Unavailable(DxgiGraphicsDiagnosticCode.EnumerationFailed, capturedAtUtc);
            }

            if (!HardwareText.IsSafe(entry.Name, 256))
            {
                return Unavailable(DxgiGraphicsDiagnosticCode.InvalidDescription, capturedAtUtc);
            }

            adapters.Add(new DxgiAdapterEvidence(
                entry.Name!,
                MapKind(entry.Kind),
                entry.VendorId,
                entry.DeviceId,
                entry.DedicatedVideoMemoryBytes,
                entry.DedicatedSystemMemoryBytes,
                entry.SharedSystemMemoryBytes,
                adapters.Count));
        }

        return ValueTask.FromResult(DxgiGraphicsEvidence.Available(adapters, capturedAtUtc));
    }

    private static ValueTask<DxgiGraphicsEvidence> Unavailable(
        DxgiGraphicsDiagnosticCode diagnostic,
        DateTimeOffset capturedAtUtc) =>
        ValueTask.FromResult(DxgiGraphicsEvidence.Unavailable(diagnostic, capturedAtUtc));

    private static DxgiGraphicsDiagnosticCode MapFailure(DxgiAdapterApiStatus status) =>
        status switch
        {
            DxgiAdapterApiStatus.FactoryUnavailable => DxgiGraphicsDiagnosticCode.FactoryUnavailable,
            DxgiAdapterApiStatus.EnumerationFailed => DxgiGraphicsDiagnosticCode.EnumerationFailed,
            DxgiAdapterApiStatus.InvalidDescription => DxgiGraphicsDiagnosticCode.InvalidDescription,
            DxgiAdapterApiStatus.AdapterLimitExceeded => DxgiGraphicsDiagnosticCode.AdapterLimitExceeded,
            DxgiAdapterApiStatus.NativeApiUnavailable => DxgiGraphicsDiagnosticCode.NativeApiUnavailable,
            _ => DxgiGraphicsDiagnosticCode.NativeApiUnavailable,
        };

    private static DxgiAdapterKind MapKind(DxgiAdapterApiKind kind) =>
        kind switch
        {
            DxgiAdapterApiKind.Hardware => DxgiAdapterKind.Hardware,
            DxgiAdapterApiKind.Software => DxgiAdapterKind.Software,
            DxgiAdapterApiKind.Remote => DxgiAdapterKind.Remote,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
}
