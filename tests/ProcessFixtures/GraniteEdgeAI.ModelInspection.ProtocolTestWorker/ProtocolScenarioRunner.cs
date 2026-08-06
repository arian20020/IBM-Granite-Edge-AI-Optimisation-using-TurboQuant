using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using GraniteEdgeAI.ModelInspection.Contracts;
using GraniteEdgeAI.ModelInspection.Transport;

namespace GraniteEdgeAI.ModelInspection.ProtocolTestWorker;

/// <summary>
/// Runs deterministic abnormal protocol and process behaviours for integration
/// tests. This executable never opens a model file or starts a network listener.
/// </summary>
internal sealed class ProtocolScenarioRunner
{
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);
    private readonly Stream _input;
    private readonly Stream _output;
    private readonly Stream _error;
    private readonly FixtureMilestoneWriter _milestones;

    internal ProtocolScenarioRunner(
        Stream input,
        Stream output,
        Stream error)
    {
        _input = input;
        _output = output;
        _error = error;
        _milestones = new FixtureMilestoneWriter(error);
    }

    internal async Task<int> RunAsync(TestWorkerScenarioRequest request)
    {
        switch (request.Scenario)
        {
            case TestWorkerScenario.LaunchProbe:
                return await RunLaunchProbeAsync().ConfigureAwait(false);
            case TestWorkerScenario.ProbeUnrelatedHandle:
                return await RunHandleProbeAsync(request).ConfigureAwait(false);
            case TestWorkerScenario.ChildProcessWait:
            case TestWorkerScenario.NoHello:
            case TestWorkerScenario.HangBeforeHello:
                await HangAsync().ConfigureAwait(false);
                return 1;
            case TestWorkerScenario.CrashBeforeHello:
                Environment.FailFast("Fixture crash before hello.");
                return 1;
            case TestWorkerScenario.InvalidUtf8:
                await WriteRawAsync([0xC3, 0x28, (byte)'\n']).ConfigureAwait(false);
                return 0;
            case TestWorkerScenario.Utf8Bom:
                await WriteRawAsync([0xEF, 0xBB, 0xBF]).ConfigureAwait(false);
                await WriteHelloAsync().ConfigureAwait(false);
                return 0;
            case TestWorkerScenario.OversizedStdoutLine:
            case TestWorkerScenario.FloodStdout:
                await WriteRawAsync(new byte[WorkerProtocol.MaximumMessageBytes + 1])
                    .ConfigureAwait(false);
                await HangAsync().ConfigureAwait(false);
                return 1;
            case TestWorkerScenario.MalformedHello:
                await WriteRawLineAsync("{}").ConfigureAwait(false);
                return 0;
            case TestWorkerScenario.MalformedJson:
                await WriteRawLineAsync("{not-json}").ConfigureAwait(false);
                return 0;
            case TestWorkerScenario.DuplicateJsonProperty:
                await WriteRawLineAsync(
                    "{\"protocolVersion\":1,\"protocolVersion\":1," +
                    "\"messageType\":\"hello\"}")
                    .ConfigureAwait(false);
                return 0;
            case TestWorkerScenario.TextBeforeHello:
                await WriteRawLineAsync("fixture text before hello")
                    .ConfigureAwait(false);
                await WriteHelloAsync().ConfigureAwait(false);
                return 0;
            case TestWorkerScenario.WrongProtocolVersion:
                await WriteRawHelloAsync(protocolVersion: 2).ConfigureAwait(false);
                return 0;
            case TestWorkerScenario.WrongWorkerId:
                await WriteRawHelloAsync(workerId: "wrong-worker")
                    .ConfigureAwait(false);
                return 0;
            case TestWorkerScenario.WrongWorkerProcessId:
                await WriteRawHelloAsync(processId: Environment.ProcessId + 1000)
                    .ConfigureAwait(false);
                return 0;
            case TestWorkerScenario.WrongRuntimeProfile:
                await WriteRawHelloAsync(runtimeProfile: "wrong-profile")
                    .ConfigureAwait(false);
                return 0;
            case TestWorkerScenario.WrongArchitecture:
                await WriteRawHelloAsync(architecture: "ARM64")
                    .ConfigureAwait(false);
                return 0;
            default:
                return await RunConversationScenarioAsync(request.Scenario)
                    .ConfigureAwait(false);
        }
    }

    private async Task<int> RunConversationScenarioAsync(
        TestWorkerScenario scenario)
    {
        await WriteHelloAsync().ConfigureAwait(false);
        await _milestones.WriteAsync("FIXTURE:HELLO_WRITTEN")
            .ConfigureAwait(false);

        if (scenario == TestWorkerScenario.CrashAfterHello)
        {
            Environment.FailFast("Fixture crash after hello.");
        }

        if (scenario == TestWorkerScenario.HangAfterHello)
        {
            await HangAsync().ConfigureAwait(false);
        }

        WorkerStartInspectionCommand start = await ReadStartAsync()
            .ConfigureAwait(false);
        await _milestones.WriteAsync("FIXTURE:START_RECEIVED")
            .ConfigureAwait(false);

        if (scenario == TestWorkerScenario.CrashAfterStart)
        {
            Environment.FailFast("Fixture crash after start.");
        }

        if (scenario == TestWorkerScenario.HangAfterStart)
        {
            await WriteStartedAsync(start.RequestId).ConfigureAwait(false);
            await HangAsync().ConfigureAwait(false);
        }

        if (scenario == TestWorkerScenario.ProgressBeforeStarted)
        {
            await WriteProgressAsync(start.RequestId, WorkerStage.OpenModel, 0)
                .ConfigureAwait(false);
            await HangAsync().ConfigureAwait(false);
        }

        if (scenario == TestWorkerScenario.SpawnChildAndWait ||
            scenario == TestWorkerScenario.ExitRootWithLiveChild)
        {
            using Process child = ChildProcessScenario.StartWaitingChild();
            await _milestones.WriteAsync(
                    $"FIXTURE:CHILD_STARTED:{child.Id}")
                .ConfigureAwait(false);
            if (scenario == TestWorkerScenario.ExitRootWithLiveChild)
            {
                return 0;
            }

            await WriteStartedAsync(start.RequestId).ConfigureAwait(false);
            await HangAsync().ConfigureAwait(false);
        }

        if (scenario == TestWorkerScenario.WrongRequestId)
        {
            Guid wrong = Guid.NewGuid();
            await WriteStartedAsync(wrong).ConfigureAwait(false);
            await WriteControlledFailureAsync(wrong).ConfigureAwait(false);
            return 1;
        }

        await WriteStartedAsync(start.RequestId).ConfigureAwait(false);

        if (scenario == TestWorkerScenario.NonMonotonicProgress)
        {
            await WriteProgressAsync(start.RequestId, WorkerStage.LoadVocabulary, 2)
                .ConfigureAwait(false);
            await WriteProgressAsync(start.RequestId, WorkerStage.OpenModel, 1)
                .ConfigureAwait(false);
            await HangAsync().ConfigureAwait(false);
        }

        if (scenario == TestWorkerScenario.ExitWithoutTerminal)
        {
            return 0;
        }

        if (scenario == TestWorkerScenario.CooperativeCancellation ||
            scenario == TestWorkerScenario.IgnoreCancellation)
        {
            await ReadCancelAsync(start.RequestId).ConfigureAwait(false);
            await _milestones.WriteAsync("FIXTURE:CANCEL_RECEIVED")
                .ConfigureAwait(false);
            if (scenario == TestWorkerScenario.IgnoreCancellation)
            {
                await HangAsync().ConfigureAwait(false);
            }

            await WriteCancelledAsync(start.RequestId).ConfigureAwait(false);
            return 3;
        }

        if (scenario == TestWorkerScenario.FloodStderr)
        {
            byte[] flood = Enumerable.Repeat((byte)'E', 1_048_576).ToArray();
            await _error.WriteAsync(flood).ConfigureAwait(false);
            await _error.WriteAsync([0xC3, 0x28]).ConfigureAwait(false);
            await _error.FlushAsync().ConfigureAwait(false);
        }

        if (scenario == TestWorkerScenario.EchoEnvironmentKeys)
        {
            string keys = string.Join(
                ';',
                Environment.GetEnvironmentVariables().Keys
                    .Cast<object>()
                    .Select(static item => item.ToString() ?? string.Empty)
                    .OrderBy(static item => item, StringComparer.OrdinalIgnoreCase));
            await _milestones.WriteAsync("FIXTURE:ENV_KEYS:" + keys)
                .ConfigureAwait(false);
        }

        await WriteControlledFailureAsync(start.RequestId).ConfigureAwait(false);
        await _milestones.WriteAsync("FIXTURE:TERMINAL_WRITTEN")
            .ConfigureAwait(false);

        if (scenario == TestWorkerScenario.DuplicateTerminal)
        {
            await WriteControlledFailureAsync(start.RequestId).ConfigureAwait(false);
        }

        return scenario == TestWorkerScenario.TerminalExitMismatch ? 0 : 1;
    }

    private async Task<int> RunLaunchProbeAsync()
    {
        await WriteRawLineAsync("fixture-ready").ConfigureAwait(false);
        using StreamReader reader = CreateReader();
        _ = await reader.ReadLineAsync().ConfigureAwait(false);
        return 0;
    }

    private async Task<int> RunHandleProbeAsync(
        TestWorkerScenarioRequest request)
    {
        bool signaled = SetEvent(new IntPtr(request.NumericValue!.Value));
        await WriteRawLineAsync(
                signaled ? "handle-signaled" : "handle-unavailable")
            .ConfigureAwait(false);
        using StreamReader reader = CreateReader();
        _ = await reader.ReadLineAsync().ConfigureAwait(false);
        return 0;
    }

    private async Task<WorkerStartInspectionCommand> ReadStartAsync()
    {
        BoundedUtf8LineReader reader = new(
            _input,
            WorkerProtocol.MaximumMessageBytes);
        byte[] payload = await reader.ReadLineAsync(CancellationToken.None)
            .ConfigureAwait(false) ??
            throw new InvalidOperationException("Fixture expected startInspection.");
        object command = WorkerProtocolJson.DeserializeCommand(payload);
        return command as WorkerStartInspectionCommand ??
            throw new InvalidOperationException("Fixture expected startInspection.");
    }

    private async Task ReadCancelAsync(Guid requestId)
    {
        BoundedUtf8LineReader reader = new(
            _input,
            WorkerProtocol.MaximumMessageBytes);
        byte[] payload = await reader.ReadLineAsync(CancellationToken.None)
            .ConfigureAwait(false) ??
            throw new InvalidOperationException("Fixture expected cancelInspection.");
        object command = WorkerProtocolJson.DeserializeCommand(payload);
        if (command is not WorkerCancelInspectionCommand cancel ||
            cancel.RequestId != requestId)
        {
            throw new InvalidOperationException("Fixture received invalid cancellation.");
        }
    }

    private Task WriteHelloAsync() => WriteMessageAsync(new WorkerHelloMessage
    {
        ProtocolVersion = WorkerProtocol.Version,
        MessageType = WorkerMessageKind.Hello,
        WorkerId = WorkerProtocol.WorkerId,
        WorkerVersion = "fixture-1.0.0",
        WorkerProcessId = Environment.ProcessId,
        RuntimeProfile = WorkerProtocol.RuntimeProfile,
        ProcessArchitecture = "X64"
    });

    private Task WriteRawHelloAsync(
        int protocolVersion = WorkerProtocol.Version,
        string workerId = WorkerProtocol.WorkerId,
        int? processId = null,
        string runtimeProfile = WorkerProtocol.RuntimeProfile,
        string architecture = "X64") => WriteRawLineAsync(
            $"{{\"protocolVersion\":{protocolVersion}," +
            "\"messageType\":\"hello\"," +
            $"\"workerId\":\"{workerId}\"," +
            "\"workerVersion\":\"fixture-1.0.0\"," +
            $"\"workerProcessId\":{processId ?? Environment.ProcessId}," +
            $"\"runtimeProfile\":\"{runtimeProfile}\"," +
            $"\"processArchitecture\":\"{architecture}\"}}");

    private Task WriteStartedAsync(Guid requestId) => WriteMessageAsync(
        new WorkerStartedMessage
        {
            ProtocolVersion = WorkerProtocol.Version,
            MessageType = WorkerMessageKind.Started,
            RequestId = requestId
        });

    private Task WriteProgressAsync(
        Guid requestId,
        WorkerStage stage,
        int completed) => WriteMessageAsync(new WorkerProgressMessage
        {
            ProtocolVersion = WorkerProtocol.Version,
            MessageType = WorkerMessageKind.Progress,
            RequestId = requestId,
            Stage = stage,
            StageStatus = WorkerStageStatus.Running,
            CompletedStageCount = completed,
            TotalStageCount = 5,
            StageFraction = 0.5
        });

    private Task WriteControlledFailureAsync(Guid requestId) => WriteMessageAsync(
        new WorkerCompletedMessage
        {
            ProtocolVersion = WorkerProtocol.Version,
            MessageType = WorkerMessageKind.Completed,
            RequestId = requestId,
            CompletionStatus = WorkerCompletionStatus.OperationalFailure,
            OperationalFailure = new WorkerOperationalFailure
            {
                Code = "MI-FIXTURE-CONTROLLED-FAILURE",
                Message = "The fixture completed with a controlled failure."
            }
        });

    private Task WriteCancelledAsync(Guid requestId) => WriteMessageAsync(
        new WorkerCompletedMessage
        {
            ProtocolVersion = WorkerProtocol.Version,
            MessageType = WorkerMessageKind.Completed,
            RequestId = requestId,
            CompletionStatus = WorkerCompletionStatus.Cancelled
        });

    private async Task WriteMessageAsync(object message)
    {
        byte[] payload = WorkerProtocolJson.Serialize(message);
        await WriteRawAsync(payload).ConfigureAwait(false);
        await WriteRawAsync([(byte)'\n']).ConfigureAwait(false);
    }

    private Task WriteRawLineAsync(string value) =>
        WriteRawAsync(StrictUtf8.GetBytes(value + "\n"));

    private async Task WriteRawAsync(byte[] payload)
    {
        await _output.WriteAsync(payload).ConfigureAwait(false);
        await _output.FlushAsync().ConfigureAwait(false);
    }

    private StreamReader CreateReader() => new(
        _input,
        StrictUtf8,
        detectEncodingFromByteOrderMarks: false,
        bufferSize: 1024,
        leaveOpen: true);

    private static Task HangAsync() =>
        Task.Delay(Timeout.InfiniteTimeSpan);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetEvent(IntPtr eventHandle);
}
