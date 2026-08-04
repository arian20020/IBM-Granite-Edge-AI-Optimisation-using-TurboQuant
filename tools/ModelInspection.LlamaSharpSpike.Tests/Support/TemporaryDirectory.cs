namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.Support;

/// <summary>
/// Owns one isolated temporary directory and removes it after a test.
/// </summary>
internal sealed class TemporaryDirectory : IDisposable
{
    internal TemporaryDirectory(string purpose)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "GraniteEdgeAI-LlamaSharpTests",
            purpose,
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(Path);
    }

    internal string Path { get; }

    internal string Combine(params string[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        string result = Path;
        foreach (string part in parts)
        {
            result = System.IO.Path.Combine(result, part);
        }

        return result;
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
