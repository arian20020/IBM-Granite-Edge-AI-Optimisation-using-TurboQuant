namespace GraniteEdgeAI.EndToEndTests.Tests;

internal sealed class TestDirectory : IDisposable
{
    private TestDirectory(string path) => Path = path;

    internal string Path { get; }

    internal static TestDirectory Create()
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "GraniteE1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return new TestDirectory(path);
    }

    internal string WriteText(string name, string content)
    {
        string path = System.IO.Path.Combine(Path, name);
        File.WriteAllText(path, content);
        return path;
    }

    internal string WriteBytes(string name, byte[] content)
    {
        string path = System.IO.Path.Combine(Path, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            foreach (string file in Directory.EnumerateFiles(Path, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = false,
                AttributesToSkip = FileAttributes.ReparsePoint,
            }))
            {
                FileAttributes attributes = File.GetAttributes(file);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                {
                    File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
                }
            }
            Directory.Delete(Path, recursive: true);
        }
    }
}
