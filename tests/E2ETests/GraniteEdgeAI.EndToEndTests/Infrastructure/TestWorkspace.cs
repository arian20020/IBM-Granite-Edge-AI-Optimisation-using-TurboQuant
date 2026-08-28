namespace GraniteEdgeAI.EndToEndTests.Infrastructure;

internal sealed class TestWorkspace : IDisposable
{
    private readonly string root;
    private bool disposed;

    private TestWorkspace(string root, string path)
    {
        this.root = root;
        Path = path;
    }

    internal string Path { get; }

    internal static TestWorkspace Create(string root, string testName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(testName);
        string fullRoot = System.IO.Path.GetFullPath(root).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
        Directory.CreateDirectory(fullRoot);
        string safeName = string.Concat(testName.Select(character => char.IsLetterOrDigit(character) ? character : '_')).Trim('_');
        if (safeName.Length == 0)
        {
            safeName = "test";
        }

        string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(fullRoot, safeName, Guid.NewGuid().ToString("N")));
        if (!path.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Test workspace escaped its configured root.");
        }

        Directory.CreateDirectory(path);
        return new TestWorkspace(fullRoot, path);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        string fullPath = System.IO.Path.GetFullPath(Path);
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) || fullPath.Equals(root.TrimEnd(System.IO.Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Refusing to clean a path outside the owned test workspace.");
        }

        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive: true);
        }
    }
}
