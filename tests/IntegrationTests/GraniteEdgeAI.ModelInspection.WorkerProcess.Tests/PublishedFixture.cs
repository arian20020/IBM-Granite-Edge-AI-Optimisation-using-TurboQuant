using System.Diagnostics;

namespace GraniteEdgeAI.ModelInspection.WorkerProcess.Tests;

/// <summary>
/// Publishes the harmless protocol fixture into a unique short-lived directory
/// so integration tests exercise the same Windows app-host shape used by the
/// production launcher rather than a test double.
/// </summary>
internal sealed class PublishedFixture : IAsyncDisposable
{
    private const string FixtureProjectRelativePath =
        "tests/ProcessFixtures/GraniteEdgeAI.ModelInspection.ProtocolTestWorker/" +
        "GraniteEdgeAI.ModelInspection.ProtocolTestWorker.csproj";

    private PublishedFixture(string outputDirectory)
    {
        OutputDirectory = outputDirectory;
    }

    internal string OutputDirectory { get; }

    internal static async Task<PublishedFixture> CreateAsync()
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectPath = Path.Combine(
            repositoryRoot,
            FixtureProjectRelativePath.Replace('/', Path.DirectorySeparatorChar));
        string outputDirectory = Path.Combine(
            Path.GetTempPath(),
            "GraniteEdgeAI-WorkerFixture",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

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
                "The protocol test fixture publish process could not start.");
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
                "Publishing the protocol test fixture exceeded two minutes.");
        }

        string output = await standardOutput.ConfigureAwait(false);
        string error = await standardError.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Protocol fixture publish failed with exit code {process.ExitCode}. " +
                $"Output: {output} Error: {error}");
        }

        return new PublishedFixture(outputDirectory);
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            if (Directory.Exists(OutputDirectory))
            {
                Directory.Delete(OutputDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Test cleanup is best effort because Windows can briefly retain
            // app-host files after the child process has exited.
        }
        catch (UnauthorizedAccessException)
        {
            // Keep cleanup failure secondary to the integration-test result.
        }

        return ValueTask.CompletedTask;
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
