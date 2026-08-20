using System.Collections;
using System.Diagnostics;
using System.Text;
using GraniteEdgeAI.ModelInspection.Contracts;
using GraniteEdgeAI.ModelInspection.WorkerClient.ProtectedWorker;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.Tests;

/// <summary>
/// Exercises the route-neutral facade against the real reviewed Windows
/// launcher and deterministic worker fixture. Protocol messages are used only
/// to put the fixture into known process states; the facade never parses them.
/// </summary>
[TestClass]
public sealed class ProtectedWorkerSessionFactoryTests
{
    private const string FixtureExecutableName =
        "GraniteEdgeAI.ModelInspection.ProtocolTestWorker.exe";

    [TestMethod]
    public async Task StartAsyncExposesContainedBoundedSession()
    {
        using VerifiedWorkerExecutable executable = ResolveFixtureExecutable();
        ProtectedWorkerLaunchSpec spec = CreateSpec(
            executable,
            ["launch-probe"]);
        ProtectedWorkerSessionFactory factory = new();

        await using ProtectedWorkerSession session = await factory.StartAsync(
                spec,
                CancellationToken.None)
            .ConfigureAwait(false);

        Assert.IsTrue(session.ProcessId > 0);
        Assert.AreEqual(1u, session.GetActiveProcessCount());
        Assert.AreEqual(TimeSpan.FromSeconds(3), session.StartupTimeout);
        Assert.AreEqual(TimeSpan.FromSeconds(1), session.CancellationGrace);
        Assert.AreEqual(TimeSpan.FromSeconds(5), session.CleanupTimeout);
        Assert.IsTrue(session.StandardInput.CanWrite);
        Assert.IsTrue(session.StandardOutput.CanRead);

        using StreamReader output = CreateReader(session.StandardOutput);
        Assert.AreEqual(
            "fixture-ready",
            await output.ReadLineAsync()
                .WaitAsync(TimeSpan.FromSeconds(5))
                .ConfigureAwait(false));

        await session.StandardInput.WriteAsync(
                new ReadOnlyMemory<byte>([(byte)'\n']))
            .ConfigureAwait(false);
        await session.StandardInput.FlushAsync().ConfigureAwait(false);
        await session.WaitForExitAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5))
            .ConfigureAwait(false);

        Assert.IsTrue(
            await session.WaitForTreeEmptyAsync()
                .ConfigureAwait(false));
        StandardErrorSnapshot standardError =
            await session.ReadStandardErrorAsync().ConfigureAwait(false);
        Assert.IsTrue(
            Encoding.UTF8.GetByteCount(standardError.RetainedText) <=
            spec.MaximumStandardErrorBytes);
        Assert.AreEqual(string.Empty, standardError.RetainedText);
    }

    [TestMethod]
    public async Task TerminateAndVerifyEmptyAsyncStopsTheFullTreeWithinPolicy()
    {
        using VerifiedWorkerExecutable executable = ResolveFixtureExecutable();
        ProtectedWorkerLaunchSpec spec = CreateSpec(
            executable,
            ["spawn-child-and-wait"]);
        ProtectedWorkerSessionFactory factory = new();

        await using ProtectedWorkerSession session = await factory.StartAsync(
                spec,
                CancellationToken.None)
            .ConfigureAwait(false);
        using StreamReader output = CreateReader(session.StandardOutput);
        string? hello = await output.ReadLineAsync()
            .WaitAsync(TimeSpan.FromSeconds(5))
            .ConfigureAwait(false);
        Assert.IsNotNull(hello);
        await WriteStartCommandAsync(session.StandardInput)
            .ConfigureAwait(false);
        string? started = await output.ReadLineAsync()
            .WaitAsync(TimeSpan.FromSeconds(5))
            .ConfigureAwait(false);
        Assert.IsNotNull(started);

        uint activeProcesses = await WaitForActiveProcessCountAsync(session, 2u)
            .ConfigureAwait(false);
        Assert.AreEqual(2u, activeProcesses);
        Stopwatch elapsed = Stopwatch.StartNew();
        bool forced = await session.TerminateAndVerifyEmptyAsync()
            .ConfigureAwait(false);
        elapsed.Stop();

        Assert.IsTrue(forced);
        Assert.AreEqual(0u, session.GetActiveProcessCount());
        Assert.IsTrue(
            elapsed.Elapsed <= TimeSpan.FromSeconds(5),
            $"Cleanup took {elapsed.Elapsed}.");
        StandardErrorSnapshot standardError =
            await session.ReadStandardErrorAsync().ConfigureAwait(false);
        StringAssert.Contains(standardError.RetainedText, "FIXTURE:CHILD_STARTED:");
    }

    [TestMethod]
    [DataRow(@"C:\Models\granite.gguf")]
    [DataRow("granite.gguf")]
    [DataRow("Tell me a private story")]
    [DataRow("{\"prompt\":\"private\"}")]
    [DataRow("--prompt")]
    public async Task StartAsyncRejectsSensitiveFixedArguments(string argument)
    {
        using VerifiedWorkerExecutable executable = ResolveFixtureExecutable();
        ProtectedWorkerLaunchSpec spec = CreateSpec(executable, [argument]);
        ProtectedWorkerSessionFactory factory = new();

        WorkerClientPolicyException error =
            await Assert.ThrowsExactlyAsync<WorkerClientPolicyException>(
                () => factory.StartAsync(spec, CancellationToken.None))
            .ConfigureAwait(false);

        Assert.AreEqual(
            WorkerClientFailureCodes.WorkerLaunchFailed,
            error.Failure.Code);
    }

    [TestMethod]
    public async Task StartAsyncRejectsEnvironmentOutsideClosedAllowlist()
    {
        using VerifiedWorkerExecutable executable = ResolveFixtureExecutable();
        Dictionary<string, string> environment = new(
            CreateEnvironment(),
            StringComparer.OrdinalIgnoreCase)
        {
            ["OPENAI_API_KEY"] = "must-not-cross-process-boundary"
        };
        ProtectedWorkerLaunchSpec spec = new(
            executable,
            ["launch-probe"],
            environment,
            MaximumStandardErrorBytes: 1024,
            StartupTimeout: TimeSpan.FromSeconds(3),
            CancellationGrace: TimeSpan.FromSeconds(1),
            CleanupTimeout: TimeSpan.FromSeconds(5));
        ProtectedWorkerSessionFactory factory = new();

        WorkerClientPolicyException error =
            await Assert.ThrowsExactlyAsync<WorkerClientPolicyException>(
                () => factory.StartAsync(spec, CancellationToken.None))
            .ConfigureAwait(false);

        Assert.AreEqual(
            WorkerClientFailureCodes.WorkerEnvironmentPolicyFailed,
            error.Failure.Code);
        Assert.IsFalse(
            error.Message.Contains("OPENAI", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(
            error.Message.Contains(
                "must-not-cross-process-boundary",
                StringComparison.Ordinal));
    }

    private static ProtectedWorkerLaunchSpec CreateSpec(
        VerifiedWorkerExecutable executable,
        IReadOnlyList<string> arguments) => new(
        executable,
        arguments,
        CreateEnvironment(),
        MaximumStandardErrorBytes: 1024,
        StartupTimeout: TimeSpan.FromSeconds(3),
        CancellationGrace: TimeSpan.FromSeconds(1),
        CleanupTimeout: TimeSpan.FromSeconds(5));

    private static VerifiedWorkerExecutable ResolveFixtureExecutable() =>
        new WorkerExecutableResolver(FixtureExecutableName)
            .Resolve(AppContext.BaseDirectory);

    private static IReadOnlyDictionary<string, string> CreateEnvironment()
    {
        Dictionary<string, string?> parent =
            new(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string key)
            {
                parent[key] = entry.Value as string;
            }
        }

        return WorkerEnvironmentPolicy.Create(parent);
    }

    private static StreamReader CreateReader(Stream stream) => new(
        stream,
        new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true),
        detectEncodingFromByteOrderMarks: false,
        bufferSize: 1024,
        leaveOpen: true);

    private static async Task<uint> WaitForActiveProcessCountAsync(
        ProtectedWorkerSession session,
        uint expected)
    {
        Stopwatch timeout = Stopwatch.StartNew();
        uint actual;
        do
        {
            actual = session.GetActiveProcessCount();
            if (actual == expected)
            {
                return actual;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25))
                .ConfigureAwait(false);
        }
        while (timeout.Elapsed < TimeSpan.FromSeconds(3));

        return actual;
    }

    private static async Task WriteStartCommandAsync(Stream input)
    {
        using Process current = Process.GetCurrentProcess();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        WorkerStartInspectionCommand command = new()
        {
            ProtocolVersion = WorkerProtocol.Version,
            CommandType = WorkerCommandKind.StartInspection,
            RequestId = Guid.NewGuid(),
            ParentProcessId = current.Id,
            ParentProcessStartTimeUtc = new DateTimeOffset(
                current.StartTime.ToUniversalTime(),
                TimeSpan.Zero),
            ModelPath = Path.Combine(Path.GetTempPath(), "model.gguf"),
            ExpectedFileIdentity = new WorkerExpectedFileIdentity
            {
                LengthBytes = 1,
                LastWriteTimeUtc = now
            },
            QuickScan = new WorkerQuickScanSnapshot
            {
                Format = "GGUF",
                ModelName = "fixture",
                Architecture = "granite",
                ParameterSizeLabel = "fixture",
                Quantisation = "Q4_K_M",
                FileSizeBytes = 1,
                DeclaredContextLength = 128,
                GgufVersion = 3
            }
        };
        byte[] payload = WorkerProtocolJson.Serialize(command);
        await input.WriteAsync(payload).ConfigureAwait(false);
        await input.WriteAsync(new ReadOnlyMemory<byte>([(byte)'\n']))
            .ConfigureAwait(false);
        await input.FlushAsync().ConfigureAwait(false);
    }
}
