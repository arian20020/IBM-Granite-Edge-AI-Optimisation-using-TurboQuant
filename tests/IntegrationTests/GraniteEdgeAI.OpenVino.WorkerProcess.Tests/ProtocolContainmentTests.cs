using System.Diagnostics;
using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.OpenVino.WorkerClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.OpenVino.WorkerProcess.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ProtocolContainmentTests
{
    private const string Digest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly string[] ExpectedText = ["one", "two"];
    private static string? s_publishedFixture;

    [ClassInitialize]
    public static async Task PublishFixture(TestContext _)
    {
        string root = FindRepositoryRoot();
        s_publishedFixture = Path.Combine(
            Path.GetTempPath(),
            "GraniteEdgeAI-OpenVinoFixture",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(s_publishedFixture);
        ProcessStartInfo info = new("dotnet")
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in new[]
        {
            "publish",
            Path.Combine(root, "tests", "ProcessFixtures", "GraniteEdgeAI.OpenVino.ProtocolTestWorker", "GraniteEdgeAI.OpenVino.ProtocolTestWorker.csproj"),
            "-c", "Release", "-r", "win-x64", "--self-contained", "false",
            "-o", s_publishedFixture, "-p:Platform=x64", "-p:UseAppHost=true"
        })
        {
            info.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(info)
            ?? throw new InvalidOperationException("Fixture publish did not start.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "Fixture publish failed: " + await output + await error);
        }
    }

    [ClassCleanup]
    public static void RemoveFixture()
    {
        if (s_publishedFixture is not null && Directory.Exists(s_publishedFixture))
        {
            Directory.Delete(s_publishedFixture, recursive: true);
        }
    }

    [TestMethod]
    public async Task ValidWorkerStreamsTwoTurnsAndDiscardsStaleEvents()
    {
        await using FixtureRun fixture = CreateFixture("valid-two-turn-stale");
        OpenVinoWorkerClient client = CreateClient(fixture.Root);
        await using OpenVinoConversation conversation = await client.StartSessionAsync(
            StartSession(), CancellationToken.None);
        List<string> text = [];

        IOpenVinoEvent first = await conversation.PromptAsync(
            Prompt(conversation.SessionId, "first"),
            new ImmediateProgress<TokenEvent>(token => text.Add(token.Text)),
            CancellationToken.None);
        IOpenVinoEvent second = await conversation.PromptAsync(
            Prompt(conversation.SessionId, "second"),
            new ImmediateProgress<TokenEvent>(token => text.Add(token.Text)),
            CancellationToken.None);

        Assert.IsInstanceOfType<TurnCompletedEvent>(first);
        Assert.IsInstanceOfType<TurnCompletedEvent>(second);
        CollectionAssert.AreEqual(ExpectedText, text);
    }

    [TestMethod]
    public async Task StopIsIdempotentPartialSuccessAndNextPromptStillRuns()
    {
        await using FixtureRun fixture = CreateFixture("partial-stop-next");
        OpenVinoWorkerClient client = CreateClient(fixture.Root);
        await using OpenVinoConversation conversation = await client.StartSessionAsync(
            StartSession(), CancellationToken.None);
        Task<IOpenVinoEvent> active = conversation.PromptAsync(
            Prompt(conversation.SessionId, "stop-sensitive"),
            new ImmediateProgress<TokenEvent>(_ => { }),
            CancellationToken.None);
        await WaitUntilAsync(async () =>
        {
            await conversation.StopAsync(CancellationToken.None);
            return active.IsCompleted;
        });
        await conversation.StopAsync(CancellationToken.None);
        Assert.IsInstanceOfType<TurnCompletedEvent>(await active);

        IOpenVinoEvent next = await conversation.PromptAsync(
            Prompt(conversation.SessionId, "after-stop"),
            null,
            CancellationToken.None);
        Assert.IsInstanceOfType<TurnCompletedEvent>(next);
    }

    [TestMethod]
    public async Task SecondConcurrentPromptFailsInsteadOfQueueingAnotherTurn()
    {
        await using FixtureRun fixture = CreateFixture("partial-stop-next");
        OpenVinoWorkerClient client = CreateClient(fixture.Root);
        await using OpenVinoConversation conversation = await client.StartSessionAsync(
            StartSession(), CancellationToken.None);
        Task<IOpenVinoEvent> active = conversation.PromptAsync(
            Prompt(conversation.SessionId, "active"),
            null,
            CancellationToken.None);
        using CancellationTokenSource queuedDeadline =
            new(TimeSpan.FromMilliseconds(250));

        OpenVinoWorkerClientException error =
            await Assert.ThrowsExactlyAsync<OpenVinoWorkerClientException>(() =>
                conversation.PromptAsync(
                    Prompt(conversation.SessionId, "must-not-queue"),
                    null,
                    queuedDeadline.Token));

        Assert.AreEqual(OpenVinoSupportCode.RuntimeProtocolFailed, error.SupportCode);
        await WaitUntilAsync(async () =>
        {
            await conversation.StopAsync(CancellationToken.None);
            return active.IsCompleted;
        });
        _ = await active;
    }

    [TestMethod]
    public async Task SessionCancellationIsIdempotentAndLeavesNoFixtureProcess()
    {
        await using FixtureRun fixture = CreateFixture("session-cancel");
        OpenVinoWorkerClient client = CreateClient(fixture.Root);
        OpenVinoConversation conversation = await client.StartSessionAsync(
            StartSession(), CancellationToken.None);

        await conversation.CancelAsync(CancellationToken.None);
        await conversation.CancelAsync(CancellationToken.None);
        await conversation.DisposeAsync();

        await AssertNoFixtureProcessAsync();
    }

    [TestMethod]
    public async Task CallerCancellationOfActiveTurnCancelsOwningSession()
    {
        await using FixtureRun fixture = CreateFixture("active-cancel");
        OpenVinoWorkerClient client = CreateClient(fixture.Root);
        await using OpenVinoConversation conversation = await client.StartSessionAsync(
            StartSession(), CancellationToken.None);
        using CancellationTokenSource cancellation = new();

        OpenVinoWorkerClientException error =
            await Assert.ThrowsExactlyAsync<OpenVinoWorkerClientException>(() =>
                conversation.PromptAsync(
                    Prompt(conversation.SessionId, "cancel-active"),
                    new ImmediateProgress<TokenEvent>(_ => cancellation.Cancel()),
                    cancellation.Token));

        Assert.AreEqual(
            OpenVinoSupportCode.OperationCancelled,
            error.SupportCode,
            error.Message + " stderr=" + error.RetainedStandardError);
        await AssertNoFixtureProcessAsync();
    }

    [TestMethod]
    public async Task CleanupInventoryScenarioLeavesNoWorkerResidue()
    {
        await using FixtureRun fixture = CreateFixture("cleanup-inventory");
        OpenVinoWorkerClient client = CreateClient(fixture.Root);

        IOpenVinoEvent result = await client.InspectAsync(
            new StartInspectionCommand(Guid.NewGuid()),
            CancellationToken.None);

        Assert.IsInstanceOfType<InspectionCompletedEvent>(result);
        await AssertNoFixtureProcessAsync();
    }

    [TestMethod]
    [DataRow("wrong-protocol")]
    [DataRow("malformed-line")]
    [DataRow("oversized-line")]
    [DataRow("stdout-after-terminal")]
    [DataRow("parent-exit")]
    [DataRow("child-escape-attempt")]
    public async Task HostileInspectionFailsClosedWithoutSensitiveDiagnostics(
        string scenario)
    {
        const string secret = "prompt-or-path-must-not-leak";
        await using FixtureRun fixture = CreateFixture(scenario);
        OpenVinoWorkerClient client = CreateClient(fixture.Root);

        OpenVinoWorkerClientException error =
            await Assert.ThrowsExactlyAsync<OpenVinoWorkerClientException>(() =>
                client.InspectAsync(
                    new StartInspectionCommand(Guid.NewGuid()),
                    CancellationToken.None));

        Assert.IsTrue(error.SupportCode is
            OpenVinoSupportCode.RuntimeProtocolFailed or
            OpenVinoSupportCode.RuntimeIntegrityFailed);
        Assert.IsFalse(error.Message.Contains(secret, StringComparison.Ordinal));
        await AssertNoFixtureProcessAsync();
    }

    [TestMethod]
    public async Task StderrOverflowIsBoundedAndSanitized()
    {
        await using FixtureRun fixture = CreateFixture("stderr-overflow");
        OpenVinoWorkerClient client = CreateClient(
            fixture.Root,
            stderrBytes: 1024);

        OpenVinoWorkerClientException error =
            await Assert.ThrowsExactlyAsync<OpenVinoWorkerClientException>(() =>
                client.InspectAsync(
                    new StartInspectionCommand(Guid.NewGuid()),
                    CancellationToken.None));

        Assert.IsTrue(error.StandardErrorTruncated);
        Assert.IsLessThanOrEqualTo(1024, error.RetainedStandardError.Length);
        Assert.IsFalse(error.RetainedStandardError.Contains('\0'));
    }

    [TestMethod]
    [DataRow("startup-timeout", 100, 2000, 2000, 2000)]
    [DataRow("turn-timeout", 2000, 150, 2000, 2000)]
    [DataRow("idle-timeout", 2000, 2000, 150, 2000)]
    [DataRow("session-timeout", 2000, 2000, 2000, 150)]
    public async Task EveryDeadlineTerminatesTheOwningSession(
        string scenario,
        int startupMs,
        int turnMs,
        int idleMs,
        int sessionMs)
    {
        await using FixtureRun fixture = CreateFixture(scenario);
        OpenVinoWorkerClient client = CreateClient(
            fixture.Root,
            startupMs,
            turnMs,
            idleMs,
            sessionMs);

        OpenVinoWorkerClientException error;
        if (scenario == "startup-timeout")
        {
            error = await Assert.ThrowsExactlyAsync<OpenVinoWorkerClientException>(
                () => client.StartSessionAsync(StartSession(), CancellationToken.None));
        }
        else
        {
            await using OpenVinoConversation conversation =
                await client.StartSessionAsync(StartSession(), CancellationToken.None);
            if (scenario == "session-timeout")
            {
                await Task.Delay(250);
            }

            error = await Assert.ThrowsExactlyAsync<OpenVinoWorkerClientException>(
                () => conversation.PromptAsync(
                    Prompt(conversation.SessionId, "timeout"),
                    null,
                    CancellationToken.None));
        }

        Assert.AreEqual(OpenVinoSupportCode.RuntimeTimedOut, error.SupportCode);
        await AssertNoFixtureProcessAsync();
    }

    private static OpenVinoWorkerClient CreateClient(
        string root,
        int startupMs = 2000,
        int turnMs = 2000,
        int idleMs = 2000,
        int sessionMs = 5000,
        int stderrBytes = 4096)
    {
        OpenVinoWorkerInstallation installation = new(
            root,
            "GraniteEdgeAI.OpenVino.ProtocolTestWorker.exe",
            OpenVinoProtocol.OfficialProtocolId);
        OpenVinoWorkerClientOptions options = new(
            installation,
            TimeSpan.FromMilliseconds(startupMs),
            TimeSpan.FromMilliseconds(turnMs),
            TimeSpan.FromMilliseconds(idleMs),
            TimeSpan.FromMilliseconds(sessionMs),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            stderrBytes,
            OpenVinoProtocol.MaximumLineBytes);
        return new OpenVinoWorkerClient(options);
    }

    private static StartSessionCommand StartSession() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Digest,
        new OpenVinoDeviceRequest("CPU"),
        new OpenVinoGenerationLimits(1024, 32));

    private static PromptCommand Prompt(Guid sessionId, string prompt) =>
        new(sessionId, Guid.NewGuid(), prompt, 8);

    private static FixtureRun CreateFixture(string scenario) =>
        new(s_publishedFixture ?? throw new InvalidOperationException(), scenario);

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(3);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.Fail("The expected process condition was not reached.");
    }

    private static async Task AssertNoFixtureProcessAsync()
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(3);
        while (DateTimeOffset.UtcNow < deadline)
        {
            Process[] processes = Process.GetProcessesByName(
                "GraniteEdgeAI.OpenVino.ProtocolTestWorker");
            try
            {
                if (processes.Length == 0)
                {
                    return;
                }
            }
            finally
            {
                foreach (Process process in processes)
                {
                    process.Dispose();
                }
            }

            await Task.Delay(25);
        }

        Assert.Fail("An OpenVINO protocol fixture process remained after cleanup.");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null &&
            !File.Exists(Path.Combine(current.FullName, "global.json")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException();
    }

    private sealed class FixtureRun : IAsyncDisposable
    {
        internal FixtureRun(string publishedRoot, string scenario)
        {
            Root = Path.Combine(Path.GetTempPath(), "GraniteEdgeAI-OpenVinoRun", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            foreach (string file in Directory.EnumerateFiles(publishedRoot))
            {
                File.Copy(file, Path.Combine(Root, Path.GetFileName(file)));
            }

            File.WriteAllText(Path.Combine(Root, "scenario.txt"), scenario);
        }

        internal string Root { get; }

        public ValueTask DisposeAsync()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class ImmediateProgress<T>(Action<T> action) : IProgress<T>
    {
        public void Report(T value) => action(value);
    }
}
