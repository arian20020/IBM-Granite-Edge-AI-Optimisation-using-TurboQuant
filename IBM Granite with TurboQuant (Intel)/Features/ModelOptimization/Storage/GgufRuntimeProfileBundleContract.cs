using System;

namespace GraniteEdgeAI.Features.ModelOptimization.Storage;

internal enum GgufRuntimeProfileBundleExportDisposition
{
    Succeeded,
    ResultRejected,
    DestinationRejected,
    DestinationExists,
    Oversized,
    SourceUnavailable,
    SourceChanged,
    CleanupFailed,
    Failed,
}

internal enum GgufRuntimeProfileBundleExportStage
{
    CopyingModel,
    WritingProfile,
    WritingManifest,
    Verifying,
    Publishing,
}

internal sealed class GgufRuntimeProfileBundleCleanupException
    : InvalidOperationException
{
    internal GgufRuntimeProfileBundleCleanupException()
        : base("Runtime-profile bundle cleanup integrity could not be verified.")
    {
    }
}

internal sealed record GgufRuntimeProfileBundleExportResult
{
    private GgufRuntimeProfileBundleExportResult(
        GgufRuntimeProfileBundleExportDisposition disposition,
        string? bundleManifestSha256,
        ulong bundleManifestLengthBytes,
        ulong bundleLengthBytes)
    {
        Disposition = disposition;
        BundleManifestSha256 = bundleManifestSha256;
        BundleManifestLengthBytes = bundleManifestLengthBytes;
        BundleLengthBytes = bundleLengthBytes;
    }

    internal GgufRuntimeProfileBundleExportDisposition Disposition { get; }

    internal string? BundleManifestSha256 { get; }

    internal ulong BundleManifestLengthBytes { get; }

    internal ulong BundleLengthBytes { get; }

    internal static GgufRuntimeProfileBundleExportResult Succeeded(
        string bundleManifestSha256,
        ulong bundleManifestLengthBytes,
        ulong bundleLengthBytes)
    {
        if (!StagedSourceSnapshot.IsCanonicalSha256(bundleManifestSha256))
        {
            throw new ArgumentException(
                "A completed bundle requires its canonical manifest digest.",
                nameof(bundleManifestSha256));
        }
        if (bundleManifestLengthBytes == 0
            || bundleLengthBytes < bundleManifestLengthBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bundleLengthBytes),
                "A completed bundle requires non-empty, coherent lengths.");
        }
        return new(
            GgufRuntimeProfileBundleExportDisposition.Succeeded,
            bundleManifestSha256,
            bundleManifestLengthBytes,
            bundleLengthBytes);
    }

    internal static GgufRuntimeProfileBundleExportResult For(
        GgufRuntimeProfileBundleExportDisposition disposition)
    {
        if (disposition == GgufRuntimeProfileBundleExportDisposition.Succeeded
            || !Enum.IsDefined(disposition))
        {
            throw new ArgumentOutOfRangeException(nameof(disposition));
        }
        return new(disposition, null, 0, 0);
    }
}
