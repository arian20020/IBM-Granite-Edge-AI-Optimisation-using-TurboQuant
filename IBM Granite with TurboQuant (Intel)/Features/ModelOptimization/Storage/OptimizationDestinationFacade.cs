using System;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.ModelInspection.SourceCustody;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;

namespace GraniteEdgeAI.Features.ModelOptimization.Storage;

internal enum OptimizationDestinationExportDisposition
{
    Succeeded,
    RuntimeOnly,
    ResultRejected,
    DestinationRejected,
    DestinationExists,
    Oversized,
    CleanupFailed,
    Failed,
}

internal sealed record OptimizationDestinationExportResult(
    OptimizationDestinationExportDisposition Disposition,
    OptimizationRoute Route,
    Guid OptimizationPlanId,
    Guid ExecutionId,
    string ConfigurationSha256,
    string? OutputIdentity,
    string? OutputManifestSha256,
    ulong OutputLengthBytes)
{
    internal static OptimizationDestinationExportResult Succeeded(
        OptimizationExecutionResult result) =>
        new(OptimizationDestinationExportDisposition.Succeeded, result.Route,
            result.OptimizationPlanId, result.ExecutionId,
            result.ConfigurationSha256, result.OutputIdentity,
            result.OutputManifestSha256, result.OutputSizeBytes);

    internal static OptimizationDestinationExportResult For(
        OptimizationExecutionResult result,
        OptimizationDestinationExportDisposition disposition) =>
        new(disposition, result.Route, result.OptimizationPlanId,
            result.ExecutionId, result.ConfigurationSha256,
            result.OutputIdentity, result.OutputManifestSha256,
            result.OutputSizeBytes);
}

internal abstract class OptimizationChatTarget(
    OptimizationExecutionResult result) : IDisposable
{
    private int _disposed;

    internal OptimizationExecutionResult Result { get; } = result;
    internal OptimizationRoute Route => Result.Route;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            DisposeCore();
        }
    }

    protected abstract void DisposeCore();
}

internal interface IOptimizationDestinationRoute
{
    OptimizationRoute Route { get; }

    Task<OptimizationChatTarget?> CreateChatTargetAsync(
        OptimizationExecutionResult result,
        CancellationToken cancellationToken);

    Task<OptimizationDestinationExportResult> ExportPersistentAsync(
        OptimizationExecutionResult result,
        string destination,
        ulong maximumBytes,
        CancellationToken cancellationToken);
}

internal sealed class GgufOptimizationChatTarget : OptimizationChatTarget
{
    private IDisposable? _custodyLease;

    internal GgufOptimizationChatTarget(
        OptimizationExecutionResult result,
        string verifiedModelPath,
        string verifiedModelSha256,
        ulong verifiedModelLengthBytes,
        GgufExecutionPayload runtimeOptions,
        IDisposable custodyLease)
        : base(result)
    {
        VerifiedModelPath = verifiedModelPath;
        VerifiedModelSha256 = verifiedModelSha256;
        VerifiedModelLengthBytes = verifiedModelLengthBytes;
        RuntimeOptions = runtimeOptions;
        _custodyLease = custodyLease;
    }

    internal string VerifiedModelPath { get; }
    internal string VerifiedModelSha256 { get; }
    internal ulong VerifiedModelLengthBytes { get; }
    internal GgufExecutionPayload RuntimeOptions { get; }

    protected override void DisposeCore() =>
        Interlocked.Exchange(ref _custodyLease, null)?.Dispose();
}

internal sealed class GgufOptimizationDestinationRoute(
    OptimizationExecutionPlan plan,
    OptimizationOutputRegistry outputs,
    ModelSourceCustodyRegistry sourceCustody) : IOptimizationDestinationRoute
{
    internal const ulong MaximumExportBytes = 1UL << 40;

    public OptimizationRoute Route => OptimizationRoute.Gguf;

    public Task<OptimizationChatTarget?> CreateChatTargetAsync(
        OptimizationExecutionResult result,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!MatchesPlan(result) || plan.ExecutionPayload.Gguf is not { } options)
        {
            return Task.FromResult<OptimizationChatTarget?>(null);
        }

        if (result.ProducedPersistentArtifact)
        {
            if (!outputs.TryAcquirePublishedGguf(
                    result, out GgufPublishedOutputLease? publishedLease))
            {
                return Task.FromResult<OptimizationChatTarget?>(null);
            }
            return Task.FromResult<OptimizationChatTarget?>(
                new GgufOptimizationChatTarget(
                    result, publishedLease!.FilePath, publishedLease.FileSha256,
                    publishedLease.FileLengthBytes, options, publishedLease));
        }

        ModelSourceCustodyKey key = new(
            Guid.ParseExact(result.ModelInspectionHandoffId, "N"),
            result.SourceSha256,
            checked((long)result.SourceLengthBytes),
            OptimizationRoute.Gguf);
        if (!sourceCustody.TryAcquire(key, out ModelSourceLease? lease))
        {
            return Task.FromResult<OptimizationChatTarget?>(null);
        }
        return Task.FromResult<OptimizationChatTarget?>(
            new GgufOptimizationChatTarget(
                result, lease!.SourcePath, result.SourceSha256,
                result.SourceLengthBytes, options, lease));
    }

    public async Task<OptimizationDestinationExportResult> ExportPersistentAsync(
        OptimizationExecutionResult result,
        string destination,
        ulong maximumBytes,
        CancellationToken cancellationToken)
    {
        if (!MatchesPlan(result))
        {
            return OptimizationDestinationExportResult.For(
                result, OptimizationDestinationExportDisposition.ResultRejected);
        }
        if (maximumBytes > MaximumExportBytes)
        {
            maximumBytes = MaximumExportBytes;
        }
        if (result.OutputSizeBytes > maximumBytes)
        {
            return OptimizationDestinationExportResult.For(
                result, OptimizationDestinationExportDisposition.Oversized);
        }
        try
        {
            bool exported = await OptimizationExporter.ExportGgufAsync(
                outputs, result, destination, maximumBytes, cancellationToken)
                .ConfigureAwait(false);
            return OptimizationDestinationExportResult.For(result,
                exported
                    ? OptimizationDestinationExportDisposition.Succeeded
                    : OptimizationDestinationExportDisposition.ResultRejected);
        }
        catch (IOException) when (File.Exists(destination) || Directory.Exists(destination))
        {
            return OptimizationDestinationExportResult.For(
                result, OptimizationDestinationExportDisposition.DestinationExists);
        }
        catch (GgufExportCleanupException)
        {
            return OptimizationDestinationExportResult.For(
                result, OptimizationDestinationExportDisposition.CleanupFailed);
        }
    }

    private bool MatchesPlan(OptimizationExecutionResult result) =>
        result.Route == Route
        && result.OptimizationPlanId == plan.OptimizationPlanId
        && string.Equals(result.ConfigurationSha256, plan.ConfigurationSha256,
            StringComparison.Ordinal)
        && string.Equals(result.SourceSha256, plan.Binding.ModelSha256,
            StringComparison.Ordinal)
        && result.SourceLengthBytes == plan.Binding.ModelLengthBytes
        && string.Equals(result.ModelInspectionRunId,
            plan.Binding.ModelInspectionRunId, StringComparison.Ordinal)
        && string.Equals(result.ModelInspectionHandoffId,
            plan.Binding.ModelInspectionHandoffId, StringComparison.Ordinal)
        && string.Equals(result.ProductHardwareRunId,
            plan.Binding.ProductHardwareRunId, StringComparison.Ordinal)
        && string.Equals(result.HardwareSnapshotSha256,
            plan.Binding.HardwareSnapshotSha256, StringComparison.Ordinal);
}

internal sealed class OptimizationDestinationFacade
{
    private readonly IOptimizationDestinationRoute _gguf;
    private readonly IOptimizationDestinationRoute _openVino;

    internal OptimizationDestinationFacade(
        IOptimizationDestinationRoute first,
        IOptimizationDestinationRoute second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        if (first.Route == second.Route
            || first.Route is not (OptimizationRoute.Gguf or OptimizationRoute.OpenVino)
            || second.Route is not (OptimizationRoute.Gguf or OptimizationRoute.OpenVino))
        {
            throw new ArgumentException(
                "The destination facade requires one exact provider for each route.");
        }

        _gguf = first.Route == OptimizationRoute.Gguf ? first : second;
        _openVino = first.Route == OptimizationRoute.OpenVino ? first : second;
    }

    internal async Task<OptimizationChatTarget?> CreateChatTargetAsync(
        OptimizationExecutionResult result,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        cancellationToken.ThrowIfCancellationRequested();
        if (!result.IsSuccessful)
        {
            return null;
        }

        OptimizationChatTarget? target = await RouteFor(result).CreateChatTargetAsync(
            result, cancellationToken).ConfigureAwait(false);
        if (cancellationToken.IsCancellationRequested)
        {
            target?.Dispose();
            cancellationToken.ThrowIfCancellationRequested();
        }
        if (target is not null
            && (!ReferenceEquals(target.Result, result)
                || target.Route != result.Route))
        {
            target.Dispose();
            return null;
        }
        return target;
    }

    internal async Task<OptimizationDestinationExportResult> ExportPersistentAsync(
        OptimizationExecutionResult result,
        string destination,
        ulong maximumBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        cancellationToken.ThrowIfCancellationRequested();
        if (!result.IsSuccessful)
        {
            return OptimizationDestinationExportResult.For(
                result, OptimizationDestinationExportDisposition.ResultRejected);
        }
        if (!result.ProducedPersistentArtifact)
        {
            return OptimizationDestinationExportResult.For(
                result, OptimizationDestinationExportDisposition.RuntimeOnly);
        }
        OptimizationDestinationExportResult export = await RouteFor(result)
            .ExportPersistentAsync(result, destination, maximumBytes,
                cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (export.Route != result.Route
            || export.OptimizationPlanId != result.OptimizationPlanId
            || export.ExecutionId != result.ExecutionId
            || !string.Equals(export.ConfigurationSha256,
                result.ConfigurationSha256, StringComparison.Ordinal)
            || !string.Equals(export.OutputIdentity,
                result.OutputIdentity, StringComparison.Ordinal)
            || !string.Equals(export.OutputManifestSha256,
                result.OutputManifestSha256, StringComparison.Ordinal)
            || export.OutputLengthBytes != result.OutputSizeBytes)
        {
            return OptimizationDestinationExportResult.For(
                result, OptimizationDestinationExportDisposition.ResultRejected);
        }
        return export;
    }

    private IOptimizationDestinationRoute RouteFor(
        OptimizationExecutionResult result) => result.Route switch
        {
            OptimizationRoute.Gguf => _gguf,
            OptimizationRoute.OpenVino => _openVino,
            _ => throw new ArgumentOutOfRangeException(
                nameof(result), result.Route, "The optimization route is not executable."),
        };
}
