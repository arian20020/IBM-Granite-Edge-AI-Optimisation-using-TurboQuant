using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace GraniteEdgeAI.EndToEndTests.Infrastructure;

internal static partial class GitEvidenceVerifier
{
    private const int MaximumEvidenceBytes = 1024 * 1024;
    private const int MaximumTextBytes = 64 * 1024;
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(10);

    internal static void VerifyBlob(
        string repositoryRoot,
        string subjectCommit,
        string subjectTree,
        string relativePath,
        string sha256,
        long bytes,
        CancellationToken cancellationToken = default)
    {
        string root = RequireRepository(repositoryRoot);
        JsonContract.RequireGitObject(subjectCommit, nameof(subjectCommit));
        JsonContract.RequireGitObject(subjectTree, nameof(subjectTree));
        JsonContract.RequireSha256(sha256, nameof(sha256));
        string path = RequireRelativeGitPath(relativePath);
        if (bytes <= 0 || bytes > MaximumEvidenceBytes)
        {
            throw new InvalidDataException("Git blob byte count violates the closed evidence bound.");
        }

        string actualTree = RunText(root, ["rev-parse", $"{subjectCommit}^{{tree}}"], cancellationToken).Trim();
        if (!string.Equals(actualTree, subjectTree, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Evidence subject tree does not match its commit.");
        }

        string objectSpec = $"{subjectCommit}:{path}";
        string objectType = RunText(root, ["cat-file", "-t", objectSpec], cancellationToken).Trim();
        if (!string.Equals(objectType, "blob", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Evidence path is not a Git blob at the subject commit.");
        }

        byte[] blob = RunGit(root, ["cat-file", "blob", objectSpec], MaximumEvidenceBytes + 1, cancellationToken);
        string actualSha = Convert.ToHexString(SHA256.HashData(blob)).ToLowerInvariant();
        if (blob.LongLength != bytes || !string.Equals(actualSha, sha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Git blob bytes or SHA-256 do not match committed evidence.");
        }
    }

    internal static void VerifyPushedRef(
        string repositoryRoot,
        string remote,
        string remoteRef,
        string expectedCommit,
        CancellationToken cancellationToken = default)
    {
        string root = RequireRepository(repositoryRoot);
        JsonContract.RequireGitObject(expectedCommit, nameof(expectedCommit));
        if (!RemoteName().IsMatch(remote))
        {
            throw new InvalidDataException("Git remote name is invalid.");
        }
        if (!remoteRef.StartsWith("refs/heads/", StringComparison.Ordinal)
            || !RemoteRef().IsMatch(remoteRef)
            || remoteRef.Contains("..", StringComparison.Ordinal)
            || remoteRef.Contains("@{", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Pushed remote ref is invalid.");
        }

        RunText(root, ["cat-file", "-e", $"{expectedCommit}^{{commit}}"], cancellationToken);
        string[] lines = RunText(root, ["ls-remote", "--exit-code", remote, remoteRef], cancellationToken)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string expected = $"{expectedCommit}\t{remoteRef}";
        if (lines.Length != 1 || !string.Equals(lines[0], expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Expected commit is not the exact pushed remote ref.");
        }
    }

    private static string RequireRepository(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string root = Path.GetFullPath(path);
        if (!Directory.Exists(root))
        {
            throw new InvalidDataException("Git repository root is unavailable.");
        }
        return root;
    }

    private static string RequireRelativeGitPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (Path.IsPathFullyQualified(path)
            || path.Contains('\\')
            || path.Contains(':')
            || path.StartsWith("-", StringComparison.Ordinal)
            || path.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new InvalidDataException("Git evidence path must be repository-relative and non-escaping.");
        }
        return path;
    }

    private static string RunText(string root, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        byte[] output = RunGit(root, arguments, MaximumTextBytes, cancellationToken);
        return new UTF8Encoding(false, true).GetString(output);
    }

    private static byte[] RunGit(
        string root,
        IReadOnlyList<string> arguments,
        int maximumOutputBytes,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(CommandTimeout);
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git.exe",
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        try
        {
            process.Start();
            Task<byte[]> output = ReadBoundedAsync(process.StandardOutput.BaseStream, maximumOutputBytes, timeout.Token);
            Task<string> error = process.StandardError.ReadToEndAsync(timeout.Token);
            Task wait = process.WaitForExitAsync(timeout.Token);
            Task.WhenAll(output, error, wait).GetAwaiter().GetResult();
            if (process.ExitCode != 0)
            {
                throw new InvalidDataException($"Git evidence command failed with exit code {process.ExitCode}.");
            }
            return output.Result;
        }
        catch (OperationCanceledException error)
        {
            TryKill(process);
            throw new InvalidDataException("Git evidence command was cancelled or timed out.", error);
        }
        catch (InvalidDataException)
        {
            TryKill(process);
            throw;
        }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            TryKill(process);
            throw new InvalidDataException("Git evidence command could not be executed.", error);
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream stream, int maximumBytes, CancellationToken cancellationToken)
    {
        using MemoryStream output = new(Math.Min(maximumBytes, 16 * 1024));
        byte[] buffer = new byte[8192];
        while (true)
        {
            int read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return output.ToArray();
            }
            if (output.Length + read > maximumBytes)
            {
                throw new InvalidDataException("Git evidence command exceeded its output bound.");
            }
            output.Write(buffer, 0, read);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(2000);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    [GeneratedRegex("^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex RemoteName();

    [GeneratedRegex("^refs/heads/[A-Za-z0-9._/-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex RemoteRef();
}
