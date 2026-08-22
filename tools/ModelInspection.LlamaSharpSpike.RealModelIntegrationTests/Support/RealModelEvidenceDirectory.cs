namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.Support;

/// <summary>
/// Owns an isolated evidence root. Local runs clean temporary evidence; trusted
/// CI preserves evidence only when an explicit artifact root is configured.
/// </summary>
internal sealed class RealModelEvidenceDirectory : IDisposable
{
    internal const string RetainedRootEnvironmentVariable =
        "LLAMASHARP_REAL_MODEL_EVIDENCE_DIR";

    private readonly bool _deleteOnDispose;

    internal RealModelEvidenceDirectory()
    {
        string? retainedRoot = Environment.GetEnvironmentVariable(
            RetainedRootEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(retainedRoot))
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "GraniteEdgeAI-LlamaSharpTests",
                "real-model-evidence",
                Guid.NewGuid().ToString("N"));
            _deleteOnDispose = true;
        }
        else
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetFullPath(retainedRoot),
                Guid.NewGuid().ToString("N"));
            _deleteOnDispose = false;
        }

        Directory.CreateDirectory(Path);
    }

    internal string Path { get; }

    internal string CreateFile(
        string scenario,
        string fileName = "result.json")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        string directory = System.IO.Path.Combine(
            Path,
            scenario,
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        return System.IO.Path.Combine(directory, fileName);
    }

    public void Dispose()
    {
        if (_deleteOnDispose && Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
