using System.Collections.ObjectModel;
using GraniteEdgeAI.HardwareInspection.Foundation.Validation;

namespace GraniteEdgeAI.HardwareInspection.Foundation.LlamaCpp;

public enum LlamaCppCapabilityEvidenceState
{
    Available,
    Unavailable,
}

public enum LlamaCppBackend
{
    Cpu,
}

public enum LlamaCppCapabilityDiagnosticCode
{
    ToolIdentityMismatch,
    CommandContractMismatch,
    IdentityStartFailed,
    IdentityTimedOut,
    IdentityOutputLimitExceeded,
    IdentityCleanupFailed,
    IdentityCancelledUnexpectedly,
    IdentityNonZeroExit,
    IdentityOutputInvalid,
    IdentityMismatch,
    CapabilityStartFailed,
    CapabilityTimedOut,
    CapabilityOutputLimitExceeded,
    CapabilityCleanupFailed,
    CapabilityCancelledUnexpectedly,
    CapabilityProcessFailed,
    NativeCapabilityUnavailable,
    CapabilityOutputInvalid,
}

public sealed class LlamaCppRuntimeIdentity
{
    private LlamaCppRuntimeIdentity(
        string managedPackage,
        string managedVersion,
        string backendPackage,
        string backendVersion,
        string llamaSharpCommit,
        string mappedLlamaCppCommit,
        string runtimeIdentifier)
    {
        ManagedPackage = managedPackage;
        ManagedVersion = managedVersion;
        BackendPackage = backendPackage;
        BackendVersion = backendVersion;
        LlamaSharpCommit = llamaSharpCommit;
        MappedLlamaCppCommit = mappedLlamaCppCommit;
        RuntimeIdentifier = runtimeIdentifier;
    }

    public static LlamaCppRuntimeIdentity PinnedCpu { get; } = new(
        "LLamaSharp",
        "0.27.0",
        "LLamaSharp.Backend.Cpu",
        "0.27.0",
        "7cbbc45e421d55794d5050d126e0b96511007007",
        "3f7c29d318e317b63f54c558bc69803963d7d88c",
        "win-x64");

    public string ManagedPackage { get; }

    public string ManagedVersion { get; }

    public string BackendPackage { get; }

    public string BackendVersion { get; }

    public string LlamaSharpCommit { get; }

    public string MappedLlamaCppCommit { get; }

    public string RuntimeIdentifier { get; }
}

public sealed record LlamaCppVisibleDevice
{
    public LlamaCppVisibleDevice(int ordinal, string bufferType)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(ordinal, 16);
        HardwareText.Validate(bufferType, 128, nameof(bufferType));

        Ordinal = ordinal;
        BufferType = bufferType;
    }

    public int Ordinal { get; }

    public string BufferType { get; }
}

public sealed class LlamaCppCapabilityEvidence
{
    private LlamaCppCapabilityEvidence(
        LlamaCppCapabilityEvidenceState state,
        LlamaCppRuntimeIdentity? runtimeIdentity,
        IReadOnlyList<LlamaCppBackend> backends,
        IReadOnlyList<LlamaCppVisibleDevice> visibleDevices,
        DateTimeOffset capturedAtUtc,
        LlamaCppCapabilityDiagnosticCode? diagnostic)
    {
        State = state;
        RuntimeIdentity = runtimeIdentity;
        Backends = backends;
        VisibleDevices = visibleDevices;
        CapturedAtUtc = capturedAtUtc;
        Diagnostic = diagnostic;
    }

    public LlamaCppCapabilityEvidenceState State { get; }

    public LlamaCppRuntimeIdentity? RuntimeIdentity { get; }

    public IReadOnlyList<LlamaCppBackend> Backends { get; }

    public IReadOnlyList<LlamaCppVisibleDevice> VisibleDevices { get; }

    public DateTimeOffset CapturedAtUtc { get; }

    public LlamaCppCapabilityDiagnosticCode? Diagnostic { get; }

    public static LlamaCppCapabilityEvidence Available(
        LlamaCppRuntimeIdentity runtimeIdentity,
        DateTimeOffset capturedAtUtc,
        IEnumerable<LlamaCppBackend> backends,
        IEnumerable<LlamaCppVisibleDevice> visibleDevices)
    {
        ArgumentNullException.ThrowIfNull(runtimeIdentity);
        ArgumentNullException.ThrowIfNull(backends);
        ArgumentNullException.ThrowIfNull(visibleDevices);
        ValidateCaptureTime(capturedAtUtc);

        if (!ReferenceEquals(runtimeIdentity, LlamaCppRuntimeIdentity.PinnedCpu))
        {
            throw new ArgumentException("The runtime identity is not the pinned CPU identity.", nameof(runtimeIdentity));
        }

        List<LlamaCppBackend> backendCopy = new(capacity: 1);
        foreach (LlamaCppBackend backend in backends)
        {
            if (!Enum.IsDefined(backend))
            {
                throw new ArgumentOutOfRangeException(nameof(backends));
            }

            if (backendCopy.Count == 8)
            {
                throw new ArgumentException("The backend collection exceeds eight entries.", nameof(backends));
            }

            backendCopy.Add(backend);
        }

        if (backendCopy.Count != 1 || backendCopy[0] != LlamaCppBackend.Cpu)
        {
            throw new ArgumentException("Available evidence requires exactly the CPU backend.", nameof(backends));
        }

        List<LlamaCppVisibleDevice> deviceCopy = new(capacity: 16);
        foreach (LlamaCppVisibleDevice device in visibleDevices)
        {
            if (deviceCopy.Count == 16)
            {
                throw new ArgumentException("The visible-device collection exceeds 16 entries.", nameof(visibleDevices));
            }

            if (device is null || device.Ordinal != deviceCopy.Count)
            {
                throw new ArgumentException("Visible-device ordinals must be contiguous from zero.", nameof(visibleDevices));
            }

            deviceCopy.Add(device);
        }

        if (deviceCopy.Count == 0)
        {
            throw new ArgumentException("Available evidence requires at least one visible device.", nameof(visibleDevices));
        }

        return new(
            LlamaCppCapabilityEvidenceState.Available,
            runtimeIdentity,
            Array.AsReadOnly(backendCopy.ToArray()),
            Array.AsReadOnly(deviceCopy.ToArray()),
            capturedAtUtc,
            diagnostic: null);
    }

    public static LlamaCppCapabilityEvidence Unavailable(
        DateTimeOffset capturedAtUtc,
        LlamaCppCapabilityDiagnosticCode diagnostic)
    {
        ValidateCaptureTime(capturedAtUtc);
        if (!Enum.IsDefined(diagnostic))
        {
            throw new ArgumentOutOfRangeException(nameof(diagnostic));
        }

        return new(
            LlamaCppCapabilityEvidenceState.Unavailable,
            runtimeIdentity: null,
            EmptyBackends(),
            EmptyDevices(),
            capturedAtUtc,
            diagnostic);
    }

    private static ReadOnlyCollection<LlamaCppBackend> EmptyBackends() =>
        Array.AsReadOnly(Array.Empty<LlamaCppBackend>());

    private static ReadOnlyCollection<LlamaCppVisibleDevice> EmptyDevices() =>
        Array.AsReadOnly(Array.Empty<LlamaCppVisibleDevice>());

    private static void ValidateCaptureTime(DateTimeOffset capturedAtUtc)
    {
        if (capturedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Capture time must use the UTC offset.", nameof(capturedAtUtc));
        }
    }
}
