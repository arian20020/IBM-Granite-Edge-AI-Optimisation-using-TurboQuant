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
    public async Task EngineConstructionStartsOnlyAfterHelloAndStartHandshake()
    {
        WorkerStartInspectionCommand start = CreateStart();
        using ControllableInputStream input = new();
        using MemoryStream output = new();
        using MemoryStream error = new();
        bool factoryObservedHello = false;
        input.SupplyLine(WorkerProtocolJson.Serialize(start));
        input.Complete();

        await using WorkerHost host = new(
            input,
            output,
            error,
            () =>
            {
                factoryObservedHello = ParseMessages(output.ToArray())
                    .FirstOrDefault() is WorkerHelloMessage;
                return new CompletedEngine();
            },
            new NeverLostParentMonitor(),
            workerProcessId: 1234,
            workerVersion: "1.0.0");

        int exitCode = await host.RunAsync(CancellationToken.None)
            .ConfigureAwait(false);

        Assert.AreEqual(WorkerExitCodes.Completed, exitCode);
        Assert.IsTrue(
            factoryObservedHello,
            "The protocol hello must be flushed before heavy engine construction starts.");
    }

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
    public async Task CompletedEngineWritesProgressBeforeOneCompletedTerminal()
    {
        WorkerStartInspectionCommand start = CreateStart();
        HostRun run = await RunHostAsync(
            [WorkerProtocolJson.Serialize(start)],
            new CompletedEngine(),
            new NeverLostParentMonitor()).ConfigureAwait(false);

        Assert.AreEqual(WorkerExitCodes.Completed, run.ExitCode);
        Assert.AreEqual(string.Empty, run.StandardError);
        Assert.IsTrue(run.StandardOutput.EndsWith('\n'));
        Assert.IsInstanceOfType<WorkerHelloMessage>(run.Messages[0]);
        Assert.IsInstanceOfType<WorkerStartedMessage>(run.Messages[1]);

        WorkerProgressMessage[] progress = run.Messages
            .Skip(2)
            .Take(run.Messages.Count - 3)
            .Select(message =>
                Assert.IsInstanceOfType<WorkerProgressMessage>(message))
            .ToArray();
        Assert.AreEqual(3, progress.Length);

        Assert.AreEqual(start.RequestId, progress[0].RequestId);
        Assert.AreEqual(WorkerStage.CheckModelPackage, progress[0].Stage);
        Assert.AreEqual(WorkerStageStatus.Active, progress[0].StageStatus);
        Assert.AreEqual(0, progress[0].CompletedStageCount);
        Assert.AreEqual(5, progress[0].TotalStageCount);
        Assert.AreEqual(0.25, progress[0].StageFraction);

        Assert.AreEqual(start.RequestId, progress[1].RequestId);
        Assert.AreEqual(WorkerStage.CheckModelPackage, progress[1].Stage);
        Assert.AreEqual(WorkerStageStatus.Completed, progress[1].StageStatus);
        Assert.AreEqual(1, progress[1].CompletedStageCount);
        Assert.AreEqual(5, progress[1].TotalStageCount);
        Assert.AreEqual(1, progress[1].StageFraction);

        Assert.AreEqual(start.RequestId, progress[2].RequestId);
        Assert.AreEqual(WorkerStage.ReadModelConfiguration, progress[2].Stage);
        Assert.AreEqual(WorkerStageStatus.Active, progress[2].StageStatus);
        Assert.AreEqual(1, progress[2].CompletedStageCount);
        Assert.AreEqual(5, progress[2].TotalStageCount);
        Assert.AreEqual(0.5, progress[2].StageFraction);

        for (int index = 1; index < progress.Length; index++)
        {
            Assert.IsTrue(progress[index].Stage >= progress[index - 1].Stage);
            Assert.IsTrue(
                progress[index].CompletedStageCount >=
                progress[index - 1].CompletedStageCount);

            if (progress[index].Stage == progress[index - 1].Stage &&
                progress[index - 1].StageFraction is double previousFraction &&
                progress[index].StageFraction is double currentFraction)
            {
                Assert.IsTrue(currentFraction >= previousFraction);
            }
        }

        WorkerCompletedMessage terminal =
            Assert.IsInstanceOfType<WorkerCompletedMessage>(run.Messages[^1]);
        Assert.AreEqual(1, run.Messages.OfType<WorkerCompletedMessage>().Count());
        Assert.AreEqual(start.RequestId, terminal.RequestId);
        Assert.AreEqual(
            WorkerCompletionStatus.Completed,
            terminal.CompletionStatus);
        Assert.IsNotNull(terminal.Evidence);
        Assert.IsNull(terminal.OperationalFailure);
    }

    [TestMethod]
    public async Task EngineExceptionMapsToFixedControlledFailureWithoutDetailLeak()
    {
        WorkerStartInspectionCommand start = CreateStart();
        ThrowingEngine engine = new(start.ModelPath);
        HostRun run = await RunHostAsync(
            [WorkerProtocolJson.Serialize(start)],
            engine,
            new NeverLostParentMonitor()).ConfigureAwait(false);

        Assert.AreEqual(WorkerExitCodes.OperationalFailure, run.ExitCode);
        WorkerCompletedMessage[] terminals = run.Messages
            .OfType<WorkerCompletedMessage>()
            .ToArray();
        Assert.HasCount(1, terminals);
        WorkerCompletedMessage terminal = terminals[0];
        Assert.AreEqual(
            WorkerCompletionStatus.OperationalFailure,
            terminal.CompletionStatus);
        Assert.IsNull(terminal.Evidence);
        Assert.AreEqual(
            "MI-OP-ENGINE-FAILED",
            terminal.OperationalFailure?.Code);
        Assert.AreEqual(
            "The model inspection engine could not produce reliable evidence.",
            terminal.OperationalFailure?.Message);
        Assert.AreEqual(string.Empty, run.StandardError);

        string retainedOutput = run.StandardOutput + run.StandardError;
        Assert.IsFalse(retainedOutput.Contains(start.ModelPath, StringComparison.Ordinal));
        Assert.IsFalse(retainedOutput.Contains(ThrowingEngine.Sentinel, StringComparison.Ordinal));
        Assert.IsFalse(retainedOutput.Contains(nameof(InvalidOperationException), StringComparison.Ordinal));
        Assert.IsFalse(retainedOutput.Contains(nameof(ThrowingEngine), StringComparison.Ordinal));
        Assert.IsFalse(retainedOutput.Contains("   at ", StringComparison.Ordinal));
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
    public async Task MismatchedCancelWritesProtocolFailureAndNoTerminal()
    {
        WorkerStartInspectionCommand start = CreateStart();
        WorkerCancelInspectionCommand cancel = new()
        {
            ProtocolVersion = WorkerProtocol.Version,
            CommandType = WorkerCommandKind.CancelInspection,
            RequestId = Guid.NewGuid()
        };
        CancellationAwareEngine engine = new();

        HostRun run = await RunHostAsync(
            [
                WorkerProtocolJson.Serialize(start),
                WorkerProtocolJson.Serialize(cancel)
            ],
            engine,
            new NeverLostParentMonitor()).ConfigureAwait(false);

        Assert.AreEqual(WorkerExitCodes.ProtocolFailure, run.ExitCode);
        Assert.IsInstanceOfType<WorkerHelloMessage>(run.Messages[0]);
        Assert.IsInstanceOfType<WorkerStartedMessage>(run.Messages[1]);
        Assert.HasCount(0, run.Messages.OfType<WorkerCompletedMessage>().ToArray());
        Assert.AreEqual("MI-WORKER-PROTOCOL-FAILURE\n", run.StandardError);
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
    public async Task EndOfCommandStreamCancelsEngineAndWritesCancelledTerminal()
    {
        WorkerStartInspectionCommand start = CreateStart();
        CancellationAwareEngine engine = new();

        HostRun run = await RunHostAsync(
            [WorkerProtocolJson.Serialize(start)],
            engine,
            new NeverLostParentMonitor()).ConfigureAwait(false);

        Assert.IsTrue(engine.CancellationObserved);
        AssertCancelledAfterProgress(run, start.RequestId);
    }

    [TestMethod]
    public async Task ParentLossCancelsEngineAndWritesCancelledTerminal()
    {
        WorkerStartInspectionCommand start = CreateStart();
        using ControllableInputStream input = new();
        input.SupplyLine(WorkerProtocolJson.Serialize(start));
        CancellationAwareEngine engine = new();
        ParentLostMonitor parentMonitor = new();

        Task<HostRun> pendingRun = RunHostAsync(
            input,
            engine,
            parentMonitor,
            CancellationToken.None);
        await parentMonitor.MonitoringStarted.WaitAsync(TimeSpan.FromSeconds(1));
        await input.ReadWaiting.WaitAsync(TimeSpan.FromSeconds(1));
        parentMonitor.ReportParentLost();
        HostRun run = await pendingRun.ConfigureAwait(false);

        Assert.IsTrue(engine.CancellationObserved);
        AssertCancelledAfterProgress(run, start.RequestId);
    }

    [TestMethod]
    public async Task ExternalCancellationWritesFixedOperationalError()
    {
        using ControllableInputStream input = new();
        using CancellationTokenSource cancellation = new();
        Task<HostRun> pendingRun = RunHostAsync(
            input,
            new CancellationAwareEngine(),
            new NeverLostParentMonitor(),
            cancellation.Token);
        await input.ReadWaiting.WaitAsync(TimeSpan.FromSeconds(1));

        cancellation.Cancel();
        HostRun run = await pendingRun.ConfigureAwait(false);

        Assert.AreEqual(WorkerExitCodes.OperationalFailure, run.ExitCode);
        Assert.HasCount(0, run.Messages.OfType<WorkerCompletedMessage>().ToArray());
        Assert.AreEqual("MI-WORKER-OPERATION-CANCELLED\n", run.StandardError);
        string retainedOutput = run.StandardOutput + run.StandardError;
        Assert.IsFalse(retainedOutput.Contains(@"C:\Models", StringComparison.Ordinal));
        Assert.IsFalse(retainedOutput.Contains("granite.gguf", StringComparison.Ordinal));
        Assert.IsFalse(retainedOutput.Contains("Granite 4.1 3B", StringComparison.Ordinal));
        Assert.IsFalse(retainedOutput.Contains("Exception", StringComparison.Ordinal));
        Assert.IsFalse(retainedOutput.Contains("   at ", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task TerminalCoordinatorAllowsOnlyOneWinner()
    {
        const int rounds = 100;
        for (int round = 0; round < rounds; round++)
        {
            WorkerTerminalCoordinator coordinator = new();
            using Barrier release = new(participantCount: 4);
            Task<bool> completion =
                Task.Run(() => RaceTerminal(release, coordinator));
            Task<bool> cooperativeCancellation =
                Task.Run(() => RaceTerminal(release, coordinator));
            Task<bool> parentLoss =
                Task.Run(() => RaceTerminal(release, coordinator));
            Task<bool>[] contenders =
            [
                completion,
                cooperativeCancellation,
                parentLoss
            ];

            release.SignalAndWait();
            bool[] results = await Task.WhenAll(contenders).ConfigureAwait(false);

            Assert.AreEqual(
                1,
                results.Count(result => result),
                $"Round {round} must have exactly one terminal winner.");
        }
    }

    private static async Task<HostRun> RunHostAsync(
        IReadOnlyList<byte[]> commands,
        IWorkerInspectionEngine engine,
        IParentProcessMonitor parentMonitor)
    {
        using ControllableInputStream input = new();
        foreach (byte[] command in commands)
        {
            input.SupplyLine(command);
        }

        input.Complete();
        return await RunHostAsync(
                input,
                engine,
                parentMonitor,
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    private static async Task<HostRun> RunHostAsync(
        Stream input,
        IWorkerInspectionEngine engine,
        IParentProcessMonitor parentMonitor,
        CancellationToken cancellationToken)
    {
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

        int exitCode = await host.RunAsync(cancellationToken)
            .ConfigureAwait(false);
        byte[] standardOutputBytes = output.ToArray();
        List<object> messages = ParseMessages(standardOutputBytes);
        string standardOutput = Encoding.UTF8.GetString(standardOutputBytes);
        string standardError = Encoding.UTF8.GetString(error.ToArray());
        return new HostRun(exitCode, messages, standardOutput, standardError);
    }

    private static List<object> ParseMessages(byte[] output)
    {
        if (output.Length > 0 && output[^1] != (byte)'\n')
        {
            throw new AssertFailedException(
                "Worker stdout ended with an unterminated protocol frame.");
        }

        List<object> messages = [];
        foreach (ReadOnlyMemory<byte> line in SplitLines(output))
        {
            messages.Add(WorkerProtocolJson.DeserializeMessage(line.Span));
        }

        return messages;
    }

    private static void AssertCancelledAfterProgress(
        HostRun run,
        Guid requestId)
    {
        Assert.AreEqual(WorkerExitCodes.Cancelled, run.ExitCode);
        WorkerCompletedMessage[] terminals = run.Messages
            .OfType<WorkerCompletedMessage>()
            .ToArray();
        Assert.HasCount(1, terminals);
        WorkerCompletedMessage terminal = terminals[0];
        Assert.AreEqual(requestId, terminal.RequestId);
        Assert.AreEqual(
            WorkerCompletionStatus.Cancelled,
            terminal.CompletionStatus);
        Assert.IsNull(terminal.Evidence);
        Assert.IsNull(terminal.OperationalFailure);

        int terminalIndex = run.Messages.IndexOf(terminal);
        int[] progressIndexes = run.Messages
            .Select((message, index) => (message, index))
            .Where(item => item.message is WorkerProgressMessage)
            .Select(item => item.index)
            .ToArray();
        Assert.IsTrue(progressIndexes.Length >= 1);
        Assert.IsTrue(progressIndexes.All(index => index < terminalIndex));
        Assert.AreEqual(string.Empty, run.StandardError);
    }

    private static bool RaceTerminal(
        Barrier release,
        WorkerTerminalCoordinator coordinator)
    {
        release.SignalAndWait();
        return coordinator.TryBeginTerminal();
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

    private static WorkerProgressMessage CreateProgress(
        Guid requestId,
        WorkerStage stage,
        WorkerStageStatus status,
        int completedStageCount,
        double? stageFraction) => new()
    {
        ProtocolVersion = WorkerProtocol.Version,
        MessageType = WorkerMessageKind.Progress,
        RequestId = requestId,
        Stage = stage,
        StageStatus = status,
        CompletedStageCount = completedStageCount,
        TotalStageCount = 5,
        StageFraction = stageFraction
    };

    private static WorkerInspectionEvidence CreateValidEvidence()
    {
        DateTimeOffset timestamp = new(
            2026,
            8,
            5,
            12,
            0,
            0,
            TimeSpan.Zero);
        return new WorkerInspectionEvidence
        {
            Runtime = new WorkerRuntimeIdentity
            {
                WorkerVersion = "1.0.0",
                ProtocolVersion = WorkerProtocol.Version,
                RuntimeProfile = WorkerProtocol.RuntimeProfile,
                LLamaSharpVersion = "0.27.0",
                BackendPackageVersion = "0.27.0",
                MappedLlamaCppCommit = "mapped-commit",
                NativeLibraryName = "llama.dll",
                ProcessArchitecture = "X64",
                InspectionMode = "VocabOnly",
                UsesCuda = false,
                UsesVulkan = false,
                GpuLayerCount = 0
            },
            ModelFile = new WorkerModelFileEvidence
            {
                FileName = "granite.gguf",
                CanonicalPathSha256 = new string('C', 64),
                LengthBefore = 100,
                LengthAfter = 100,
                LastWriteTimeBeforeUtc = timestamp,
                LastWriteTimeAfterUtc = timestamp,
                Sha256Before = new string('A', 64),
                Sha256After = new string('A', 64),
                IntegrityPreserved = true
            },
            Configuration = new WorkerModelConfigurationEvidence
            {
                Architecture = "granite",
                ModelName = "Granite 4.1 3B",
                FileType = 15,
                QuantisationVersion = 2,
                TokenizerModel = "BPE",
                DeclaredContextLength = 131_072,
                EmbeddingSize = 4096,
                LayerCount = 32,
                AttentionHeadCount = 32,
                KvHeadCount = 8,
                ParameterCount = 3_000_000_000
            },
            Tokenizer = new WorkerTokenizerEvidence
            {
                VocabularyCount = 32_000,
                VocabularyType = "BPE",
                TokenizerSmokePassed = true,
                TokenizerSmokeTokenCount = 4,
                KnownSpecialTokenIds = new Dictionary<string, int>(
                    StringComparer.Ordinal)
                {
                    ["bos"] = 1,
                    ["eos"] = 2
                }
            },
            ChatTemplate = new WorkerChatTemplateEvidence
            {
                Present = true,
                LengthCharacters = 128,
                Sha256 = new string('D', 64)
            },
            Observations =
            [
                new WorkerObservation
                {
                    Code = "MI-WORKER-TEST",
                    TechnicalCategory = "ModelInspection",
                    TechnicalDetail = "Worker host test evidence is complete."
                }
            ]
        };
    }

    private sealed class NeverLostParentMonitor : IParentProcessMonitor
    {
        public Task MonitorAsync(
            WorkerStartInspectionCommand command,
            CancellationToken cancellationToken) =>
            Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }

    private sealed class ParentLostMonitor : IParentProcessMonitor
    {
        private readonly TaskCompletionSource<bool> _monitoringStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _parentLost = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task MonitoringStarted => _monitoringStarted.Task;

        public async Task MonitorAsync(
            WorkerStartInspectionCommand command,
            CancellationToken cancellationToken)
        {
            _monitoringStarted.TrySetResult(true);
            await _parentLost.Task
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        public void ReportParentLost()
        {
            _parentLost.TrySetResult(true);
        }
    }

    private sealed class CompletedEngine : IWorkerInspectionEngine
    {
        public Task<WorkerEngineResult> InspectAsync(
            WorkerStartInspectionCommand command,
            IProgress<WorkerProgressMessage>? progress,
            CancellationToken cancellationToken)
        {
            progress?.Report(CreateProgress(
                command.RequestId,
                WorkerStage.CheckModelPackage,
                WorkerStageStatus.Active,
                completedStageCount: 0,
                stageFraction: 0.25));
            progress?.Report(CreateProgress(
                command.RequestId,
                WorkerStage.CheckModelPackage,
                WorkerStageStatus.Completed,
                completedStageCount: 1,
                stageFraction: 1));
            progress?.Report(CreateProgress(
                command.RequestId,
                WorkerStage.ReadModelConfiguration,
                WorkerStageStatus.Active,
                completedStageCount: 1,
                stageFraction: 0.5));
            return Task.FromResult(
                WorkerEngineResult.Completed(CreateValidEvidence()));
        }
    }

    private sealed class ThrowingEngine(string modelPath) :
        IWorkerInspectionEngine
    {
        internal const string Sentinel = "WORKER-ENGINE-SECRET-SENTINEL";

        public Task<WorkerEngineResult> InspectAsync(
            WorkerStartInspectionCommand command,
            IProgress<WorkerProgressMessage>? progress,
            CancellationToken cancellationToken) =>
            Task.FromException<WorkerEngineResult>(
                new InvalidOperationException(
                    $"Inspection failed for {modelPath}; {Sentinel}."));
    }

    private sealed class CancellationAwareEngine : IWorkerInspectionEngine
    {
        private int _cancellationObserved;

        public bool CancellationObserved =>
            Volatile.Read(ref _cancellationObserved) != 0;

        public async Task<WorkerEngineResult> InspectAsync(
            WorkerStartInspectionCommand command,
            IProgress<WorkerProgressMessage>? progress,
            CancellationToken cancellationToken)
        {
            progress?.Report(CreateProgress(
                command.RequestId,
                WorkerStage.CheckModelPackage,
                WorkerStageStatus.Active,
                completedStageCount: 0,
                stageFraction: 0.5));

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)
                    .ConfigureAwait(false);
                throw new AssertFailedException(
                    "The cancellation-aware engine unexpectedly completed.");
            }
            catch (OperationCanceledException)
            {
                Interlocked.Exchange(ref _cancellationObserved, 1);
                throw;
            }
        }
    }

    private sealed class ControllableInputStream : Stream
    {
        private readonly object _sync = new();
        private readonly Queue<byte> _bytes = new();
        private readonly TaskCompletionSource<bool> _readWaiting = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private TaskCompletionSource<bool> _stateChanged = NewSignal();
        private bool _completed;

        public Task ReadWaiting => _readWaiting.Task;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public void SupplyLine(byte[] payload)
        {
            ArgumentNullException.ThrowIfNull(payload);
            TaskCompletionSource<bool> signal;
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_completed, this);
                foreach (byte value in payload)
                {
                    _bytes.Enqueue(value);
                }

                _bytes.Enqueue((byte)'\n');
                signal = _stateChanged;
                _stateChanged = NewSignal();
            }

            signal.TrySetResult(true);
        }

        public void Complete()
        {
            TaskCompletionSource<bool> signal;
            lock (_sync)
            {
                _completed = true;
                signal = _stateChanged;
            }

            signal.TrySetResult(true);
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            while (true)
            {
                Task stateChanged;
                lock (_sync)
                {
                    if (_bytes.Count > 0)
                    {
                        int count = Math.Min(buffer.Length, _bytes.Count);
                        for (int index = 0; index < count; index++)
                        {
                            buffer.Span[index] = _bytes.Dequeue();
                        }

                        return count;
                    }

                    if (_completed)
                    {
                        return 0;
                    }

                    _readWaiting.TrySetResult(true);
                    stateChanged = _stateChanged.Task;
                }

                await stateChanged
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        private static TaskCompletionSource<bool> NewSignal() => new(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed record HostRun(
        int ExitCode,
        List<object> Messages,
        string StandardOutput,
        string StandardError);
}
