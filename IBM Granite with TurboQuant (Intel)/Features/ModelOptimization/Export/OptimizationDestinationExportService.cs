using System;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.ModelOptimization.Storage;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.Features.ModelOptimization.Export;

internal delegate Task<string?> OptimizationExportDestinationPicker(
    OptimizationRoute route,
    CancellationToken cancellationToken);

internal delegate Task<OptimizationDestinationExportResult>
    OptimizationPersistentDestinationExporter(
        OptimizationExecutionResult result,
        string destination,
        ulong maximumBytes,
        CancellationToken cancellationToken);

/// <summary>
/// Adapts the verified optimization destination backend to the UI export
/// controller. The canonical execution result is captured at composition time;
/// callers cannot substitute another plan, configuration, or output.
/// </summary>
internal sealed class OptimizationDestinationExportService : IOptimizationExportService
{
    private const ulong MaximumExportBytes = 1UL << 40;
    private const ulong OpenVinoMetadataAllowanceBytes = 64UL * 1024 * 1024;

    private readonly OptimizationExecutionResult _result;
    private readonly VerifiedPersistentExportTarget _target;
    private readonly OptimizationExportDestinationPicker _pickDestination;
    private readonly OptimizationPersistentDestinationExporter _export;

    internal OptimizationDestinationExportService(
        OptimizationExecutionResult result,
        OptimizationExportDestinationPicker pickDestination,
        OptimizationPersistentDestinationExporter export)
    {
        ArgumentNullException.ThrowIfNull(result);
        _target = VerifiedPersistentExportTarget.FromExecutionResult(result);
        _result = result;
        _pickDestination = pickDestination
            ?? throw new ArgumentNullException(nameof(pickDestination));
        _export = export ?? throw new ArgumentNullException(nameof(export));
    }

    public async Task<OptimizationExportResult> ExportAsync(
        VerifiedPersistentExportTarget target,
        IProgress<OptimizationExportProgress> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(progress);
        cancellationToken.ThrowIfCancellationRequested();
        if (!MatchesCanonicalTarget(target))
        {
            return OptimizationExportResult.Failed(
                OptimizationExportFailure.IntegrityMismatch);
        }

        string? destination = await _pickDestination(
            _result.Route, cancellationToken).ConfigureAwait(false);
        if (destination is null)
        {
            return OptimizationExportResult.Cancelled();
        }

        cancellationToken.ThrowIfCancellationRequested();
        progress.Report(new(OptimizationExportStage.Writing));
        OptimizationDestinationExportResult exported = await _export(
            _result,
            destination,
            MaximumBytesFor(_result),
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (!MatchesCanonicalResult(exported))
        {
            return OptimizationExportResult.Failed(
                OptimizationExportFailure.IntegrityMismatch);
        }

        if (exported.Disposition !=
            OptimizationDestinationExportDisposition.Succeeded)
        {
            return OptimizationExportResult.Failed(MapFailure(exported.Disposition));
        }

        progress.Report(new(OptimizationExportStage.Verifying));
        progress.Report(new(OptimizationExportStage.Publishing));
        return OptimizationExportResult.Succeeded(
            new OptimizationExportReceipt(
                _target,
                $"export-{_result.ExecutionId:N}"));
    }

    private bool MatchesCanonicalTarget(VerifiedPersistentExportTarget target) =>
        target.Route == _target.Route
        && target.OptimizationPlanId == _target.OptimizationPlanId
        && string.Equals(target.ConfigurationSha256,
            _target.ConfigurationSha256, StringComparison.Ordinal)
        && string.Equals(target.SourceSha256,
            _target.SourceSha256, StringComparison.Ordinal)
        && target.SourceUnchanged
        && string.Equals(target.OutputIdentity,
            _target.OutputIdentity, StringComparison.Ordinal)
        && string.Equals(target.OutputManifestSha256,
            _target.OutputManifestSha256, StringComparison.Ordinal)
        && target.OutputLengthBytes == _target.OutputLengthBytes;

    private bool MatchesCanonicalResult(
        OptimizationDestinationExportResult exported) =>
        exported.Route == _result.Route
        && exported.OptimizationPlanId == _result.OptimizationPlanId
        && exported.ExecutionId == _result.ExecutionId
        && string.Equals(exported.ConfigurationSha256,
            _result.ConfigurationSha256, StringComparison.Ordinal)
        && string.Equals(exported.OutputIdentity,
            _result.OutputIdentity, StringComparison.Ordinal)
        && string.Equals(exported.OutputManifestSha256,
            _result.OutputManifestSha256, StringComparison.Ordinal)
        && exported.OutputLengthBytes == _result.OutputSizeBytes;

    private static ulong MaximumBytesFor(OptimizationExecutionResult result) =>
        result.Route == OptimizationRoute.OpenVino
        && result.OutputSizeBytes
            <= MaximumExportBytes - OpenVinoMetadataAllowanceBytes
            ? result.OutputSizeBytes + OpenVinoMetadataAllowanceBytes
            : result.OutputSizeBytes;

    private static OptimizationExportFailure MapFailure(
        OptimizationDestinationExportDisposition disposition) => disposition switch
        {
            OptimizationDestinationExportDisposition.Oversized =>
                OptimizationExportFailure.InsufficientSpace,
            OptimizationDestinationExportDisposition.DestinationRejected or
            OptimizationDestinationExportDisposition.DestinationExists =>
                OptimizationExportFailure.DestinationUnavailable,
            OptimizationDestinationExportDisposition.CleanupFailed =>
                OptimizationExportFailure.CleanupFailure,
            OptimizationDestinationExportDisposition.ResultRejected or
            OptimizationDestinationExportDisposition.RuntimeOnly =>
                OptimizationExportFailure.IntegrityMismatch,
            _ => OptimizationExportFailure.PublicationFailure,
        };
}
