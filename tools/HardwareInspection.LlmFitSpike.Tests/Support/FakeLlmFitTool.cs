using System.ComponentModel;
using System.Diagnostics;
using HardwareInspection.LlmFitSpike.Command;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HardwareInspection.LlmFitSpike.Tests.Support;

internal sealed class FakeLlmFitTool : IAsyncDisposable
{
    private const string ControlledRootEnvironmentVariable = "GRANITE_LLMFIT_FAKE_TOOL_ROOT";
    private const string ExecutableName = "GraniteEdgeAI.HardwareInspection.LlmFitFakeTool.exe";
    private const string ProjectRelativePath =
        "tests/ProcessFixtures/GraniteEdgeAI.HardwareInspection.LlmFitFakeTool/" +
        "GraniteEdgeAI.HardwareInspection.LlmFitFakeTool.csproj";
    private const string PublishDirectoryPrefix = "GraniteEdgeAI-LlmFit-Publish-";
    private const string TestDirectoryPrefix = "GraniteEdgeAI-LlmFit-Test-";
    private static readonly string[] SystemArguments = ["--no-dashboard", "--json", "system"];
    private static readonly string[] VersionArguments = ["--version"];
    private static readonly Lazy<Task<SharedFixture>> SharedFixtureTask = new(
        ResolveOrPublishAsync,
        LazyThreadSafetyMode.ExecutionAndPublication);
    private static int _sharedCleanupStarted;

    private FakeLlmFitTool(string root)
    {
        Root = root;
        ExecutablePath = Path.Combine(root, ExecutableName);
    }

    internal string Root { get; }

    internal string ExecutablePath { get; }

    internal LlmFitCommand SystemCommand => new(ExecutablePath, Root, SystemArguments.ToArray());

    internal LlmFitCommand VersionCommand => new(ExecutablePath, Root, VersionArguments.ToArray());

    internal static async Task<FakeLlmFitTool> CreateAsync(string mode)
    {
        if (mode is not (
            "success" or
            "nonzero" or
            "large-output" or
            "sleep" or
            "spawn-child" or
            "sleep-child" or
            "dashboard"))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        SharedFixture shared = await SharedFixtureTask.Value.ConfigureAwait(false);
        string testRoot = CreateOwnedDirectory(TestDirectoryPrefix);

        try
        {
            CopyWithoutReparsePoints(shared.Root, testRoot);
            await File.WriteAllTextAsync(Path.Combine(testRoot, "fake-mode.txt"), mode)
                .ConfigureAwait(false);
            return new FakeLlmFitTool(testRoot);
        }
        catch
        {
            await DeleteOwnedDirectoryAsync(testRoot, TestDirectoryPrefix).ConfigureAwait(false);
            throw;
        }
    }

    internal static async Task CleanupSharedAsync()
    {
        if (Interlocked.Exchange(ref _sharedCleanupStarted, 1) != 0 || !SharedFixtureTask.IsValueCreated)
        {
            return;
        }

        SharedFixture shared;
        try
        {
            shared = await SharedFixtureTask.Value.ConfigureAwait(false);
        }
        catch
        {
            return;
        }

        if (shared.OwnsRoot)
        {
            await DeleteOwnedDirectoryAsync(shared.Root, PublishDirectoryPrefix).ConfigureAwait(false);
        }
    }

    public ValueTask DisposeAsync()
    {
        return new ValueTask(DeleteOwnedDirectoryAsync(Root, TestDirectoryPrefix));
    }

    private static async Task<SharedFixture> ResolveOrPublishAsync()
    {
        string? controlledRoot = TryGetControlledRoot();
        if (controlledRoot is not null)
        {
            return new SharedFixture(controlledRoot, OwnsRoot: false);
        }

        return await PublishAsync().ConfigureAwait(false);
    }

    private static string? TryGetControlledRoot()
    {
        string? configuredRoot = Environment.GetEnvironmentVariable(ControlledRootEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configuredRoot))
        {
            return null;
        }

        try
        {
            string root = Path.GetFullPath(configuredRoot);
            return File.Exists(Path.Combine(root, ExecutableName)) ? root : null;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            IOException or
            NotSupportedException or
            UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static async Task<SharedFixture> PublishAsync()
    {
        string outputRoot = CreateOwnedDirectory(PublishDirectoryPrefix);

        try
        {
            string repositoryRoot = FindRepositoryRoot();
            string projectPath = Path.Combine(
                repositoryRoot,
                ProjectRelativePath.Replace('/', Path.DirectorySeparatorChar));
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = repositoryRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("publish");
            startInfo.ArgumentList.Add(projectPath);
            startInfo.ArgumentList.Add("--configuration");
            startInfo.ArgumentList.Add("Release");
            startInfo.ArgumentList.Add("--no-restore");
            startInfo.ArgumentList.Add("--runtime");
            startInfo.ArgumentList.Add("win-x64");
            startInfo.ArgumentList.Add("--self-contained");
            startInfo.ArgumentList.Add("false");
            startInfo.ArgumentList.Add("--output");
            startInfo.ArgumentList.Add(outputRoot);
            startInfo.ArgumentList.Add("-p:UseAppHost=true");

            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                throw new InvalidOperationException();
            }

            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
            Task<string> standardError = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));

            try
            {
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                TryKillProcessTree(process);
                await process.WaitForExitAsync().ConfigureAwait(false);
                await Task.WhenAll(standardOutput, standardError).ConfigureAwait(false);
                throw new TimeoutException("Publishing the LLM Fit fake tool exceeded two minutes.");
            }

            await Task.WhenAll(standardOutput, standardError).ConfigureAwait(false);
            if (process.ExitCode != 0 || !File.Exists(Path.Combine(outputRoot, ExecutableName)))
            {
                throw new InvalidOperationException();
            }

            return new SharedFixture(outputRoot, OwnsRoot: true);
        }
        catch (TimeoutException)
        {
            await DeleteOwnedDirectoryAsync(outputRoot, PublishDirectoryPrefix).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception) when (
            exception is Win32Exception or
            InvalidOperationException or
            IOException or
            UnauthorizedAccessException)
        {
            await DeleteOwnedDirectoryAsync(outputRoot, PublishDirectoryPrefix).ConfigureAwait(false);
            throw new InvalidOperationException("The LLM Fit fake tool could not be published.");
        }
    }

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The publish process exited between the state check and the kill.
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "global.json")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("The repository root could not be resolved.");
    }

    private static string CreateOwnedDirectory(string prefix)
    {
        string path = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CopyWithoutReparsePoints(string sourceRoot, string destinationRoot)
    {
        EnsureNotReparsePoint(sourceRoot);
        var pending = new Stack<(string Source, string Destination)>();
        pending.Push((sourceRoot, destinationRoot));

        while (pending.TryPop(out (string Source, string Destination) directory))
        {
            foreach (string sourceEntry in Directory.EnumerateFileSystemEntries(directory.Source))
            {
                EnsureNotReparsePoint(sourceEntry);
                string destinationEntry = Path.Combine(directory.Destination, Path.GetFileName(sourceEntry));
                FileAttributes attributes = File.GetAttributes(sourceEntry);
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    Directory.CreateDirectory(destinationEntry);
                    pending.Push((sourceEntry, destinationEntry));
                }
                else
                {
                    File.Copy(sourceEntry, destinationEntry, overwrite: false);
                }
            }
        }
    }

    private static void EnsureNotReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("The fake tool publish contains unsupported linked content.");
        }
    }

    private static async Task DeleteOwnedDirectoryAsync(string path, string requiredPrefix)
    {
        if (!IsProvenOwnedDirectory(path, requiredPrefix))
        {
            return;
        }

        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    return;
                }

                EnsureTreeHasNoReparsePoints(path);
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);
            }
        }
    }

    private static bool IsProvenOwnedDirectory(string path, string requiredPrefix)
    {
        try
        {
            string fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            string tempPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            string? parent = Directory.GetParent(fullPath)?.FullName;
            string name = Path.GetFileName(fullPath);
            return string.Equals(parent, tempPath, StringComparison.OrdinalIgnoreCase) &&
                name.StartsWith(requiredPrefix, StringComparison.Ordinal) &&
                Guid.TryParseExact(name[requiredPrefix.Length..], "N", out _);
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or NotSupportedException)
        {
            return false;
        }
    }

    private static void EnsureTreeHasNoReparsePoints(string root)
    {
        EnsureNotReparsePoint(root);
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.TryPop(out string? directory))
        {
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
            {
                EnsureNotReparsePoint(entry);
                if ((File.GetAttributes(entry) & FileAttributes.Directory) != 0)
                {
                    pending.Push(entry);
                }
            }
        }
    }

    private sealed record SharedFixture(string Root, bool OwnsRoot);
}

[TestClass]
public sealed class FakeLlmFitToolAssemblyLifecycle
{
    [AssemblyCleanup]
    public static async Task CleanupAsync()
    {
        await FakeLlmFitTool.CleanupSharedAsync().ConfigureAwait(false);
    }
}
