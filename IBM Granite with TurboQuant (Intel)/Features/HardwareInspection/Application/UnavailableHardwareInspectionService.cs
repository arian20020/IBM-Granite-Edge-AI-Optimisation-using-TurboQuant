using System;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.HardwareInspection.Application;

/// <summary>
/// Keeps the product route truthful until an evidence-approved collector is
/// registered. It never fabricates hardware facts or starts an external tool.
/// </summary>
internal sealed class UnavailableHardwareInspectionService
    : IHardwareInspectionService
{
    internal static UnavailableHardwareInspectionService Instance { get; } = new();

    private UnavailableHardwareInspectionService()
    {
    }

    public Task<HardwareInspectionRunResult> RunAsync(
        Guid inspectionId,
        IProgress<HardwareInspectionRunProgress> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(HardwareInspectionRunResult.CreateFailed(
            inspectionId,
            HardwareInspectionFailureKind.ApplicationRepairRequired,
            "HI-PROVIDER-NOT-AVAILABLE"));
    }
}
