using System.Collections.ObjectModel;
using GraniteEdgeAI.HardwareInspection.Foundation.Validation;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Dxgi;

public enum DxgiGraphicsEvidenceState
{
    Available,
    Unavailable,
}

public enum DxgiAdapterKind
{
    Hardware,
    Software,
    Remote,
}

public enum DxgiGraphicsDiagnosticCode
{
    FactoryUnavailable,
    EnumerationFailed,
    InvalidDescription,
    AdapterLimitExceeded,
    NativeApiUnavailable,
}

public sealed class DxgiAdapterEvidence
{
    public DxgiAdapterEvidence(
        string name,
        DxgiAdapterKind kind,
        uint vendorId,
        uint deviceId,
        ulong dedicatedVideoMemoryBytes,
        ulong dedicatedSystemMemoryBytes,
        ulong sharedSystemMemoryBytes,
        int ordinal)
    {
        HardwareText.Validate(name, 256, nameof(name));
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(ordinal, 64);

        Name = name;
        Kind = kind;
        VendorId = vendorId;
        DeviceId = deviceId;
        DedicatedVideoMemoryBytes = dedicatedVideoMemoryBytes;
        DedicatedSystemMemoryBytes = dedicatedSystemMemoryBytes;
        SharedSystemMemoryBytes = sharedSystemMemoryBytes;
        Ordinal = ordinal;
    }

    public string Name { get; }

    public DxgiAdapterKind Kind { get; }

    public uint VendorId { get; }

    public uint DeviceId { get; }

    public ulong DedicatedVideoMemoryBytes { get; }

    public ulong DedicatedSystemMemoryBytes { get; }

    public ulong SharedSystemMemoryBytes { get; }

    public int Ordinal { get; }
}

public sealed class DxgiGraphicsEvidence
{
    private DxgiGraphicsEvidence(
        DxgiGraphicsEvidenceState state,
        IReadOnlyList<DxgiAdapterEvidence> adapters,
        DateTimeOffset capturedAtUtc,
        IReadOnlyList<DxgiGraphicsDiagnosticCode> diagnostics)
    {
        State = state;
        Adapters = adapters;
        CapturedAtUtc = capturedAtUtc;
        Diagnostics = diagnostics;
    }

    public DxgiGraphicsEvidenceState State { get; }

    public IReadOnlyList<DxgiAdapterEvidence> Adapters { get; }

    public DateTimeOffset CapturedAtUtc { get; }

    public IReadOnlyList<DxgiGraphicsDiagnosticCode> Diagnostics { get; }

    public static DxgiGraphicsEvidence Available(
        IEnumerable<DxgiAdapterEvidence> adapters,
        DateTimeOffset capturedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(adapters);
        ValidateCaptureTime(capturedAtUtc);

        List<DxgiAdapterEvidence> copy = new(capacity: 64);
        HashSet<(DxgiAdapterKind Kind, uint VendorId, uint DeviceId, int Ordinal)> identities = [];
        foreach (DxgiAdapterEvidence adapter in adapters)
        {
            if (copy.Count == 64)
            {
                throw new ArgumentException("The DXGI adapter collection exceeds 64 entries.", nameof(adapters));
            }

            if (adapter is null)
            {
                throw new ArgumentException("The DXGI adapter collection contains a null entry.", nameof(adapters));
            }

            if (!identities.Add((adapter.Kind, adapter.VendorId, adapter.DeviceId, adapter.Ordinal)))
            {
                throw new ArgumentException("DXGI adapter provider identities must be unique.", nameof(adapters));
            }

            copy.Add(adapter);
        }

        return new(
            DxgiGraphicsEvidenceState.Available,
            Array.AsReadOnly(copy.ToArray()),
            capturedAtUtc,
            EmptyDiagnostics());
    }

    public static DxgiGraphicsEvidence Unavailable(
        DxgiGraphicsDiagnosticCode diagnostic,
        DateTimeOffset capturedAtUtc)
    {
        if (!Enum.IsDefined(diagnostic))
        {
            throw new ArgumentOutOfRangeException(nameof(diagnostic));
        }

        ValidateCaptureTime(capturedAtUtc);
        return new(
            DxgiGraphicsEvidenceState.Unavailable,
            Array.AsReadOnly(Array.Empty<DxgiAdapterEvidence>()),
            capturedAtUtc,
            Array.AsReadOnly([diagnostic]));
    }

    private static ReadOnlyCollection<DxgiGraphicsDiagnosticCode> EmptyDiagnostics() =>
        Array.AsReadOnly(Array.Empty<DxgiGraphicsDiagnosticCode>());

    private static void ValidateCaptureTime(DateTimeOffset capturedAtUtc)
    {
        if (capturedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Capture time must use the UTC offset.", nameof(capturedAtUtc));
        }
    }
}
