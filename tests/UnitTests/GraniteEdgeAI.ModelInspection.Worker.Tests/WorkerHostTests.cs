using System.Text;
using GraniteEdgeAI.ModelInspection.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Worker.Tests;

/// <summary>
/// Specifies the production worker lifecycle through its standard-stream
/// boundary. Tests use in-memory streams and deterministic engine/parent seams.
/// </summary>
[TestClass]
public sealed class WorkerHostTests
{
    [TestMethod]
    public async Task UnavailableEngineWritesHelloStartedAndControlledFailure()
    {
        WorkerStartInspectionCommand start = CreateStart();
        HostRun run = await RunHostAsync(
            [WorkerProtocolJson.Serialize(start)],
            new UnavailableWorkerInspectionEngine(),
            new NeverLostParentMonitor()).ConfigureAwait(false);

        Assert.AreEqual(WorkerExitCodes.OperationalFailure, run.ExitCode);
        Assert.AreEqual(3, run.Messages.Count);
        Assert.IsInstanceOfType<WorkerHelloMessage>(run.Messages[0]);
        Assert.IsInstanceOfType<WorkerStartedMessage>(run.Messages[1]);
        WorkerCompletedMessage terminal =
            Assert.IsInstanceOfType<WorkerCompletedMessage>(run.Messages[2]);
        Assert.AreEqual(
            WorkerCompletionStatus.OperationalFailure,
            terminal.CompletionStatus);
        Assert.AreEqual(
            "MI-OP-ENGINE-NOT-CONFIGURED",
            terminal.OperationalFailure?.Code);
        Assert.AreEqual(string.Empty, run.StandardError);
    }

    [TestMethod]
    public async Task MalformedStartWritesHelloThenProtocolExitWithoutEcho()
    {
        byte[] untrusted = Encoding.UTF8.GetBytes("not-json");
        HostRun run = await RunHostAsync(
            [untrusted],
            new UnavailableWorkerInspectionEngine(),
            new NeverLostParentMonitor()).ConfigureAwait(false);

        Assert.AreEqual(WorkerExitCodes.ProtocolFailure, run.ExitCode);
        Assert.AreEqual(1, run.Messages.Count);
        Assert.IsInstanceOfType<WorkerHelloMessage>(run.Messages[0]);
        StringAssert.Contains(run.StandardError, "MI-WORKER-PROTOCOL-FAILURE");
        Assert.IsFalse(
            run.StandardError.Contains("not-json", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task MatchingCancelProducesCooperativeCancelledTerminal()
    {
        WorkerStartInspectionCommand start = CreateStart();
        WorkerCancelInspectionCommand cancel = new()
        {
            ProtocolVersion = WorkerProtocol.Version,
            CommandType = WorkerCommandKind.CancelInspection,
            RequestId = start.RequestId
        };
        HostRun run = await RunHostAsync(
            [
                WorkerProtocolJson.Serialize(start),
                WorkerProtocolJson.Serialize(cancel)
            ],
            new CancellationAwareEngine(),
            new NeverLostParentMonitor()).ConfigureAwait(false);

        Assert.AreEqual(WorkerExitCodes.Cancelled, run.ExitCode);
        WorkerCompletedMessage terminal =
            Assert.IsInstanceOfType<WorkerCompletedMessage>(run.Messages[^1]);
        Assert.AreEqual(
            WorkerCompletionStatus.Cancelled,
            terminal.CompletionStatus);
        Assert.AreEqual(1, run.Messages.OfType<WorkerCompletedMessage>().Count());
    }

    [TestMethod]
    public void TerminalCoordinatorAllowsOnlyOneWinner()
    {
        WorkerTerminalCoordinator coordinator = new();

        bool first = coordinator.TryBeginTerminal();
        bool second = coordinator.TryBeginTerminal();

        Assert.IsTrue(first);
        Assert.IsFalse(second);
    }

    private static async Task<HostRun> RunHostAsync(
        IReadOnlyList<byte[]> commands,
        IWorkerInspectionEngine engine,
        IParentProcessMonitor parentMonitor)
    {
        using MemoryStream input = new();
        foreach (byte[] command in commands)
        {
            await input.WriteAsync(command).ConfigureAwait(false);
            input.WriteByte((byte)'\n');
        }

        input.Position = 0;
        using MemoryStream output = new();
        using MemoryStream error = new();
        await using WorkerHost host = new(
            input,
            output,
            error,
            engine,
            parentMonitor,
            workerProcessId: 1234,
            workerVersion: "1.0.0");

        int exitCode = await host.RunAsync(CancellationToken.None)
            .ConfigureAwait(false);
        List<object> messages = ParseMessages(output.ToArray());
        string standardError = Encoding.UTF8.GetString(error.ToArray());
        return new HostRun(exitCode, messages, standardError);
    }

    private static List<object> ParseMessages(byte[] output)
    {
        List<object> messages = [];
        foreach (ReadOnlyMemory<byte> line in SplitLines(output))
        {
            messages.Add(WorkerProtocolJson.DeserializeMessage(line.Span));
        }

        return messages;
    }

    private static IEnumerable<ReadOnlyMemory<byte>> SplitLines(byte[] bytes)
    {
        int start = 0;
        for (int index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] != (byte)'\n')
            {
                continue;
            }

            yield return bytes.AsMemory(start, index - start);
            start = index + 1;
        }
    }

    private static WorkerStartInspectionCommand CreateStart()
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        return new WorkerStartInspectionCommand
        {
            ProtocolVersion = WorkerProtocol.Version,
            CommandType = WorkerCommandKind.StartInspection,
            RequestId = Guid.NewGuid(),
            ParentProcessId = 4321,
            ParentProcessStartTimeUtc = timestamp,
            ModelPath = @"C:\Models\granite.gguf",
            ExpectedFileIdentity = new WorkerExpectedFileIdentity
            {
                LengthBytes = 100,
                LastWriteTimeUtc = timestamp
            },
            QuickScan = new WorkerQuickScanSnapshot
            {
                Format = "GGUF",
                ModelName = "Granite 4.1 3B",
                Architecture = "granite",
                ParameterSizeLabel = "3B",
                Quantisation = "Q4_K_M",
                FileSizeBytes = 100,
                DeclaredContextLength = 131_072,
                GgufVersion = 3
            }
        };
    }

    private sealed class NeverLostParentMonitor : IParentProcessMonitor
    {
        public Task MonitorAsync(
            WorkerStartInspectionCommand command,
            CancellationToken cancellationToken) =>
            Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }

    private sealed class CancellationAwareEngine : IWorkerInspectionEngine
    {
        public async Task<WorkerEngineResult> InspectAsync(
            WorkerStartInspectionCommand command,
            IProgress<WorkerProgressMessage>? progress,
            CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)
                    .ConfigureAwait(false);
                throw new AssertFailedException(
                    "The cancellation-aware engine unexpectedly completed.");
            }
            catch (OperationCanceledException)
            {
                return WorkerEngineResult.Cancelled();
            }
        }
    }

    private sealed record HostRun(
        int ExitCode,
        List<object> Messages,
        string StandardError);
}
