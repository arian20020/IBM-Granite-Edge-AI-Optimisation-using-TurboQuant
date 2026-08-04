namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;

/// <summary>
/// Owns a disposable copy of a published feasibility tool so negative native
/// backend tests can remove or corrupt DLLs without changing the real publish.
/// </summary>
public sealed class TemporaryProbeSandbox : IDisposable
{
    private TemporaryProbeSandbox(string directoryPath)
    {
        DirectoryPath = directoryPath;
        ExecutablePath = Path.Combine(
            directoryPath,
            PublishedProbeLocation.ExecutableFileName);
    }

    public string DirectoryPath { get; }

    public string ExecutablePath { get; }

    public static TemporaryProbeSandbox Create(string publishedDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publishedDirectory);

        string sourceDirectory = Path.GetFullPath(publishedDirectory);
        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Published probe directory does not exist: {sourceDirectory}");
        }

        string sandboxDirectory = Path.Combine(
            Path.GetTempPath(),
            "GraniteEdgeAI-LlamaSharpTests",
            "probe-sandboxes",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(sandboxDirectory);

        foreach (string sourceFile in Directory.EnumerateFiles(
            sourceDirectory,
            "*",
            SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(
                sourceDirectory,
                sourceFile);
            string destinationFile = Path.Combine(
                sandboxDirectory,
                relativePath);
            string? destinationDirectory = Path.GetDirectoryName(
                destinationFile);

            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            File.Copy(
                sourceFile,
                destinationFile,
                overwrite: false);
        }

        var sandbox = new TemporaryProbeSandbox(sandboxDirectory);

        if (!File.Exists(sandbox.ExecutablePath))
        {
            sandbox.Dispose();
            throw new FileNotFoundException(
                "Published probe copy did not contain the expected executable.",
                sandbox.ExecutablePath);
        }

        return sandbox;
    }

    public ProbeProcessRequest CreateRequest(
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        IReadOnlyDictionary<string, string?>? environmentVariables = null)
    {
        return new ProbeProcessRequest
        {
            ExecutablePath = ExecutablePath,
            Arguments = arguments,
            WorkingDirectory = DirectoryPath,
            Timeout = timeout,
            EnvironmentVariables = environmentVariables ??
                new Dictionary<string, string?>()
        };
    }

    /// <summary>
    /// Finds only native llama.cpp and ggml libraries. The managed
    /// <c>LLamaSharp.dll</c> assembly is deliberately excluded.
    /// </summary>
    public IReadOnlyList<string> FindNativeDlls()
    {
        return Directory
            .EnumerateFiles(
                DirectoryPath,
                "*.dll",
                SearchOption.AllDirectories)
            .Where(path =>
            {
                string fileName = Path.GetFileName(path);
                bool isLlamaNative = string.Equals(
                        fileName,
                        "llama.dll",
                        StringComparison.OrdinalIgnoreCase) ||
                    fileName.StartsWith(
                        "llama_",
                        StringComparison.OrdinalIgnoreCase) ||
                    fileName.StartsWith(
                        "llama-",
                        StringComparison.OrdinalIgnoreCase);
                bool isGgmlNative = fileName.StartsWith(
                    "ggml",
                    StringComparison.OrdinalIgnoreCase);

                return isLlamaNative || isGgmlNative;
            })
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public void Dispose()
    {
        if (!Directory.Exists(DirectoryPath))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(
            DirectoryPath,
            "*",
            SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(DirectoryPath, recursive: true);
    }
}
