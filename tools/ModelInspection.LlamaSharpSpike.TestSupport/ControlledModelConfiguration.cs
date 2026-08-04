namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;

/// <summary>
/// Resolves and validates the trusted model path without storing it in committed
/// configuration or silently skipping an integration run.
/// </summary>
public sealed record ControlledModelConfiguration
{
    public const string ModelPathEnvironmentVariable =
        "GRANITE_TEST_MODEL_PATH";

    public required string ModelPath { get; init; }

    public required ControlledModelManifest Manifest { get; init; }

    public static ControlledModelConfiguration Load(string manifestPath)
    {
        return Load(
            manifestPath,
            Environment.GetEnvironmentVariable(
                ModelPathEnvironmentVariable));
    }

    public static ControlledModelConfiguration Load(
        string manifestPath,
        string? configuredModelPath)
    {
        ControlledModelManifest manifest =
            ControlledModelManifest.Load(manifestPath);

        if (string.IsNullOrWhiteSpace(configuredModelPath))
        {
            throw new InvalidOperationException(
                $"{ModelPathEnvironmentVariable} is required.");
        }

        string fullPath = Path.GetFullPath(configuredModelPath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                "The controlled Granite model was not found.",
                fullPath);
        }

        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!string.Equals(
            Path.GetFileName(fullPath),
            manifest.FileName,
            comparison))
        {
            throw new InvalidDataException(
                "Configured model filename does not match the controlled manifest.");
        }

        long actualLength = new FileInfo(fullPath).Length;
        if (actualLength != manifest.LengthBytes)
        {
            throw new InvalidDataException(
                $"Configured model length {actualLength} does not match " +
                $"manifest length {manifest.LengthBytes}.");
        }

        return new ControlledModelConfiguration
        {
            ModelPath = fullPath,
            Manifest = manifest
        };
    }

    /// <summary>
    /// Validates the expensive full-file identity once before native tests run.
    /// </summary>
    public async Task ValidateSha256Async(
        CancellationToken cancellationToken)
    {
        string actualHash = await ModelFileHash.ComputeSha256Async(
            ModelPath,
            cancellationToken);

        if (!string.Equals(
            actualHash,
            Manifest.Sha256,
            StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Configured model SHA-256 does not match the controlled manifest.");
        }
    }
}
