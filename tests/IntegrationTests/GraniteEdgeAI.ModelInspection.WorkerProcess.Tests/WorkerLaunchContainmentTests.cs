using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using GraniteEdgeAI.ModelInspection.WorkerClient;
using GraniteEdgeAI.ModelInspection.WorkerClient.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32.SafeHandles;

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
            ResolveFixtureExecutable(fixture);
        WindowsProcessLaunchRequest request = CreateRequest(
            executable,
            ["launch-probe"]);

        await using WorkerProcessSession session =
            WindowsWorkerProcessLauncher.Launch(request);
        string? milestone = await ReadMilestoneAsync(session)
            .ConfigureAwait(false);

        Assert.AreEqual("fixture-ready", milestone);
        Assert.AreEqual(
            1u,
            session.Job.GetActiveProcessCount(),
            $"Contained processes: {DescribeJobProcesses(session.Job)}");

        await CompleteFixtureAsync(session).ConfigureAwait(false);
        Assert.AreEqual(0u, session.Job.GetActiveProcessCount());
    }

    [TestMethod]
    public async Task UnrelatedInheritableHandleIsNotAvailableToChild()
    {
        await using PublishedFixture fixture =
            await PublishedFixture.CreateAsync().ConfigureAwait(false);
        using VerifiedWorkerExecutable executable =
            ResolveFixtureExecutable(fixture);
        using EventWaitHandle unrelatedEvent =
            new(initialState: false, EventResetMode.ManualReset);

        bool markedInheritable = SetHandleInformation(
            unrelatedEvent.SafeWaitHandle,
            NativeConstants.HandleFlagInherit,
            NativeConstants.HandleFlagInherit);
        Assert.IsTrue(markedInheritable, "The test handle was not inheritable.");

        string handleValue = unrelatedEvent.SafeWaitHandle
            .DangerousGetHandle()
            .ToInt64()
            .ToString(CultureInfo.InvariantCulture);
        WindowsProcessLaunchRequest request = CreateRequest(
            executable,
            ["probe-unrelated-handle", handleValue]);

        await using WorkerProcessSession session =
            WindowsWorkerProcessLauncher.Launch(request);
        string? milestone = await ReadMilestoneAsync(session)
            .ConfigureAwait(false);

        Assert.AreEqual("handle-unavailable", milestone);
        Assert.IsFalse(
            unrelatedEvent.WaitOne(millisecondsTimeout: 0),
            "The child signalled a handle excluded from HANDLE_LIST.");
        Assert.AreEqual(1u, session.Job.GetActiveProcessCount());

        await CompleteFixtureAsync(session).ConfigureAwait(false);
        Assert.AreEqual(0u, session.Job.GetActiveProcessCount());
    }

    private static VerifiedWorkerExecutable ResolveFixtureExecutable(
        PublishedFixture fixture) =>
        new WorkerExecutableResolver(
            "GraniteEdgeAI.ModelInspection.ProtocolTestWorker.exe")
            .Resolve(fixture.OutputDirectory);

    private static WindowsProcessLaunchRequest CreateRequest(
        VerifiedWorkerExecutable executable,
        IReadOnlyList<string> arguments) => new(
            executable,
            WorkerEnvironmentPolicy.Create(CaptureParentEnvironment()),
            arguments);

    private static async Task<string?> ReadMilestoneAsync(
        WorkerProcessSession session)
    {
        using StreamReader reader = new(
            session.StandardOutput,
            new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true);
        return await reader.ReadLineAsync()
            .WaitAsync(TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);
    }

    private static async Task CompleteFixtureAsync(
        WorkerProcessSession session)
    {
        byte[] newline = [(byte)'\n'];
        await session.StandardInput.WriteAsync(newline)
            .ConfigureAwait(false);
        await session.StandardInput.FlushAsync().ConfigureAwait(false);
        await session.WaitForExitAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);
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

    private static string DescribeJobProcesses(WindowsJobObject job)
    {
        List<string> descriptions = [];
        foreach (int processId in job.GetProcessIds())
        {
            try
            {
                using Process process = Process.GetProcessById(processId);
                descriptions.Add($"{processId}:{process.ProcessName}");
            }
            catch (ArgumentException)
            {
                descriptions.Add($"{processId}:exited");
            }
            catch (InvalidOperationException)
            {
                descriptions.Add($"{processId}:unavailable");
            }
        }

        return string.Join(", ", descriptions);
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetHandleInformation(
        SafeWaitHandle handle,
        uint mask,
        uint flags);
}
