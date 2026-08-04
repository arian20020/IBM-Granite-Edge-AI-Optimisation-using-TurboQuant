namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.Support;

/// <summary>
/// Creates deterministic files inside a test-owned temporary directory.
/// </summary>
internal static class TestFileBuilder
{
    internal static async Task<string> WriteBytesAsync(
        TemporaryDirectory directory,
        string fileName,
        byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(bytes);

        string path = directory.Combine(fileName);
        await File.WriteAllBytesAsync(path, bytes);
        return path;
    }

    internal static async Task<string> WriteTextAsync(
        TemporaryDirectory directory,
        string fileName,
        string text)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(text);

        string path = directory.Combine(fileName);
        await File.WriteAllTextAsync(path, text);
        return path;
    }
}
