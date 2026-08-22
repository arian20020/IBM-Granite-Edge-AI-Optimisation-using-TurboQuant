using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using HardwareInspection.LlmFitSpike.Command;
using HardwareInspection.LlmFitSpike.Execution;
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
    private const int MaximumPublishDiagnosticBytesPerStream = 65_536;
    private static readonly TimeSpan PublishCleanupDeadline = TimeSpan.FromSeconds(5);
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

    internal async Task<int> WaitForChildReadyAsync(TimeSpan timeout)
    {
        return await WaitForOwnedProcessReadyAsync(
                "spawn-child-ready.txt",
                timeout,
                "The owned fake child did not become ready in time.")
            .ConfigureAwait(false);
    }

    internal async Task<int> WaitForRootReadyAsync(TimeSpan timeout)
    {
        return await WaitForOwnedProcessReadyAsync(
                "owned-root-ready.txt",
                timeout,
                "The owned fake root did not become ready in time.")
            .ConfigureAwait(false);
    }

    private async Task<int> WaitForOwnedProcessReadyAsync(
        string readinessFileName,
        TimeSpan timeout,
        string timeoutMessage)
    {
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(10))
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        string readinessPath = Path.Combine(Root, readinessFileName);
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < timeout)
        {
            try
            {
                if (File.Exists(readinessPath))
                {
                    string text = await File.ReadAllTextAsync(readinessPath).ConfigureAwait(false);
                    if (int.TryParse(
                            text,
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out int childProcessId) &&
                        childProcessId > 0)
                    {
                        return childProcessId;
                    }
                }
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                // The owned readiness file may still be moving into place.
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20)).ConfigureAwait(false);
        }

        throw new TimeoutException(timeoutMessage);
    }

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

    internal static Task DeleteOwnedDirectoryForTestsAsync(string path)
    {
        return DeleteOwnedDirectoryAsync(path, TestDirectoryPrefix);
    }

    private static async Task<SharedFixture> ResolveOrPublishAsync()
    {
        string? configuredRoot = Environment.GetEnvironmentVariable(ControlledRootEnvironmentVariable);
        if (configuredRoot is not null)
        {
            string controlledRoot = ResolveControlledRootForTests(configuredRoot)!;
            return new SharedFixture(controlledRoot, OwnsRoot: false);
        }

        return await PublishAsync().ConfigureAwait(false);
    }

    internal static string? ResolveControlledRootForTests(string? configuredRoot)
    {
        if (configuredRoot is null)
        {
            return null;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(configuredRoot))
            {
                throw new InvalidDataException();
            }

            string root = Path.GetFullPath(configuredRoot);
            if (!Directory.Exists(root) || !File.Exists(Path.Combine(root, ExecutableName)))
            {
                throw new InvalidDataException();
            }

            EnsureTreeHasNoReparsePoints(root);
            return root;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            InvalidDataException or
            IOException or
            NotSupportedException or
            UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                "The controlled LLM Fit fake tool root is invalid.");
        }
    }

    private static async Task<SharedFixture> PublishAsync()
    {
        string outputRoot = CreateOwnedDirectory(PublishDirectoryPrefix);
        Process? process = null;
        Task? exitTask = null;
        Task<BoundedTextCapture>? standardOutputTask = null;
        Task<BoundedTextCapture>? standardErrorTask = null;
        bool processStarted = false;
        bool cleanupAttempted = false;
        bool cleanupComplete = true;

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

            process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                throw new InvalidOperationException();
            }

            processStarted = true;
            standardOutputTask = BoundedTextReader.ReadAsync(
                process.StandardOutput.BaseStream,
                MaximumPublishDiagnosticBytesPerStream);
            standardErrorTask = BoundedTextReader.ReadAsync(
                process.StandardError.BaseStream,
                MaximumPublishDiagnosticBytesPerStream);
            exitTask = process.WaitForExitAsync(CancellationToken.None);

            using var timeoutLifetime = new CancellationTokenSource();
            Task timeoutTask = Task.Delay(TimeSpan.FromMinutes(2), timeoutLifetime.Token);
            PublishTerminal terminal = await WaitForPublishTerminalAsync(
                    exitTask,
                    standardOutputTask,
                    standardErrorTask,
                    timeoutTask)
                .ConfigureAwait(false);
            TryCancel(timeoutLifetime);
            if (terminal != PublishTerminal.Exit)
            {
                TryKillProcessTree(process);
                PublishCleanup cleanup = await AwaitPublishCleanupAsync(
                        process,
                        exitTask,
                        standardOutputTask,
                        standardErrorTask)
                    .ConfigureAwait(false);
                cleanupAttempted = true;
                cleanupComplete = cleanup.Complete;
                if (terminal == PublishTerminal.Timeout)
                {
                    throw new TimeoutException("Publishing the LLM Fit fake tool exceeded two minutes.");
                }

                throw new InvalidOperationException();
            }

            PublishCleanup completed = await AwaitPublishCleanupAsync(
                    process,
                    exitTask,
                    standardOutputTask,
                    standardErrorTask)
                .ConfigureAwait(false);
            cleanupAttempted = true;
            cleanupComplete = completed.Complete;
            if (!completed.Succeeded)
            {
                throw new InvalidOperationException();
            }

            if (process.ExitCode != 0 || !File.Exists(Path.Combine(outputRoot, ExecutableName)))
            {
                throw new InvalidOperationException();
            }

            return new SharedFixture(outputRoot, OwnsRoot: true);
        }
        catch (Exception exception)
        {
            if (processStarted && process is not null && !cleanupAttempted)
            {
                if (!HasExited(process))
                {
                    TryKillProcessTree(process);
                }

                PublishCleanup cleanup = await AwaitPublishCleanupAsync(
                        process,
                        exitTask,
                        standardOutputTask,
                        standardErrorTask)
                    .ConfigureAwait(false);
                cleanupComplete = cleanup.Complete;
            }

            if (cleanupComplete)
            {
                await DeleteOwnedDirectoryAsync(outputRoot, PublishDirectoryPrefix).ConfigureAwait(false);
            }

            if (!cleanupComplete)
            {
                throw new InvalidOperationException(
                    "The LLM Fit fake tool publish process could not be cleaned up.");
            }

            if (exception is TimeoutException)
            {
                throw new TimeoutException("Publishing the LLM Fit fake tool exceeded two minutes.");
            }

            throw new InvalidOperationException("The LLM Fit fake tool could not be published.");
        }
        finally
        {
            TryDisposeProcess(process);
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
        catch (Win32Exception)
        {
            // Bounded cleanup below decides whether termination was confirmed.
        }
        catch (NotSupportedException)
        {
            // Bounded cleanup below decides whether termination was confirmed.
        }
    }

    private static async Task<PublishCleanup> AwaitPublishCleanupAsync(
        Process process,
        Task? exitTask,
        Task<BoundedTextCapture>? standardOutputTask,
        Task<BoundedTextCapture>? standardErrorTask)
    {
        if (exitTask is null)
        {
            try
            {
                exitTask = process.WaitForExitAsync(CancellationToken.None);
            }
            catch
            {
                if (standardOutputTask is not null)
                {
                    ObserveFault(standardOutputTask);
                }

                if (standardErrorTask is not null)
                {
                    ObserveFault(standardErrorTask);
                }

                return new PublishCleanup(Complete: false, Succeeded: false);
            }
        }

        var tasks = new List<Task>();
        AddObservedTask(tasks, exitTask);
        AddObservedTask(tasks, standardOutputTask);
        AddObservedTask(tasks, standardErrorTask);
        Task allTasks = Task.WhenAll(tasks);
        ObserveFault(allTasks);

        using var deadlineLifetime = new CancellationTokenSource();
        Task deadlineTask = Task.Delay(PublishCleanupDeadline, deadlineLifetime.Token);
        Task completedTask = await Task.WhenAny(allTasks, deadlineTask).ConfigureAwait(false);
        bool completedWithinDeadline = ReferenceEquals(completedTask, allTasks);
        if (completedWithinDeadline)
        {
            TryCancel(deadlineLifetime);
            try
            {
                await allTasks.ConfigureAwait(false);
            }
            catch
            {
                // Completion state is mapped below without exposing diagnostics.
            }
        }

        ObserveFault(deadlineTask);
        bool tasksSucceeded = exitTask.IsCompletedSuccessfully &&
            (standardOutputTask is null || standardOutputTask.IsCompletedSuccessfully) &&
            (standardErrorTask is null || standardErrorTask.IsCompletedSuccessfully);
        bool complete = completedWithinDeadline &&
            exitTask.IsCompleted &&
            (standardOutputTask is null || standardOutputTask.IsCompleted) &&
            (standardErrorTask is null || standardErrorTask.IsCompleted) &&
            HasExited(process);
        return new PublishCleanup(complete, Succeeded: complete && tasksSucceeded);
    }

    private static async Task<PublishTerminal> WaitForPublishTerminalAsync(
        Task exitTask,
        Task standardOutputTask,
        Task standardErrorTask,
        Task timeoutTask)
    {
        while (true)
        {
            if (exitTask.IsCompleted)
            {
                return exitTask.IsCompletedSuccessfully
                    ? PublishTerminal.Exit
                    : PublishTerminal.Failure;
            }

            if (standardOutputTask.IsCompleted && !standardOutputTask.IsCompletedSuccessfully ||
                standardErrorTask.IsCompleted && !standardErrorTask.IsCompletedSuccessfully)
            {
                return PublishTerminal.Failure;
            }

            if (timeoutTask.IsCompleted)
            {
                return timeoutTask.IsCompletedSuccessfully
                    ? PublishTerminal.Timeout
                    : PublishTerminal.Failure;
            }

            var pendingTasks = new List<Task> { exitTask, timeoutTask };
            if (!standardOutputTask.IsCompleted)
            {
                pendingTasks.Add(standardOutputTask);
            }

            if (!standardErrorTask.IsCompleted)
            {
                pendingTasks.Add(standardErrorTask);
            }

            _ = await Task.WhenAny(pendingTasks).ConfigureAwait(false);
        }
    }

    private static void AddObservedTask(List<Task> tasks, Task? task)
    {
        if (task is not null)
        {
            ObserveFault(task);
            tasks.Add(task);
        }
    }

    private static void ObserveFault(Task task)
    {
        if (task.IsFaulted)
        {
            _ = task.Exception;
        }
        else if (!task.IsCompleted)
        {
            _ = task.ContinueWith(
                static completed => _ = completed.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private static void TryCancel(CancellationTokenSource source)
    {
        try
        {
            source.Cancel();
        }
        catch (AggregateException)
        {
            // This source has no user callbacks; retain a stable harness failure surface.
        }
    }

    private static bool HasExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return false;
        }
    }

    private static void TryDisposeProcess(Process? process)
    {
        try
        {
            process?.Dispose();
        }
        catch
        {
            // Process disposal cannot expose machine-specific diagnostics.
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
                NormalizeReadOnlyAttributes(path);
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is ArgumentException or
                InvalidDataException or
                NotSupportedException)
            {
                throw new InvalidOperationException(
                    "The owned fake tool directory could not be cleaned up.");
            }
        }

        throw new InvalidOperationException(
            "The owned fake tool directory could not be cleaned up.");
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

    private static void NormalizeReadOnlyAttributes(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.TryPop(out string? entry))
        {
            EnsureNotReparsePoint(entry);
            FileAttributes attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReadOnly) != 0)
            {
                File.SetAttributes(entry, attributes & ~FileAttributes.ReadOnly);
            }

            if ((attributes & FileAttributes.Directory) != 0)
            {
                foreach (string child in Directory.EnumerateFileSystemEntries(entry))
                {
                    pending.Push(child);
                }
            }
        }
    }

    private readonly record struct PublishCleanup(bool Complete, bool Succeeded);

    private enum PublishTerminal
    {
        Exit,
        Timeout,
        Failure,
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
