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
            FixtureArguments("launch-probe"));
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
        byte[]? ready = await session.StandardOutput
            .ReadLineAsync(CancellationToken.None).AsTask()
            .WaitAsync(TimeSpan.FromSeconds(5))
            .ConfigureAwait(false);
        Assert.AreEqual(
            "fixture-ready",
            Encoding.UTF8.GetString(ready!));
        await session.StandardInput.WriteLineAsync(
            "complete"u8.ToArray(),
            CancellationToken.None);
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
            FixtureArguments("spawn-child-and-wait"));
        ProtectedWorkerSessionFactory factory = new();

        await using ProtectedWorkerSession session = await factory.StartAsync(
                spec,
                CancellationToken.None)
            .ConfigureAwait(false);
        byte[]? hello = await session.StandardOutput
            .ReadLineAsync(CancellationToken.None).AsTask()
            .WaitAsync(TimeSpan.FromSeconds(5))
            .ConfigureAwait(false);
        Assert.IsNotNull(hello);
        await WriteStartCommandAsync(session.StandardInput)
            .ConfigureAwait(false);
        byte[]? started = await session.StandardOutput
            .ReadLineAsync(CancellationToken.None).AsTask()
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
    public async Task SessionEnforcesBothConfiguredLineBounds()
    {
        using VerifiedWorkerExecutable executable = ResolveFixtureExecutable();
        ProtectedWorkerLaunchSpec inputSpec = CreateSpec(
            executable,
            FixtureArguments("launch-probe")) with
        {
            MaximumStandardInputLineBytes = 4
        };
        ProtectedWorkerSessionFactory factory = new();

        await using (ProtectedWorkerSession inputSession =
            await factory.StartAsync(inputSpec, CancellationToken.None))
        {
            await Assert.ThrowsExactlyAsync<
                GraniteEdgeAI.ModelInspection.Transport.ProtocolStreamException>(
                async () => await inputSession.StandardInput.WriteLineAsync(
                    "12345"u8.ToArray(),
                    CancellationToken.None));
        }

        ProtectedWorkerLaunchSpec outputSpec = CreateSpec(
            executable,
            FixtureArguments("oversized-stdout-line")) with
        {
            MaximumStandardOutputLineBytes = 32
        };
        await using ProtectedWorkerSession outputSession =
            await factory.StartAsync(outputSpec, CancellationToken.None);
        await Assert.ThrowsExactlyAsync<
            GraniteEdgeAI.ModelInspection.Transport.ProtocolStreamException>(
            async () => await outputSession.StandardOutput
                .ReadLineAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task StderrFloodIsDrainedAndRetainedWithinConfiguredBound()
    {
        using VerifiedWorkerExecutable executable = ResolveFixtureExecutable();
        ProtectedWorkerLaunchSpec spec = CreateSpec(
            executable,
            FixtureArguments("flood-stderr")) with
        {
            MaximumStandardErrorBytes = 64
        };
        ProtectedWorkerSessionFactory factory = new();

        await using ProtectedWorkerSession session =
            await factory.StartAsync(spec, CancellationToken.None);
        Assert.IsNotNull(await session.StandardOutput
            .ReadLineAsync(CancellationToken.None));
        await WriteStartCommandAsync(session.StandardInput);
        while (await session.StandardOutput.ReadLineAsync(CancellationToken.None)
            is not null)
        {
        }

        await session.WaitForExitAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5));
        StandardErrorSnapshot snapshot = await session.ReadStandardErrorAsync()
            .WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(snapshot.IsTruncated);
        Assert.IsTrue(
            Encoding.UTF8.GetByteCount(snapshot.RetainedText) <= 64);
    }

    [TestMethod]
    public async Task WorkerObservesDeliveredParentPidAndCreationTime()
    {
        using VerifiedWorkerExecutable executable = ResolveFixtureExecutable();
        ProtectedWorkerLaunchSpec spec = CreateSpec(
            executable,
            FixtureArguments("observe-parent-identity"));
        ProtectedWorkerSessionFactory factory = new();

        await using ProtectedWorkerSession session =
            await factory.StartAsync(spec, CancellationToken.None);
        Assert.IsNotNull(await session.StandardOutput
            .ReadLineAsync(CancellationToken.None));
        await WriteStartCommandAsync(session.StandardInput);
        while (await session.StandardOutput.ReadLineAsync(CancellationToken.None)
            is not null)
        {
        }

        await session.WaitForExitAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5));
        StandardErrorSnapshot snapshot = await session.ReadStandardErrorAsync();
        StringAssert.Contains(
            snapshot.RetainedText,
            "FIXTURE:PARENT_IDENTITY_OBSERVED");
    }

    [TestMethod]
    public async Task StartAsyncRejectsCleanupTimeoutBeyondFiveSeconds()
    {
        using VerifiedWorkerExecutable executable = ResolveFixtureExecutable();
        ProtectedWorkerLaunchSpec spec = CreateSpec(
            executable,
            FixtureArguments("launch-probe")) with
        {
            CleanupTimeout = TimeSpan.FromSeconds(5) + TimeSpan.FromTicks(1)
        };
        ProtectedWorkerSessionFactory factory = new();

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(
            () => factory.StartAsync(spec, CancellationToken.None));
    }

    [TestMethod]
    public async Task ValidatedArgumentsAreSnapshottedFromTheValidatedValues()
    {
        using VerifiedWorkerExecutable executable = ResolveFixtureExecutable();
        ProtectedWorkerLaunchSpec spec = CreateSpec(
            executable,
            new ChangingArgumentList());
        ProtectedWorkerSessionFactory factory = new();

        await using ProtectedWorkerSession session =
            await factory.StartAsync(spec, CancellationToken.None);
        byte[]? ready = await session.StandardOutput
            .ReadLineAsync(CancellationToken.None);

        Assert.AreEqual("fixture-ready", Encoding.UTF8.GetString(ready!));
        await session.StandardInput.WriteLineAsync(
            "complete"u8.ToArray(),
            CancellationToken.None);
    }

    [TestMethod]
    [DataRow(@"C:\Models\granite.gguf")]
    [DataRow("granite.gguf")]
    [DataRow("models/private.weights")]
    [DataRow("Tell me a private story")]
    [DataRow("tell-me-a-secret")]
    [DataRow("payload.json")]
    [DataRow("{\"prompt\":\"private\"}")]
    [DataRow("--prompt")]
    public async Task StartAsyncRejectsStandaloneFixedArguments(string argument)
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
    [DataRow("--api-key", "secret-value")]
    [DataRow("--model", "private.weights")]
    [DataRow("--prompt", "tell-me-a-secret")]
    [DataRow("--protocol", "models/private/1")]
    [DataRow("--protocol", "OpenVino.Official/1")]
    [DataRow("--protocol", "openvino/1")]
    [DataRow("--protocol", "openvino..official/1")]
    [DataRow("--protocol", "openvino.official/0")]
    [DataRow("--protocol", "openvino.official/01")]
    [DataRow("--protocol", "openvino.official/-1")]
    [DataRow("--protocol", "openvino.official/1/extra")]
    public async Task StartAsyncRejectsNoncanonicalArgumentSequences(
        string selector,
        string value)
    {
        using VerifiedWorkerExecutable executable = ResolveFixtureExecutable();
        ProtectedWorkerLaunchSpec spec = CreateSpec(
            executable,
            [selector, value]);
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
            FixtureArguments("launch-probe"),
            environment,
            MaximumStandardInputLineBytes: WorkerProtocol.MaximumMessageBytes,
            MaximumStandardOutputLineBytes: WorkerProtocol.MaximumMessageBytes,
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
        MaximumStandardInputLineBytes: WorkerProtocol.MaximumMessageBytes,
        MaximumStandardOutputLineBytes: WorkerProtocol.MaximumMessageBytes,
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

    private static string[] FixtureArguments(string scenario, long version = 1) =>
        ["--protocol", $"modelinspection.fixture.{scenario}/{version}"];

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

    private static async Task WriteStartCommandAsync(
        GraniteEdgeAI.ModelInspection.Transport.BoundedUtf8LineWriter input)
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
        await input.WriteLineAsync(payload, CancellationToken.None)
            .ConfigureAwait(false);
    }

    private sealed class ChangingArgumentList : IReadOnlyList<string>
    {
        private int _identifierReads;

        public int Count => 2;

        public string this[int index] => index switch
        {
            0 => "--protocol",
            1 when Interlocked.Increment(ref _identifierReads) == 1 =>
                "modelinspection.fixture.launch-probe/1",
            1 => "secret-value",
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

        public IEnumerator<string> GetEnumerator() =>
            Enumerable.Range(0, Count).Select(index => this[index])
                .GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
