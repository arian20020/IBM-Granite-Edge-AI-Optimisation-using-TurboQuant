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
        string outputIdentity,
        string outputManifestSha256,
        ulong outputLengthBytes)
    {
        if (!Enum.IsDefined(route))
        {
            throw new ArgumentOutOfRangeException(nameof(route));
        }
        if (optimizationPlanId == Guid.Empty)
        {
            throw new ArgumentException(
                "A verified export target requires its optimization plan identity.",
                nameof(optimizationPlanId));
        }
        if (outputLengthBytes == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(outputLengthBytes),
                "A persistent export target cannot be empty.");
        }

        Route = route;
        OptimizationPlanId = optimizationPlanId;
        ConfigurationSha256 = OptimizationExportContractGuard.RequireSha256(
            configurationSha256, nameof(configurationSha256));
        OutputIdentity = OptimizationExportContractGuard.RequireOpaqueId(
            outputIdentity, nameof(outputIdentity));
        OutputManifestSha256 = OptimizationExportContractGuard.RequireSha256(
            outputManifestSha256, nameof(outputManifestSha256));
        OutputLengthBytes = outputLengthBytes;
    }

    internal OptimizationRoute Route { get; }
    internal Guid OptimizationPlanId { get; }
    internal string ConfigurationSha256 { get; }
    internal string OutputIdentity { get; }
    internal string OutputManifestSha256 { get; }
    internal ulong OutputLengthBytes { get; }
}

internal sealed record OptimizationExportReceipt
{
    internal OptimizationExportReceipt(string sha256, ulong lengthBytes)
    {
        Sha256 = OptimizationExportContractGuard.RequireSha256(
            sha256, nameof(sha256));
        if (lengthBytes == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lengthBytes));
        }
        LengthBytes = lengthBytes;
    }

    internal string Sha256 { get; }
    internal ulong LengthBytes { get; }
}

internal enum OptimizationExportStage
{
    ChoosingDestination,
    Writing,
    Verifying,
    Publishing,
    CleaningUp
}

internal sealed record OptimizationExportProgress
{
    internal OptimizationExportProgress(
        OptimizationExportStage stage,
        double? fraction = null)
    {
        if (!Enum.IsDefined(stage))
        {
            throw new ArgumentOutOfRangeException(nameof(stage));
        }
        if (fraction is < 0 or > 1 || double.IsNaN(fraction ?? 0))
        {
            throw new ArgumentOutOfRangeException(nameof(fraction));
        }
        Stage = stage;
        Fraction = fraction;
    }

    internal OptimizationExportStage Stage { get; }
    internal double? Fraction { get; }
}

internal enum OptimizationExportFailure
{
    None,
    IntegrityMismatch,
    InsufficientSpace,
    DestinationUnavailable,
    PublicationFailure,
    CleanupFailure
}

internal enum OptimizationExportResultKind
{
    Succeeded,
    Cancelled,
    Failed
}

internal sealed record OptimizationExportResult
{
    private OptimizationExportResult(
        OptimizationExportResultKind kind,
        OptimizationExportReceipt? receipt,
        OptimizationExportFailure failure)
    {
        Kind = kind;
        Receipt = receipt;
        Failure = failure;
    }

    internal OptimizationExportResultKind Kind { get; }
    internal OptimizationExportReceipt? Receipt { get; }
    internal OptimizationExportFailure Failure { get; }

    internal static OptimizationExportResult Succeeded(
        OptimizationExportReceipt receipt) =>
        new(
            OptimizationExportResultKind.Succeeded,
            receipt ?? throw new ArgumentNullException(nameof(receipt)),
            OptimizationExportFailure.None);

    internal static OptimizationExportResult Cancelled() =>
        new(
            OptimizationExportResultKind.Cancelled,
            receipt: null,
            OptimizationExportFailure.None);

    internal static OptimizationExportResult Failed(
        OptimizationExportFailure failure)
    {
        if (failure is OptimizationExportFailure.None || !Enum.IsDefined(failure))
        {
            throw new ArgumentOutOfRangeException(nameof(failure));
        }
        return new(
            OptimizationExportResultKind.Failed,
            receipt: null,
            failure);
    }
}

internal interface IOptimizationExportService
{
    Task<OptimizationExportResult> ExportAsync(
        VerifiedPersistentExportTarget target,
        IProgress<OptimizationExportProgress> progress,
        CancellationToken cancellationToken);
}

internal static class OptimizationExportContractGuard
{
    internal static string RequireSha256(string value, string parameter)
    {
        if (value is null
            || value.Length != 64
            || value.Any(character => character is not (>= '0' and <= '9')
                and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException(
                "The value must be a canonical lowercase SHA-256 digest.",
                parameter);
        }
        return value;
    }

    internal static string RequireOpaqueId(string value, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 128
            || value.Any(char.IsControl)
            || value.Contains('/')
            || value.Contains('\\')
            || value.Contains(':')
            || value.Contains('?')
            || value.Contains('#'))
        {
            throw new ArgumentException(
                "The value must be a bounded opaque identity.", parameter);
        }
        return value;
    }
}
