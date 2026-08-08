using System.Diagnostics;

namespace GraniteEdgeAI.ModelInspection.WorkerProcess.Tests;

/// <summary>
/// Resolves the production worker from a controlled CI publish root or, for a
/// local run, publishes it into a unique directory owned by this instance.
/// </summary>
internal sealed class PublishedWorker : IAsyncDisposable
{
    private const string WorkerRootEnvironmentVariable =
        "GRANITE_GATE2_WORKER_ROOT";
    private const string WorkerExecutableName =
        "GraniteEdgeAI.ModelInspection.Worker.exe";
    private const string WorkerProjectRelativePath =
        "workers/GraniteEdgeAI.ModelInspection.Worker/" +
        "GraniteEdgeAI.ModelInspection.Worker.csproj";
    private const string OwnedDirectoryParentName =
        "GraniteEdgeAI-ProductionWorker";

    private readonly string? _ownedOutputDirectory;

    private PublishedWorker(
        string outputDirectory,
        bool ownsOutputDirectory)
    {
        OutputDirectory = Path.GetFullPath(outputDirectory);
        _ownedOutputDirectory = ownsOutputDirectory
            ? OutputDirectory
            : null;
    }

    internal string OutputDirectory { get; }

    internal static async Task<PublishedWorker> CreateAsync()
    {
        string? controlledRoot = Environment.GetEnvironmentVariable(
            WorkerRootEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(controlledRoot))
        {
            string fullRoot = ValidatePublishedRoot(controlledRoot);
            return new PublishedWorker(
                fullRoot,
                ownsOutputDirectory: false);
        }

        string repositoryRoot = FindRepositoryRoot();
        string projectPath = Path.Combine(
            repositoryRoot,
            WorkerProjectRelativePath.Replace(
                '/',
                Path.DirectorySeparatorChar));
        string ownedParent = Path.Combine(
            Path.GetTempPath(),
            OwnedDirectoryParentName);
        string outputDirectory = Path.Combine(
            ownedParent,
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        try
        {
            await PublishAsync(
                    repositoryRoot,
                    projectPath,
                    outputDirectory)
                .ConfigureAwait(false);
            ValidatePublishedRoot(outputDirectory);
            return new PublishedWorker(
                outputDirectory,
                ownsOutputDirectory: true);
        }
        catch
        {
            DeleteOwnedDirectory(outputDirectory);
            throw;
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_ownedOutputDirectory is not null)
        {
            DeleteOwnedDirectory(_ownedOutputDirectory);
        }

        return ValueTask.CompletedTask;
    }

    private static async Task PublishAsync(
        string repositoryRoot,
        string projectPath,
        string outputDirectory)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "dotnet",
            WorkingDirectory = repositoryRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("publish");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add("Release");
        startInfo.ArgumentList.Add("--runtime");
        startInfo.ArgumentList.Add("win-x64");
        startInfo.ArgumentList.Add("--self-contained");
        startInfo.ArgumentList.Add("false");
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(outputDirectory);
        startInfo.ArgumentList.Add("-p:Platform=x64");
        startInfo.ArgumentList.Add("-p:UseAppHost=true");

        using Process process = new() { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException(
                "The production worker publish process could not start.");
        }

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource timeout =
            new(TimeSpan.FromMinutes(2));

        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(
                "Publishing the production worker exceeded two minutes.");
        }

        string output = await standardOutput.ConfigureAwait(false);
        string error = await standardError.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Production worker publish failed with exit code {process.ExitCode}. " +
                $"Output: {output} Error: {error}");
        }
    }

    private static string ValidatePublishedRoot(string root)
    {
        string fullRoot = Path.GetFullPath(root);
        if (!Directory.Exists(fullRoot))
        {
            throw new DirectoryNotFoundException(
                "The controlled Gate 2 worker root does not exist.");
        }

        string executablePath = Path.Combine(
            fullRoot,
            WorkerExecutableName);
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException(
                "The Gate 2 worker root does not contain the production executable.",
                WorkerExecutableName);
        }

        return fullRoot;
    }

    private static void DeleteOwnedDirectory(string directory)
    {
        string fullDirectory = Path.GetFullPath(directory);
        string ownedParent = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            OwnedDirectoryParentName));
        DirectoryInfo? parent = Directory.GetParent(fullDirectory);
        bool isUniqueChild = parent is not null &&
            string.Equals(
                parent.FullName,
                ownedParent,
                StringComparison.OrdinalIgnoreCase) &&
            Guid.TryParseExact(
                Path.GetFileName(fullDirectory),
                "N",
                out _);
        if (!isUniqueChild)
        {
            throw new InvalidOperationException(
                "Refusing to delete a production worker directory that is not owned by this test.");
        }

        try
        {
            if (Directory.Exists(fullDirectory))
            {
                Directory.Delete(fullDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Keep a transient Windows apphost lock secondary to the test result.
        }
        catch (UnauthorizedAccessException)
        {
            // Keep cleanup failure secondary to the integration-test result.
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

        throw new DirectoryNotFoundException(
            "The repository root containing global.json could not be found.");
    }
}
