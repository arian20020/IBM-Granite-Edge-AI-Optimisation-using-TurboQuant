using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.ModelOptimization.Storage;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;

namespace GraniteEdgeAI.Features.ModelOptimization.Export;

internal delegate Task<GgufRuntimeProfileBundleExportResult>
    OptimizationRuntimeBundleDestinationExporter(
        OptimizationExecutionResult result,
        string destination,
        ulong maximumBytes,
        IProgress<GgufRuntimeProfileBundleExportStage> progress,
        CancellationToken cancellationToken);

internal delegate IDisposable? OptimizationRuntimeBundleSourceCustodyAcquirer(
    VerifiedGgufRuntimeBundleExportTarget target);

internal sealed class GgufRuntimeProfileBundleExportService
    : IGgufRuntimeBundleExportService
{
    private const int MaximumDestinationAttempts = 1000;
    private const ulong MetadataAllowanceBytes = 64UL * 1024 * 1024;
    private const ulong MaximumBundleBytes = 1UL << 40;

    private readonly OptimizationExecutionPlan _plan;
    private readonly OptimizationExecutionResult _result;
    private readonly VerifiedGgufRuntimeBundleExportTarget _target;
    private readonly OptimizationRuntimeBundleSourceCustodyAcquirer
        _acquireSourceCustody;
    private readonly OptimizationExportDestinationPicker _pickDestination;
    private readonly OptimizationRuntimeBundleDestinationExporter _export;

    internal GgufRuntimeProfileBundleExportService(
        OptimizationExecutionPlan plan,
        OptimizationExecutionResult result,
        OptimizationRuntimeBundleSourceCustodyAcquirer acquireSourceCustody,
        OptimizationExportDestinationPicker pickDestination,
        OptimizationRuntimeBundleDestinationExporter export)
    {
        _plan = plan ?? throw new ArgumentNullException(nameof(plan));
        _result = result ?? throw new ArgumentNullException(nameof(result));
        _target = VerifiedGgufRuntimeBundleExportTarget.FromExecution(plan, result);
        _acquireSourceCustody = acquireSourceCustody
            ?? throw new ArgumentNullException(nameof(acquireSourceCustody));
        _pickDestination = pickDestination
            ?? throw new ArgumentNullException(nameof(pickDestination));
        _export = export ?? throw new ArgumentNullException(nameof(export));
    }

    internal static bool TryCreateAbsentDestination(
        string? selectedParentDirectory,
        OptimizationExecutionPlan? plan,
        OptimizationExecutionResult? result,
        out string? destination)
    {
        destination = null;
        if (plan is null || result is null
            || !TryGetCanonicalLocalParent(
                selectedParentDirectory, out string? parent))
        {
            return false;
        }

        VerifiedGgufRuntimeBundleExportTarget target;
        try
        {
            target = VerifiedGgufRuntimeBundleExportTarget.FromExecution(
                plan, result);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        if (!target.Matches(plan, result)
            || plan.ExecutionPayload.Gguf is not { } payload
            || CacheToken(payload.KeyCacheType, payload.ValueCacheType)
                is not { } cacheToken)
        {
            return false;
        }

        string baseLeaf = $"GGUF-Original-{cacheToken}";
        for (int attempt = 1; attempt <= MaximumDestinationAttempts; attempt++)
        {
            string leaf = attempt == 1
                ? baseLeaf
                : $"{baseLeaf}-{attempt}";
            string candidate = Path.Combine(parent!, leaf);
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                destination = candidate;
                return true;
            }
        }

        return false;
    }

    internal static string? CacheToken(
        GgufCacheType keyCache,
        GgufCacheType valueCache)
    {
        if (keyCache != valueCache)
        {
            return null;
        }

        return keyCache switch
        {
            GgufCacheType.F16 => "F16",
            GgufCacheType.Q8Zero => "Q8_0",
            GgufCacheType.Turbo2 => "TQ2",
            GgufCacheType.Turbo3 => "TQ3",
            GgufCacheType.Turbo4 => "TQ4",
            _ => null,
        };
    }

    public async Task<OptimizationExportResult> ExportAsync(
        VerifiedGgufRuntimeBundleExportTarget target,
        IProgress<OptimizationExportProgress> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(progress);
        cancellationToken.ThrowIfCancellationRequested();
        if (!target.Matches(_plan, _result)
            || !SameTarget(target, _target))
        {
            return OptimizationExportResult.Failed(
                OptimizationExportFailure.IntegrityMismatch);
        }

        using IDisposable? sourceCustody = _acquireSourceCustody(target);
        if (sourceCustody is null)
        {
            return OptimizationExportResult.Failed(
                OptimizationExportFailure.IntegrityMismatch);
        }

        string? destination = await _pickDestination(
            OptimizationRoute.Gguf, cancellationToken).ConfigureAwait(false);
        if (destination is null)
        {
            return OptimizationExportResult.Cancelled();
        }

        cancellationToken.ThrowIfCancellationRequested();
        GgufRuntimeProfileBundleExportResult exported = await _export(
            _result,
            destination,
            MaximumBytesFor(_plan),
            new RuntimeProgress(progress),
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (exported.Disposition
            != GgufRuntimeProfileBundleExportDisposition.Succeeded)
        {
            return OptimizationExportResult.Failed(MapFailure(exported.Disposition));
        }
        if (exported.BundleManifestSha256 is null
            || exported.BundleManifestLengthBytes == 0
            || !HasCoherentBundleLength(exported))
        {
            return OptimizationExportResult.Failed(
                OptimizationExportFailure.IntegrityMismatch);
        }
        return OptimizationExportResult.Succeeded(
            new GgufRuntimeBundleExportReceipt(
                _target,
                exported.BundleManifestSha256,
                exported.BundleManifestLengthBytes,
                exported.BundleLengthBytes));
    }

    private bool HasCoherentBundleLength(
        GgufRuntimeProfileBundleExportResult exported)
    {
        try
        {
            ulong modelAndManifest = checked(
                _target.SourceLengthBytes
                + exported.BundleManifestLengthBytes);
            return exported.BundleLengthBytes > modelAndManifest;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static bool TryGetCanonicalLocalParent(
        string? selectedParentDirectory,
        out string? parent)
    {
        parent = null;
        if (string.IsNullOrWhiteSpace(selectedParentDirectory)
            || !Path.IsPathFullyQualified(selectedParentDirectory)
            || selectedParentDirectory.StartsWith("\\\\", StringComparison.Ordinal)
            || selectedParentDirectory.StartsWith("\\\\?\\", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            string fullPath = Path.GetFullPath(selectedParentDirectory);
            string root = Path.GetPathRoot(fullPath) ?? string.Empty;
            if (root.Length < 3
                || root[1] != ':'
                || !string.Equals(
                    Path.TrimEndingDirectorySeparator(fullPath),
                    Path.TrimEndingDirectorySeparator(selectedParentDirectory),
                    StringComparison.OrdinalIgnoreCase)
                || !Directory.Exists(fullPath)
                || (File.GetAttributes(fullPath)
                    & FileAttributes.ReparsePoint) != 0
                || new DriveInfo(root).DriveType == DriveType.Network)
            {
                return false;
            }

            parent = fullPath;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
            or IOException
            or NotSupportedException
            or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool SameTarget(
        VerifiedGgufRuntimeBundleExportTarget first,
        VerifiedGgufRuntimeBundleExportTarget second) =>
        first.OptimizationPlanId == second.OptimizationPlanId
        && first.ExecutionId == second.ExecutionId
        && string.Equals(first.ConfigurationSha256,
            second.ConfigurationSha256, StringComparison.Ordinal)
        && string.Equals(first.SourceSha256,
            second.SourceSha256, StringComparison.Ordinal)
        && first.SourceLengthBytes == second.SourceLengthBytes
        && string.Equals(first.ModelInspectionRunId,
            second.ModelInspectionRunId, StringComparison.Ordinal)
        && string.Equals(first.ModelInspectionHandoffId,
            second.ModelInspectionHandoffId, StringComparison.Ordinal)
        && string.Equals(first.ProductHardwareRunId,
            second.ProductHardwareRunId, StringComparison.Ordinal)
        && string.Equals(first.HardwareSnapshotSha256,
            second.HardwareSnapshotSha256, StringComparison.Ordinal);

    private static ulong MaximumBytesFor(OptimizationExecutionPlan plan)
    {
        ulong bounded = plan.Binding.ModelLengthBytes
            > MaximumBundleBytes - MetadataAllowanceBytes
                ? MaximumBundleBytes
                : plan.Binding.ModelLengthBytes + MetadataAllowanceBytes;
        return Math.Min(bounded, MaximumBundleBytes);
    }

    private static OptimizationExportFailure MapFailure(
        GgufRuntimeProfileBundleExportDisposition disposition) => disposition switch
        {
            GgufRuntimeProfileBundleExportDisposition.Oversized =>
                OptimizationExportFailure.InsufficientSpace,
            GgufRuntimeProfileBundleExportDisposition.DestinationRejected or
            GgufRuntimeProfileBundleExportDisposition.DestinationExists =>
                OptimizationExportFailure.DestinationUnavailable,
            GgufRuntimeProfileBundleExportDisposition.CleanupFailed =>
                OptimizationExportFailure.CleanupFailure,
            GgufRuntimeProfileBundleExportDisposition.ResultRejected or
            GgufRuntimeProfileBundleExportDisposition.SourceUnavailable or
            GgufRuntimeProfileBundleExportDisposition.SourceChanged =>
                OptimizationExportFailure.IntegrityMismatch,
            _ => OptimizationExportFailure.PublicationFailure,
        };

    private sealed class RuntimeProgress(
        IProgress<OptimizationExportProgress> progress)
        : IProgress<GgufRuntimeProfileBundleExportStage>
    {
        private OptimizationExportStage? _last;

        public void Report(GgufRuntimeProfileBundleExportStage value)
        {
            OptimizationExportStage mapped = value switch
            {
                GgufRuntimeProfileBundleExportStage.CopyingModel or
                GgufRuntimeProfileBundleExportStage.WritingProfile or
                GgufRuntimeProfileBundleExportStage.WritingManifest =>
                    OptimizationExportStage.Writing,
                GgufRuntimeProfileBundleExportStage.Verifying =>
                    OptimizationExportStage.Verifying,
                GgufRuntimeProfileBundleExportStage.Publishing =>
                    OptimizationExportStage.Publishing,
                _ => throw new ArgumentOutOfRangeException(nameof(value)),
            };
            if (_last != mapped)
            {
                _last = mapped;
                progress.Report(new OptimizationExportProgress(mapped));
            }
        }
    }
}
