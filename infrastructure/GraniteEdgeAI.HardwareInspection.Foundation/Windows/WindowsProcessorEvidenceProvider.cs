using GraniteEdgeAI.HardwareInspection.Foundation.Validation;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Windows;

internal enum WindowsProcessorApiStatus
{
    Success,
    NameUnavailable,
    TopologyUnavailable,
    InvalidTopology,
    UnsupportedArchitecture,
    NativeApiUnavailable,
}

internal enum WindowsProcessorApiArchitecture
{
    Unsupported,
    X86,
    X64,
    Arm64,
}

internal sealed record WindowsProcessorApiResult(
    WindowsProcessorApiStatus Status,
    string? Name,
    WindowsProcessorApiArchitecture Architecture,
    int PhysicalCoreCount,
    int LogicalProcessorCount);

internal interface IWindowsProcessorApi
{
    WindowsProcessorApiResult Capture();
}

public sealed class WindowsProcessorEvidenceProvider
{
    private readonly IWindowsProcessorApi _processorApi;
    private readonly TimeProvider _timeProvider;

    internal WindowsProcessorEvidenceProvider(
        IWindowsProcessorApi processorApi,
        TimeProvider timeProvider)
    {
        _processorApi = processorApi ?? throw new ArgumentNullException(nameof(processorApi));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public ValueTask<WindowsProcessorEvidence> CaptureAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WindowsProcessorApiResult result = _processorApi.Capture();
        DateTimeOffset capturedAtUtc = _timeProvider.GetUtcNow().ToUniversalTime();

        if (result.Status != WindowsProcessorApiStatus.Success)
        {
            return ValueTask.FromResult(WindowsProcessorEvidence.Unavailable(
                MapFailure(result.Status),
                capturedAtUtc));
        }

        if (!HardwareText.IsSafe(result.Name, 256))
        {
            return ValueTask.FromResult(WindowsProcessorEvidence.Unavailable(
                WindowsProcessorDiagnosticCode.NameUnavailable,
                capturedAtUtc));
        }

        WindowsProcessorArchitecture? architecture = MapArchitecture(result.Architecture);
        if (!architecture.HasValue)
        {
            return ValueTask.FromResult(WindowsProcessorEvidence.Unavailable(
                WindowsProcessorDiagnosticCode.UnsupportedArchitecture,
                capturedAtUtc));
        }

        if (result.PhysicalCoreCount is < 1 or > 4096 ||
            result.LogicalProcessorCount is < 1 or > 4096 ||
            result.PhysicalCoreCount > result.LogicalProcessorCount)
        {
            return ValueTask.FromResult(WindowsProcessorEvidence.Unavailable(
                WindowsProcessorDiagnosticCode.TopologyInconsistent,
                capturedAtUtc));
        }

        return ValueTask.FromResult(WindowsProcessorEvidence.Available(
            result.Name!,
            architecture.Value,
            result.PhysicalCoreCount,
            result.LogicalProcessorCount,
            capturedAtUtc));
    }

    private static WindowsProcessorDiagnosticCode MapFailure(WindowsProcessorApiStatus status) =>
        status switch
        {
            WindowsProcessorApiStatus.NameUnavailable => WindowsProcessorDiagnosticCode.NameUnavailable,
            WindowsProcessorApiStatus.TopologyUnavailable => WindowsProcessorDiagnosticCode.TopologyUnavailable,
            WindowsProcessorApiStatus.InvalidTopology => WindowsProcessorDiagnosticCode.TopologyInconsistent,
            WindowsProcessorApiStatus.UnsupportedArchitecture => WindowsProcessorDiagnosticCode.UnsupportedArchitecture,
            WindowsProcessorApiStatus.NativeApiUnavailable => WindowsProcessorDiagnosticCode.NativeApiUnavailable,
            _ => WindowsProcessorDiagnosticCode.NativeApiUnavailable,
        };

    private static WindowsProcessorArchitecture? MapArchitecture(
        WindowsProcessorApiArchitecture architecture) =>
        architecture switch
        {
            WindowsProcessorApiArchitecture.X86 => WindowsProcessorArchitecture.X86,
            WindowsProcessorApiArchitecture.X64 => WindowsProcessorArchitecture.X64,
            WindowsProcessorApiArchitecture.Arm64 => WindowsProcessorArchitecture.Arm64,
            _ => null,
        };
}
