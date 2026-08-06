using System.Collections;
using System.Text;
using GraniteEdgeAI.ModelInspection.WorkerClient;
using GraniteEdgeAI.ModelInspection.WorkerClient.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.WorkerProcess.Tests;

/// <summary>
/// Proves that the production Windows launcher creates the worker already
/// contained by a private Job Object and communicates through only the three
/// approved standard-stream pipes.
/// </summary>
[TestClass]
public sealed class WorkerLaunchContainmentTests
{
    [TestMethod]
    public async Task LaunchCreatesPrivateJobAndThreePipes()
    {
        await using PublishedFixture fixture =
            await PublishedFixture.CreateAsync().ConfigureAwait(false);
        using VerifiedWorkerExecutable executable =
            new WorkerExecutableResolver(
                "GraniteEdgeAI.ModelInspection.ProtocolTestWorker.exe")
                .Resolve(fixture.OutputDirectory);

        WindowsProcessLaunchRequest request = new(
            executable,
            fixture.OutputDirectory,
            WorkerEnvironmentPolicy.Create(CaptureParentEnvironment()),
            ["launch-probe"]);

        await using WorkerProcessSession session =
            WindowsWorkerProcessLauncher.Launch(request);
        using StreamReader reader = new(
            session.StandardOutput,
            new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true);

        string? milestone = await reader.ReadLineAsync()
            .WaitAsync(TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);

        Assert.AreEqual("fixture-ready", milestone);
        Assert.AreEqual(1u, session.Job.GetActiveProcessCount());

        // Let the harmless fixture exit normally before session cleanup proves
        // that the private Job Object reaches zero active processes.
        await session.StandardInput.WriteAsync([(byte)'\n'])
            .ConfigureAwait(false);
        await session.StandardInput.FlushAsync().ConfigureAwait(false);
        await session.WaitForExitAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);
        Assert.AreEqual(0u, session.Job.GetActiveProcessCount());
    }

    private static Dictionary<string, string?> CaptureParentEnvironment()
    {
        Dictionary<string, string?> values =
            new(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            string? key = entry.Key as string;
            if (key is not null)
            {
                values[key] = entry.Value as string;
            }
        }

        return values;
    }
}
