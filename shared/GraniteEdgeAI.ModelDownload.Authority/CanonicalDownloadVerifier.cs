using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.Features.ModelImport.ModelDownload;

internal enum DownloadCompletionStatus
{
    Completed,
    Failed,
    Cancelled,
    Retired,
    Stale
}

internal sealed record DownloadVerificationContext(
    OptimizationPreferenceBand Band,
    Guid OperationId,
    long Generation,
    string PublicationIdentity,
    Guid InspectionRequestId,
    long LifecycleGeneration);

internal sealed record DownloadedArtifactEvidence(
    DownloadCompletionStatus Status,
    OptimizationPreferenceBand Band,
    string RepositoryId,
    string Revision,
    string FileName,
    long ObservedByteLength,
    string ObservedSha256,
    Guid OperationId,
    long Generation,
    string PublicationIdentity,
    Guid InspectionRequestId,
    long LifecycleGeneration);

internal static class CanonicalDownloadVerifier
{
    internal static bool IsExactMatch(
        DownloadVerificationContext expected,
        DownloadedArtifactEvidence observed)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(observed);
        ModelDownloadCatalogEntry canonical;
        try
        {
            canonical = PinnedGraniteModelCatalog.ForPreferenceBand(expected.Band);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        return observed.Status == DownloadCompletionStatus.Completed
            && expected.OperationId != Guid.Empty
            && expected.Generation >= 0
            && !string.IsNullOrWhiteSpace(expected.PublicationIdentity)
            && expected.InspectionRequestId != Guid.Empty
            && expected.LifecycleGeneration >= 0
            && observed.Band == expected.Band
            && string.Equals(observed.RepositoryId, canonical.RepositoryId, StringComparison.Ordinal)
            && string.Equals(observed.Revision, canonical.Revision, StringComparison.Ordinal)
            && string.Equals(observed.FileName, canonical.FileName, StringComparison.Ordinal)
            && observed.ObservedByteLength == canonical.ExpectedByteLength
            && string.Equals(observed.ObservedSha256, canonical.ExpectedSha256, StringComparison.Ordinal)
            && observed.OperationId == expected.OperationId
            && observed.Generation == expected.Generation
            && string.Equals(observed.PublicationIdentity,
                expected.PublicationIdentity, StringComparison.Ordinal)
            && observed.InspectionRequestId == expected.InspectionRequestId
            && observed.LifecycleGeneration == expected.LifecycleGeneration;
    }
}
