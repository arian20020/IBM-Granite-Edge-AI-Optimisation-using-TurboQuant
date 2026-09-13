using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelOptimization.Export;

internal enum OptimizationExportTargetKind
{
    PersistentModel,
    GgufRuntimeBundle,
}

internal enum OptimizationExportReceiptKind
{
    PersistentModel,
    GgufRuntimeBundle,
}

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
    internal OptimizationExportTargetKind Kind =>
        OptimizationExportTargetKind.PersistentModel;

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

internal sealed record VerifiedGgufRuntimeBundleExportTarget
{
    private VerifiedGgufRuntimeBundleExportTarget(
        OptimizationExecutionPlan plan,
        OptimizationExecutionResult result)
    {
        Kind = OptimizationExportTargetKind.GgufRuntimeBundle;
        OptimizationPlanId = plan.OptimizationPlanId;
        ExecutionId = result.ExecutionId;
        ConfigurationSha256 = OptimizationExportContractGuard.RequireSha256(
            plan.ConfigurationSha256, nameof(plan.ConfigurationSha256));
        SourceSha256 = OptimizationExportContractGuard.RequireSha256(
            plan.Binding.ModelSha256, nameof(plan.Binding.ModelSha256));
        SourceLengthBytes = plan.Binding.ModelLengthBytes;
        ModelInspectionRunId = OptimizationExportContractGuard.RequireOpaqueId(
            plan.Binding.ModelInspectionRunId,
            nameof(plan.Binding.ModelInspectionRunId));
        ModelInspectionHandoffId = OptimizationExportContractGuard.RequireOpaqueId(
            plan.Binding.ModelInspectionHandoffId,
            nameof(plan.Binding.ModelInspectionHandoffId));
        ProductHardwareRunId = OptimizationExportContractGuard.RequireOpaqueId(
            plan.Binding.ProductHardwareRunId,
            nameof(plan.Binding.ProductHardwareRunId));
        HardwareSnapshotSha256 = OptimizationExportContractGuard.RequireSha256(
            plan.Binding.HardwareSnapshotSha256,
            nameof(plan.Binding.HardwareSnapshotSha256));
    }

    internal OptimizationExportTargetKind Kind { get; }
    internal OptimizationRoute Route => OptimizationRoute.Gguf;
    internal Guid OptimizationPlanId { get; }
    internal Guid ExecutionId { get; }
    internal string ConfigurationSha256 { get; }
    internal string SourceSha256 { get; }
    internal ulong SourceLengthBytes { get; }
    internal string ModelInspectionRunId { get; }
    internal string ModelInspectionHandoffId { get; }
    internal string ProductHardwareRunId { get; }
    internal string HardwareSnapshotSha256 { get; }

    internal static VerifiedGgufRuntimeBundleExportTarget FromExecution(
        OptimizationExecutionPlan plan,
        OptimizationExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(result);
        if (!MatchesRuntimeProfile(plan, result))
        {
            throw new ArgumentException(
                "Only the exact canonical GGUF runtime-profile result can be bundled.",
                nameof(result));
        }
        return new(plan, result);
    }

    internal bool Matches(
        OptimizationExecutionPlan plan,
        OptimizationExecutionResult result) =>
        MatchesRuntimeProfile(plan, result)
        && OptimizationPlanId == plan.OptimizationPlanId
        && ExecutionId == result.ExecutionId
        && string.Equals(ConfigurationSha256, plan.ConfigurationSha256,
            StringComparison.Ordinal)
        && string.Equals(SourceSha256, plan.Binding.ModelSha256,
            StringComparison.Ordinal)
        && SourceLengthBytes == plan.Binding.ModelLengthBytes
        && string.Equals(ModelInspectionRunId,
            plan.Binding.ModelInspectionRunId, StringComparison.Ordinal)
        && string.Equals(ModelInspectionHandoffId,
            plan.Binding.ModelInspectionHandoffId, StringComparison.Ordinal)
        && string.Equals(ProductHardwareRunId,
            plan.Binding.ProductHardwareRunId, StringComparison.Ordinal)
        && string.Equals(HardwareSnapshotSha256,
            plan.Binding.HardwareSnapshotSha256, StringComparison.Ordinal);

    private static bool MatchesRuntimeProfile(
        OptimizationExecutionPlan plan,
        OptimizationExecutionResult result)
    {
        if (plan.Route != OptimizationRoute.Gguf
            || !plan.IsExecutableBy(OptimizationExecutionPlan.CurrentContractVersion)
            || !plan.MatchesExecutionPayload(plan.ExecutionPayload)
            || plan.ExecutionPayload.Gguf is not { } payload
            || plan.ProducesPersistentArtifact
            || plan.Candidate.Configuration is not GgufRouteConfiguration
            {
                Weights: GgufWeightFormat.Imported,
            } configuration
            || plan.Candidate.Metrics.RequiresPersistentChange
            || plan.Candidate.Metrics.ContextTokens != payload.ContextSize
            || payload.PersistentTargetWeightFormat != GgufWeightFormat.Imported
            || payload.Quantiser is not null
            || payload.ConversionSource is not null
            || payload.RequantisationPolicy is not null
            || payload.RequiresPersistentConversion
            || payload.KeyCacheType != payload.ValueCacheType
            || !PayloadMatches(configuration, payload))
        {
            return false;
        }
        return result.Status == OptimizationExecutionStatus.SucceededRuntimeProfile
            && result.SupportCode == OptimizationSupportCode.None
            && result.SourceUnchanged
            && !result.ProducedPersistentArtifact
            && result.OutputSizeBytes == 0
            && result.ExecutionId != Guid.Empty
            && result.Route == plan.Route
            && result.OptimizationPlanId == plan.OptimizationPlanId
            && string.Equals(result.ConfigurationSha256,
                plan.ConfigurationSha256, StringComparison.Ordinal)
            && string.Equals(result.SourceSha256,
                plan.Binding.ModelSha256, StringComparison.Ordinal)
            && result.SourceLengthBytes == plan.Binding.ModelLengthBytes
            && string.Equals(result.ModelInspectionRunId,
                plan.Binding.ModelInspectionRunId, StringComparison.Ordinal)
            && string.Equals(result.ModelInspectionHandoffId,
                plan.Binding.ModelInspectionHandoffId, StringComparison.Ordinal)
            && string.Equals(result.ProductHardwareRunId,
                plan.Binding.ProductHardwareRunId, StringComparison.Ordinal)
            && string.Equals(result.HardwareSnapshotSha256,
                plan.Binding.HardwareSnapshotSha256, StringComparison.Ordinal)
            && string.Equals(result.OutputIdentity,
                $"gguf-profile-{plan.OptimizationPlanId:N}",
                StringComparison.Ordinal)
            && string.Equals(result.OutputManifestSha256,
                plan.ConfigurationSha256, StringComparison.Ordinal);
    }

    private static bool PayloadMatches(
        GgufRouteConfiguration configuration,
        GgufExecutionPayload payload)
    {
        GgufCacheType cache = configuration.KvCache switch
        {
            GgufKvCacheFormat.F16 => GgufCacheType.F16,
            GgufKvCacheFormat.Q8_0 => GgufCacheType.Q8Zero,
            GgufKvCacheFormat.TurboQuant4Bit => GgufCacheType.Turbo4,
            GgufKvCacheFormat.TurboQuant3Bit => GgufCacheType.Turbo3,
            GgufKvCacheFormat.TurboQuant2Bit => GgufCacheType.Turbo2,
            _ => (GgufCacheType)(-1),
        };
        GgufRuntimeBackend backend = configuration.Backend switch
        {
            CompatibilityBackend.Cpu => GgufRuntimeBackend.Cpu,
            CompatibilityBackend.IntelVulkan => GgufRuntimeBackend.Vulkan,
            CompatibilityBackend.IntelSycl => GgufRuntimeBackend.Sycl,
            _ => (GgufRuntimeBackend)(-1),
        };
        return payload.KeyCacheType == cache && payload.Backend == backend;
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
    internal OptimizationExportReceiptKind Kind =>
        OptimizationExportReceiptKind.PersistentModel;
}

internal sealed record GgufRuntimeBundleExportReceipt
{
    internal GgufRuntimeBundleExportReceipt(
        VerifiedGgufRuntimeBundleExportTarget target,
        string bundleManifestSha256,
        ulong bundleManifestLengthBytes,
        ulong bundleLengthBytes)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (bundleManifestLengthBytes == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bundleLengthBytes));
        }
        try
        {
            if (bundleLengthBytes <= checked(
                    target.SourceLengthBytes + bundleManifestLengthBytes))
            {
                throw new ArgumentOutOfRangeException(nameof(bundleLengthBytes));
            }
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(nameof(bundleLengthBytes));
        }
        Kind = OptimizationExportReceiptKind.GgufRuntimeBundle;
        OptimizationPlanId = target.OptimizationPlanId;
        ExecutionId = target.ExecutionId;
        ConfigurationSha256 = target.ConfigurationSha256;
        SourceSha256 = target.SourceSha256;
        BundleManifestSha256 = OptimizationExportContractGuard.RequireSha256(
            bundleManifestSha256, nameof(bundleManifestSha256));
        BundleManifestLengthBytes = bundleManifestLengthBytes;
        BundleLengthBytes = bundleLengthBytes;
    }

    internal OptimizationExportReceiptKind Kind { get; }
    internal OptimizationRoute Route => OptimizationRoute.Gguf;
    internal Guid OptimizationPlanId { get; }
    internal Guid ExecutionId { get; }
    internal string ConfigurationSha256 { get; }
    internal string SourceSha256 { get; }
    internal string BundleManifestSha256 { get; }
    internal ulong BundleManifestLengthBytes { get; }
    internal ulong BundleLengthBytes { get; }
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
    private OptimizationExportResult(
        OptimizationExportResultKind kind,
        OptimizationExportReceipt? receipt,
        GgufRuntimeBundleExportReceipt? runtimeBundleReceipt,
        OptimizationExportFailure failure)
    {
        Kind = kind;
        Receipt = receipt;
        RuntimeBundleReceipt = runtimeBundleReceipt;
        Failure = failure;
    }
    internal OptimizationExportResultKind Kind { get; }
    internal OptimizationExportReceipt? Receipt { get; }
    internal GgufRuntimeBundleExportReceipt? RuntimeBundleReceipt { get; }
    internal OptimizationExportFailure Failure { get; }
    internal static OptimizationExportResult Succeeded(OptimizationExportReceipt receipt) =>
        new(OptimizationExportResultKind.Succeeded,
            receipt ?? throw new ArgumentNullException(nameof(receipt)),
            null,
            OptimizationExportFailure.None);
    internal static OptimizationExportResult Succeeded(
        GgufRuntimeBundleExportReceipt receipt) =>
        new(OptimizationExportResultKind.Succeeded,
            null,
            receipt ?? throw new ArgumentNullException(nameof(receipt)),
            OptimizationExportFailure.None);
    internal static OptimizationExportResult Cancelled() =>
        new(OptimizationExportResultKind.Cancelled, null, null,
            OptimizationExportFailure.None);
    internal static OptimizationExportResult Failed(OptimizationExportFailure failure)
    {
        if (failure is OptimizationExportFailure.None || !Enum.IsDefined(failure)) throw new ArgumentOutOfRangeException(nameof(failure));
        return new(OptimizationExportResultKind.Failed, null, null, failure);
    }
}

internal interface IOptimizationExportService
{
    Task<OptimizationExportResult> ExportAsync(VerifiedPersistentExportTarget target, IProgress<OptimizationExportProgress> progress, CancellationToken cancellationToken);
}

internal interface IGgufRuntimeBundleExportService
{
    Task<OptimizationExportResult> ExportAsync(
        VerifiedGgufRuntimeBundleExportTarget target,
        IProgress<OptimizationExportProgress> progress,
        CancellationToken cancellationToken);
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
