using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelOptimization.Export;

internal sealed record VerifiedPersistentExportTarget
{
    internal VerifiedPersistentExportTarget(
        OptimizationRoute route,
        Guid optimizationPlanId,
        string configurationSha256,
        string sourceSha256,
        bool sourceUnchanged,
        string outputIdentity,
        string outputManifestSha256,
        ulong outputLengthBytes)
    {
        if (!Enum.IsDefined(route)) throw new ArgumentOutOfRangeException(nameof(route));
        if (optimizationPlanId == Guid.Empty)
            throw new ArgumentException("A verified export target requires its optimization plan identity.", nameof(optimizationPlanId));
        if (outputLengthBytes == 0)
            throw new ArgumentOutOfRangeException(nameof(outputLengthBytes), "A persistent export target cannot be empty.");

        Route = route;
        OptimizationPlanId = optimizationPlanId;
        ConfigurationSha256 = OptimizationExportContractGuard.RequireSha256(configurationSha256, nameof(configurationSha256));
        SourceSha256 = OptimizationExportContractGuard.RequireSha256(sourceSha256, nameof(sourceSha256));
        if (!sourceUnchanged) throw new ArgumentException("The canonical execution result must attest that its source is unchanged.", nameof(sourceUnchanged));
        SourceUnchanged = true;
        OutputIdentity = OptimizationExportContractGuard.RequireOpaqueId(outputIdentity, nameof(outputIdentity));
        OutputManifestSha256 = OptimizationExportContractGuard.RequireSha256(outputManifestSha256, nameof(outputManifestSha256));
        OutputLengthBytes = outputLengthBytes;
    }

    internal OptimizationRoute Route { get; }
    internal Guid OptimizationPlanId { get; }
    internal string ConfigurationSha256 { get; }
    internal string SourceSha256 { get; }
    internal bool SourceUnchanged { get; }
    internal string OutputIdentity { get; }
    internal string OutputManifestSha256 { get; }
    internal ulong OutputLengthBytes { get; }

    internal static VerifiedPersistentExportTarget FromExecutionResult(OptimizationExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Status != OptimizationExecutionStatus.SucceededPersistent || !result.SourceUnchanged
            || result.OutputIdentity is null || result.OutputManifestSha256 is null)
            throw new ArgumentException("Only the exact canonical persistent-success result can be exported.", nameof(result));
        return new(result.Route, result.OptimizationPlanId, result.ConfigurationSha256,
            result.SourceSha256, result.SourceUnchanged, result.OutputIdentity,
            result.OutputManifestSha256, result.OutputSizeBytes);
    }
}

internal sealed record OptimizationExportReceipt
{
    internal OptimizationExportReceipt(VerifiedPersistentExportTarget target, string publishedOutputIdentity)
    {
        ArgumentNullException.ThrowIfNull(target);
        Route = target.Route;
        OptimizationPlanId = target.OptimizationPlanId;
        ConfigurationSha256 = target.ConfigurationSha256;
        SourceSha256 = target.SourceSha256;
        SourceUnchanged = target.SourceUnchanged;
        OutputIdentity = target.OutputIdentity;
        Sha256 = target.OutputManifestSha256;
        LengthBytes = target.OutputLengthBytes;
        PublishedOutputIdentity = OptimizationExportContractGuard.RequireOpaqueId(publishedOutputIdentity, nameof(publishedOutputIdentity));
    }
    internal OptimizationRoute Route { get; }
    internal Guid OptimizationPlanId { get; }
    internal string ConfigurationSha256 { get; }
    internal string SourceSha256 { get; }
    internal bool SourceUnchanged { get; }
    internal string OutputIdentity { get; }
    internal string Sha256 { get; }
    internal ulong LengthBytes { get; }
    internal string PublishedOutputIdentity { get; }
}

internal enum OptimizationExportStage { ChoosingDestination, Writing, Verifying, Publishing, CleaningUp }

internal sealed record OptimizationExportProgress
{
    internal OptimizationExportProgress(OptimizationExportStage stage, double? fraction = null)
    {
        if (!Enum.IsDefined(stage)) throw new ArgumentOutOfRangeException(nameof(stage));
        if (fraction is < 0 or > 1 || double.IsNaN(fraction ?? 0)) throw new ArgumentOutOfRangeException(nameof(fraction));
        Stage = stage;
        Fraction = fraction;
    }
    internal OptimizationExportStage Stage { get; }
    internal double? Fraction { get; }
}

internal enum OptimizationExportFailure { None, IntegrityMismatch, InsufficientSpace, DestinationUnavailable, PublicationFailure, CleanupFailure }
internal enum OptimizationExportResultKind { Succeeded, Cancelled, Failed }

internal sealed record OptimizationExportResult
{
    private OptimizationExportResult(OptimizationExportResultKind kind, OptimizationExportReceipt? receipt, OptimizationExportFailure failure)
    { Kind = kind; Receipt = receipt; Failure = failure; }
    internal OptimizationExportResultKind Kind { get; }
    internal OptimizationExportReceipt? Receipt { get; }
    internal OptimizationExportFailure Failure { get; }
    internal static OptimizationExportResult Succeeded(OptimizationExportReceipt receipt) =>
        new(OptimizationExportResultKind.Succeeded, receipt ?? throw new ArgumentNullException(nameof(receipt)), OptimizationExportFailure.None);
    internal static OptimizationExportResult Cancelled() => new(OptimizationExportResultKind.Cancelled, null, OptimizationExportFailure.None);
    internal static OptimizationExportResult Failed(OptimizationExportFailure failure)
    {
        if (failure is OptimizationExportFailure.None || !Enum.IsDefined(failure)) throw new ArgumentOutOfRangeException(nameof(failure));
        return new(OptimizationExportResultKind.Failed, null, failure);
    }
}

internal interface IOptimizationExportService
{
    Task<OptimizationExportResult> ExportAsync(VerifiedPersistentExportTarget target, IProgress<OptimizationExportProgress> progress, CancellationToken cancellationToken);
}

internal static class OptimizationExportContractGuard
{
    internal static string RequireSha256(string value, string parameter)
    {
        if (value is null || value.Length != 64 || value.Any(c => c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            throw new ArgumentException("The value must be a canonical lowercase SHA-256 digest.", parameter);
        return value;
    }

    internal static string RequireOpaqueId(string value, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128 || value.Any(char.IsControl)
            || value.Contains('/') || value.Contains('\\') || value.Contains(':') || value.Contains('?') || value.Contains('#'))
            throw new ArgumentException("The value must be a bounded opaque identity.", parameter);
        return value;
    }
}
