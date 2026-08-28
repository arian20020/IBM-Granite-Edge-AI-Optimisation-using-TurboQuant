using GraniteEdgeAI.Features.ModelImport.Selection;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelImport.ModelDownload;

internal sealed record RecommendedModelOffer
{
    internal RecommendedModelOffer(
        string offerId,
        string displayName,
        string packageSummary,
        string description,
        string format,
        string optimization,
        string estimatedMemory,
        string contextLength)
    {
        OfferId = ModelDownloadContractGuard.RequireOpaqueId(
            offerId, nameof(offerId));
        DisplayName = ModelDownloadContractGuard.RequireDisplayText(
            displayName, nameof(displayName));
        PackageSummary = ModelDownloadContractGuard.RequireDisplayText(
            packageSummary, nameof(packageSummary));
        Description = ModelDownloadContractGuard.RequireDisplayText(
            description, nameof(description));
        Format = ModelDownloadContractGuard.RequireDisplayText(
            format, nameof(format));
        Optimization = ModelDownloadContractGuard.RequireDisplayText(
            optimization, nameof(optimization));
        EstimatedMemory = ModelDownloadContractGuard.RequireDisplayText(
            estimatedMemory, nameof(estimatedMemory));
        ContextLength = ModelDownloadContractGuard.RequireDisplayText(
            contextLength, nameof(contextLength));
    }

    internal string OfferId { get; }
    internal string DisplayName { get; }
    internal string PackageSummary { get; }
    internal string Description { get; }
    internal string Format { get; }
    internal string Optimization { get; }
    internal string EstimatedMemory { get; }
    internal string ContextLength { get; }
}

internal sealed record RecommendedModelDownloadRequest
{
    internal RecommendedModelDownloadRequest(
        Guid operationId,
        string offerId,
        int preferenceValue)
    {
        if (operationId == Guid.Empty)
        {
            throw new ArgumentException(
                "A download operation requires a unique identity.",
                nameof(operationId));
        }
        if (preferenceValue is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(preferenceValue));
        }

        OperationId = operationId;
        OfferId = ModelDownloadContractGuard.RequireOpaqueId(
            offerId, nameof(offerId));
        PreferenceValue = preferenceValue;
    }

    internal Guid OperationId { get; }
    internal string OfferId { get; }
    internal int PreferenceValue { get; }
}

internal sealed record CompletedModelDownload
{
    internal CompletedModelDownload(
        ModelSelectionInput selection,
        string sha256,
        ulong lengthBytes,
        string publicationId)
    {
        Selection = selection ?? throw new ArgumentNullException(nameof(selection));
        Sha256 = ModelDownloadContractGuard.RequireSha256(sha256, nameof(sha256));
        if (lengthBytes == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lengthBytes),
                "A completed model download cannot be empty.");
        }
        LengthBytes = lengthBytes;
        PublicationId = ModelDownloadContractGuard.RequireOpaqueId(
            publicationId, nameof(publicationId));
    }

    internal ModelSelectionInput Selection { get; }
    internal string Sha256 { get; }
    internal ulong LengthBytes { get; }
    internal string PublicationId { get; }
}

internal enum ModelDownloadStage
{
    Resolving,
    Transferring,
    Verifying,
    Publishing,
    CleaningUp
}

internal sealed record ModelDownloadProgress
{
    internal ModelDownloadProgress(
        ModelDownloadStage stage,
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

    internal ModelDownloadStage Stage { get; }
    internal double? Fraction { get; }
}

internal enum ModelDownloadFailure
{
    None,
    IntegrityMismatch,
    InsufficientSpace,
    PublicationFailure,
    CleanupFailure
}

internal enum ModelDownloadResultKind
{
    Succeeded,
    Cancelled,
    Failed
}

internal sealed record ModelDownloadResult
{
    private ModelDownloadResult(
        ModelDownloadResultKind kind,
        CompletedModelDownload? completedDownload,
        ModelDownloadFailure failure)
    {
        Kind = kind;
        CompletedDownload = completedDownload;
        Failure = failure;
    }

    internal ModelDownloadResultKind Kind { get; }
    internal CompletedModelDownload? CompletedDownload { get; }
    internal ModelDownloadFailure Failure { get; }

    internal static ModelDownloadResult Succeeded(
        CompletedModelDownload completedDownload) =>
        new(
            ModelDownloadResultKind.Succeeded,
            completedDownload
                ?? throw new ArgumentNullException(nameof(completedDownload)),
            ModelDownloadFailure.None);

    internal static ModelDownloadResult Cancelled() =>
        new(
            ModelDownloadResultKind.Cancelled,
            completedDownload: null,
            ModelDownloadFailure.None);

    internal static ModelDownloadResult Failed(ModelDownloadFailure failure)
    {
        if (failure is ModelDownloadFailure.None || !Enum.IsDefined(failure))
        {
            throw new ArgumentOutOfRangeException(nameof(failure));
        }
        return new(
            ModelDownloadResultKind.Failed,
            completedDownload: null,
            failure);
    }
}

internal interface IRecommendedModelDownloadService
{
    Task<ModelDownloadResult> DownloadAsync(
        RecommendedModelDownloadRequest request,
        IProgress<ModelDownloadProgress> progress,
        CancellationToken cancellationToken);
}

internal static class ModelDownloadContractGuard
{
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

    internal static string RequireDisplayText(string value, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 160
            || value.Any(char.IsControl)
            || value.Contains('/')
            || value.Contains('\\')
            || value.Contains("http", StringComparison.OrdinalIgnoreCase)
            || value.Contains("token=", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Approved model display text must be bounded and path-free.",
                parameter);
        }
        return value;
    }
}
