using System.Security.Cryptography;

namespace GraniteEdgeAI.GgufQuantization.WorkerClient;

public sealed class GgufQuantizationFileLease : IDisposable
{
    private bool _disposed;

    private GgufQuantizationFileLease(
        string sourcePath,
        string sourceSha256,
        ulong sourceLengthBytes,
        string outputPath)
    {
        SourcePath = sourcePath;
        SourceSha256 = sourceSha256;
        SourceLengthBytes = sourceLengthBytes;
        OutputPath = outputPath;
    }

    internal string SourcePath { get; }
    internal string SourceSha256 { get; }
    internal ulong SourceLengthBytes { get; }
    internal string OutputPath { get; }

    public static GgufQuantizationFileLease Create(
        string sourcePath,
        string expectedSourceSha256,
        ulong expectedSourceLengthBytes,
        string outputPath)
    {
        if (!IsDigest(expectedSourceSha256) || expectedSourceLengthBytes == 0)
        {
            throw new ArgumentException("A sealed source identity is required.");
        }
        string source = Path.GetFullPath(sourcePath);
        string output = Path.GetFullPath(outputPath);
        RequireRegularFile(source);
        if (File.Exists(output) || Directory.Exists(output))
        {
            throw new IOException("The quantizer output must not already exist.");
        }
        string outputDirectory = Path.GetDirectoryName(output)
            ?? throw new ArgumentException("The output has no parent directory.", nameof(outputPath));
        var outputRoot = new DirectoryInfo(outputDirectory);
        if (!outputRoot.Exists || IsReparse(outputRoot))
        {
            throw new IOException("The output parent is unavailable or redirected.");
        }
        if (!string.Equals(Path.GetPathRoot(source), Path.GetPathRoot(output), StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("Source and output must use the same local volume.");
        }
        VerifySource(source, expectedSourceSha256, expectedSourceLengthBytes);
        return new GgufQuantizationFileLease(source, expectedSourceSha256, expectedSourceLengthBytes, output);
    }

    internal void Validate()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        VerifySource(SourcePath, SourceSha256, SourceLengthBytes);
        if (File.Exists(OutputPath) || Directory.Exists(OutputPath))
        {
            throw new IOException("The quantizer output is no longer unused.");
        }
    }

    internal void VerifySourceUnchanged()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        VerifySource(SourcePath, SourceSha256, SourceLengthBytes);
    }

    internal void DeletePendingOutput()
    {
        if (File.Exists(OutputPath))
        {
            File.SetAttributes(OutputPath, FileAttributes.Normal);
            File.Delete(OutputPath);
        }
    }

    public void Dispose() => _disposed = true;

    private static void VerifySource(string path, string expectedSha256, ulong expectedLength)
    {
        RequireRegularFile(path);
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        string digest = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if ((ulong)stream.Length != expectedLength || !string.Equals(digest, expectedSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The sealed quantization source changed.");
        }
    }

    private static void RequireRegularFile(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || IsReparse(info))
        {
            throw new FileNotFoundException("The sealed quantization source is unavailable.");
        }
    }

    private static bool IsReparse(FileSystemInfo info) =>
        (info.Attributes & FileAttributes.ReparsePoint) != 0;

    private static bool IsDigest(string? value) =>
        value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
