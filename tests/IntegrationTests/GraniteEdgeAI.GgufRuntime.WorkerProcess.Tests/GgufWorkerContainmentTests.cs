using System.Diagnostics;
using System.Text;
using GraniteEdgeAI.GgufRuntime.ProtocolTestWorker;
using GraniteEdgeAI.GgufRuntime.WorkerClient;
using GraniteEdgeAI.GgufRuntime.WorkerClient.Windows;

namespace GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests;

[TestClass]
public sealed class GgufWorkerContainmentTests
{
    [TestMethod]
    public async Task LaunchUsesSanitizedEnvironmentAndInheritedStandardStreams()
    {
        string executable = ResolveFixtureExecutable();
        IReadOnlyDictionary<string, string> environment = CreateEnvironment();
        await using GgufWorkerProcessSession session = GgufWorkerProcessLauncher.Launch(
            executable,
            [],
            environment,
            Path.GetDirectoryName(executable)!);
        using var reader = new StreamReader(
            session.StandardOutput,
            new UTF8Encoding(false, true),
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);
        await using var writer = new StreamWriter(
            session.StandardInput,
            new UTF8Encoding(false),
            leaveOpen: true)
        {
            AutoFlush = true,
        };

        string? environmentLine = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
        await writer.WriteLineAsync("ping");
        string? echoLine = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.AreEqual("secret=absent", environmentLine);
        Assert.AreEqual("echo=ping", echoLine);
        Assert.AreEqual(
            1u,
            session.ActiveProcessCount,
            $"Contained processes: {string.Join(",", session.ProcessIds.Select(DescribeProcess))}");
    }

    [TestMethod]
    public async Task TerminateAndVerifyEmptyKillsWorkerAndSpawnedChild()
    {
        string executable = ResolveFixtureExecutable();
        await using GgufWorkerProcessSession session = GgufWorkerProcessLauncher.Launch(
            executable,
            ["--spawn-child"],
            CreateEnvironment(),
            Path.GetDirectoryName(executable)!);
        using var reader = new StreamReader(
            session.StandardOutput,
            new UTF8Encoding(false, true),
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);
        _ = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
        string childLine = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10))
            ?? throw new InvalidOperationException("The fixture did not report its child.");
        int childId = int.Parse(childLine["child=".Length..], System.Globalization.CultureInfo.InvariantCulture);

        await session.TerminateAndVerifyEmptyAsync(TimeSpan.FromSeconds(10));

        Assert.AreEqual(0u, session.ActiveProcessCount);
        Assert.ThrowsExactly<ArgumentException>(() => Process.GetProcessById(childId));
    }

    private static string ResolveFixtureExecutable()
    {
        string assemblyPath = typeof(ProtocolTestWorkerMarker).Assembly.Location;
        return Path.ChangeExtension(assemblyPath, ".exe");
    }

    private static string DescribeProcess(int processId)
    {
        using Process process = Process.GetProcessById(processId);
        return $"{processId}:{process.ProcessName}";
    }

    private static IReadOnlyDictionary<string, string> CreateEnvironment()
    {
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string temp = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
        var parent = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["SystemRoot"] = windows,
            ["WINDIR"] = windows,
            ["TEMP"] = temp,
            ["TMP"] = temp,
            ["G1_TEST_SECRET"] = "must-not-cross-boundary",
        };
        return GgufWorkerEnvironmentPolicy.Create(parent);
    }
}
