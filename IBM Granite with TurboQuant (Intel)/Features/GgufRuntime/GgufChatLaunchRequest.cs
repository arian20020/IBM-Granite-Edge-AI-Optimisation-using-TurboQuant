using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.GgufRuntime.Capabilities.Manifest;
using GraniteEdgeAI.GgufRuntime.Contracts.Configuration;

namespace GraniteEdgeAI.Features.GgufRuntime;

internal sealed class GgufChatLaunchRequest
{
    private readonly byte[] trustedManifest;

    internal GgufChatLaunchRequest(
        string packageRoot,
        ReadOnlySpan<byte> trustedManifest,
        string modelFile,
        string displayName,
        GgufRuntimeConfiguration configuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(configuration);
        if (trustedManifest.IsEmpty ||
            trustedManifest.Length > GgufRuntimeManifestJson.MaximumManifestBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(trustedManifest));
        }

        GgufRuntimeManifest manifest =
            GgufRuntimeManifestJson.Deserialize(trustedManifest);
        if (!string.Equals(
                configuration.RuntimeBuildId,
                manifest.RuntimeBuildId,
                StringComparison.Ordinal) ||
            !string.Equals(
                configuration.RuntimeSourceCommit,
                manifest.RuntimeSourceCommit,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The approved configuration does not match the trusted runtime manifest.",
                nameof(configuration));
        }

        if (configuration.Backend != GgufRuntimeBackend.Cpu ||
            !string.Equals(
                configuration.DeviceId,
                "cpu",
                StringComparison.OrdinalIgnoreCase) ||
            configuration.GpuLayerCount != 0)
        {
            throw new ArgumentException(
                "The current GGUF chat release accepts only the approved CPU profile.",
                nameof(configuration));
        }

        PackageRoot = RequireExistingAbsoluteDirectory(
            packageRoot,
            nameof(packageRoot));
        ModelFile = RequireExistingAbsoluteFile(modelFile, nameof(modelFile));
        DisplayName = displayName.Trim();
        Configuration = configuration;
        this.trustedManifest = trustedManifest.ToArray();
    }

    internal string PackageRoot { get; }

    internal ReadOnlyMemory<byte> TrustedManifest =>
        (byte[])trustedManifest.Clone();

    internal string ModelFile { get; }

    internal string DisplayName { get; }

    internal GgufRuntimeConfiguration Configuration { get; }

    internal async Task VerifyModelAsync(CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            ModelFile,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] actual = await SHA256.HashDataAsync(stream, cancellationToken)
            .ConfigureAwait(false);
        byte[] expected = Convert.FromHexString(Configuration.ModelSha256);
        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
        {
            throw new GgufChatLaunchException(
                "model-changed-since-inspection");
        }
    }

    private static string RequireExistingAbsoluteDirectory(
        string path,
        string parameterName)
    {
        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException(
                "A fully qualified runtime package directory is required.",
                parameterName);
        }

        string fullPath = Path.GetFullPath(path);
        return Directory.Exists(fullPath)
            ? fullPath
            : throw new DirectoryNotFoundException(
                "The runtime package directory does not exist.");
    }

    private static string RequireExistingAbsoluteFile(
        string path,
        string parameterName)
    {
        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException(
                "A fully qualified local model file is required.",
                parameterName);
        }

        string fullPath = Path.GetFullPath(path);
        return File.Exists(fullPath)
            ? fullPath
            : throw new GgufChatLaunchException("model-file-missing");
    }
}

internal sealed class GgufChatLaunchException : Exception
{
    internal GgufChatLaunchException(string code)
        : base("The local GGUF chat launch request was rejected.")
    {
        Code = code;
    }

    internal string Code { get; }
}
