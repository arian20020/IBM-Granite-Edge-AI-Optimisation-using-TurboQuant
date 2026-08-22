using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Runtime;
using GraniteEdgeAI.ModelInspection.Contracts;
using GraniteEdgeAI.ModelInspection.WorkerClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection.Runtime;

[TestClass]
public sealed class WorkerProcessLlamaModelProbeTests
{
    [TestMethod]
    public async Task InspectAsync_ExecutesMappedCommandAndReturnsEvidence()
    {
        CapturingWorkerClient client = new(command =>
            RuntimeTestData.CreateClientResult(
                RuntimeTestData.CreateTerminal(
                    requestId: command.RequestId)));
        WorkerProcessLlamaModelProbe probe = CreateProbe(client);

        ModelInspectionProbeResult result = await probe.InspectAsync(
            RuntimeTestData.CreateRequest(),
            progress: null,
            CancellationToken.None);

        Assert.AreEqual(ModelInspectionProbeStatus.Completed, result.Status);
        Assert.IsNotNull(result.Evidence);
        Assert.AreEqual(1, client.InvocationCount);
        Assert.IsNotNull(client.Command);
        Assert.AreEqual(RuntimeTestData.RequestId, client.Command.RequestId);
        Assert.AreEqual(RuntimeTestData.ModelPath, client.Command.ModelPath);
        Assert.AreEqual(321, client.Command.ParentProcessId);
        Assert.AreEqual(
            RuntimeTestData.FixedUtcTime,
            client.Command.ParentProcessStartTimeUtc);
    }

    [TestMethod]
    public async Task InspectAsync_MapsFiveStageProgressInReportedOrder()
    {
        CapturingWorkerClient client = new(
            command => RuntimeTestData.CreateClientResult(
                RuntimeTestData.CreateTerminal(requestId: command.RequestId)),
            (command, progress) =>
            {
                foreach (WorkerStage stage in Enum.GetValues<WorkerStage>())
                {
                    progress?.Report(CreateProgress(
                        command.RequestId,
                        stage,
                        WorkerStageStatus.Active,
                        (int)stage - 1));
                    progress?.Report(CreateProgress(
                        command.RequestId,
                        stage,
                        WorkerStageStatus.Completed,
                        (int)stage));
                }
            });
        WorkerProcessLlamaModelProbe probe = CreateProbe(client);
        List<ModelInspectionProgress> progress = [];

        ModelInspectionProbeResult result = await probe.InspectAsync(
            RuntimeTestData.CreateRequest(),
            new DelegatingProgress<ModelInspectionProgress>(progress.Add),
            CancellationToken.None);

        Assert.AreEqual(ModelInspectionProbeStatus.Completed, result.Status);
        Assert.HasCount(10, progress);
        CollectionAssert.AreEqual(
            Enum.GetValues<ModelInspectionStage>()
                .SelectMany(stage => new[] { stage, stage })
                .ToArray(),
            progress.Select(update => update.Stage).ToArray());
        CollectionAssert.AreEqual(
            Enum.GetValues<ModelInspectionStage>()
                .SelectMany(_ => new[]
                {
                    ModelInspectionStageStatus.Active,
                    ModelInspectionStageStatus.Completed
                })
                .ToArray(),
            progress.Select(update => update.StageStatus).ToArray());
        CollectionAssert.AreEqual(
            Enumerable.Range(0, 5)
                .SelectMany(value => new[] { value, value + 1 })
                .ToArray(),
            progress.Select(update => update.CompletedStageCount).ToArray());
    }

    [TestMethod]
    public async Task InspectAsync_PreCancelledRequestPropagatesWithoutStartingWorker()
    {
        CapturingWorkerClient client = new(_ => throw new InvalidOperationException(
            "The worker must not start."));
        WorkerProcessLlamaModelProbe probe = CreateProbe(client);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            probe.InspectAsync(
                RuntimeTestData.CreateRequest(),
                progress: null,
                cancellation.Token));

        Assert.AreEqual(0, client.InvocationCount);
    }

    [TestMethod]
    public async Task InspectAsync_ClientExceptionBecomesPrivacySafeFailure()
    {
        const string secret = @"C:\private\granite.gguf";
        CapturingWorkerClient client = new(_ =>
            throw new InvalidOperationException(
                $"request {RuntimeTestData.RequestId} {secret}"));
        WorkerProcessLlamaModelProbe probe = CreateProbe(client);

        ModelInspectionProbeResult result = await probe.InspectAsync(
            RuntimeTestData.CreateRequest(),
            progress: null,
            CancellationToken.None);

        Assert.AreEqual(
            ModelInspectionProbeStatus.OperationalFailure,
            result.Status);
        Assert.IsNotNull(result.Failure);
        Assert.AreEqual("MI-OP-WORKER-CLIENT", result.Failure.Code);
        StringAssert.DoesNotContain(
            result.Failure.TechnicalDetail,
            secret,
            StringComparison.Ordinal);
        StringAssert.DoesNotContain(
            result.Failure.TechnicalDetail,
            RuntimeTestData.RequestId.ToString(),
            StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task InspectAsync_RequestIdentityFailureBecomesPrivacySafeFailure()
    {
        const string secret = @"C:\private\granite.gguf";
        CapturingWorkerClient client = new(_ =>
            throw new InvalidOperationException("The worker must not start."));
        WorkerProcessLlamaModelProbe probe = new(
            client,
            new WorkerRequestMapper(
                () => RuntimeTestData.RequestId,
                () => throw new InvalidOperationException(
                    $"request {RuntimeTestData.RequestId} {secret}")),
            new WorkerResultMapper());

        ModelInspectionProbeResult result = await probe.InspectAsync(
            RuntimeTestData.CreateRequest(),
            progress: null,
            CancellationToken.None);

        Assert.AreEqual(
            ModelInspectionProbeStatus.OperationalFailure,
            result.Status);
        Assert.IsNotNull(result.Failure);
        Assert.AreEqual("MI-OP-WORKER-CLIENT", result.Failure.Code);
        StringAssert.DoesNotContain(
            result.Failure.TechnicalDetail,
            secret,
            StringComparison.Ordinal);
        StringAssert.DoesNotContain(
            result.Failure.TechnicalDetail,
            RuntimeTestData.RequestId.ToString(),
            StringComparison.Ordinal);
        Assert.AreEqual(0, client.InvocationCount);
    }

    [TestMethod]
    public async Task InspectAsync_WrongProgressRequestIdFailsClosed()
    {
        List<ModelInspectionProgress> observed = [];
        CapturingWorkerClient client = new(
            command => RuntimeTestData.CreateClientResult(
                RuntimeTestData.CreateTerminal(requestId: command.RequestId)),
            (_, progress) => progress?.Report(CreateProgress(
                Guid.Parse("ecde37fb-20a9-46a3-a67a-3b28319ddc25"),
                WorkerStage.CheckModelPackage,
                WorkerStageStatus.Active,
                completedStageCount: 0)));
        WorkerProcessLlamaModelProbe probe = CreateProbe(client);

        ModelInspectionProbeResult result = await probe.InspectAsync(
            RuntimeTestData.CreateRequest(),
            new DelegatingProgress<ModelInspectionProgress>(observed.Add),
            CancellationToken.None);

        Assert.AreEqual(
            ModelInspectionProbeStatus.OperationalFailure,
            result.Status);
        Assert.AreEqual("MI-OP-WORKER-CLIENT", result.Failure?.Code);
        Assert.HasCount(0, observed);
    }

    [TestMethod]
    public async Task InspectAsync_NullRequestThrowsBeforeStartingWorker()
    {
        CapturingWorkerClient client = new(_ => throw new InvalidOperationException(
            "The worker must not start."));
        WorkerProcessLlamaModelProbe probe = CreateProbe(client);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() =>
            probe.InspectAsync(
                null!,
                progress: null,
                CancellationToken.None));

        Assert.AreEqual(0, client.InvocationCount);
    }

    private static WorkerProcessLlamaModelProbe CreateProbe(
        IInspectionWorkerClient client)
    {
        return new WorkerProcessLlamaModelProbe(
            client,
            new WorkerRequestMapper(
                () => RuntimeTestData.RequestId,
                () => new ParentProcessIdentity(
                    321,
                    RuntimeTestData.FixedUtcTime)),
            new WorkerResultMapper());
    }

    private static WorkerProgressMessage CreateProgress(
        Guid requestId,
        WorkerStage stage,
        WorkerStageStatus status,
        int completedStageCount)
    {
        return new WorkerProgressMessage
        {
            ProtocolVersion = WorkerProtocol.Version,
            MessageType = WorkerMessageKind.Progress,
            RequestId = requestId,
            Stage = stage,
            StageStatus = status,
            CompletedStageCount = completedStageCount,
            TotalStageCount = 5,
            StageFraction = null
        };
    }

    private sealed class CapturingWorkerClient : IInspectionWorkerClient
    {
        private readonly Func<WorkerStartInspectionCommand,
            WorkerClientResult> _resultFactory;
        private readonly Action<WorkerStartInspectionCommand,
            IProgress<WorkerProgressMessage>?>? _reportProgress;

        internal CapturingWorkerClient(
            Func<WorkerStartInspectionCommand, WorkerClientResult> resultFactory,
            Action<WorkerStartInspectionCommand,
                IProgress<WorkerProgressMessage>?>? reportProgress = null)
        {
            _resultFactory = resultFactory;
            _reportProgress = reportProgress;
        }

        internal int InvocationCount { get; private set; }

        internal WorkerStartInspectionCommand? Command { get; private set; }

        public Task<WorkerClientResult> ExecuteAsync(
            WorkerStartInspectionCommand command,
            IProgress<WorkerProgressMessage>? progress,
            CancellationToken cancellationToken)
        {
            InvocationCount++;
            Command = command;
            _reportProgress?.Invoke(command, progress);
            return Task.FromResult(_resultFactory(command));
        }
    }

    private sealed class DelegatingProgress<T> : IProgress<T>
    {
        private readonly Action<T> _report;

        internal DelegatingProgress(Action<T> report)
        {
            _report = report;
        }

        public void Report(T value)
        {
            _report(value);
        }
    }
}
