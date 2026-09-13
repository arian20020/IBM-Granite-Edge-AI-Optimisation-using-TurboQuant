using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using GraniteEdgeAI.ModelInspection.Contracts;
using GraniteEdgeAI.ModelInspection.Transport;

namespace GraniteEdgeAI.ModelInspection.ProtocolTestWorker;

/// <summary>
/// runs deterministic abnormal protocol and process behaviours for integration
/// tests. this executable never opens a model file or starts a network listener
/// </summary>
internal sealed class ProtocolScenarioRunner
{
    private static readonly TimeSpan DelayedHelloMinimumActiveBudget =
        TimeSpan.FromMilliseconds(100);
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);
    private readonly Stream _input;
    private readonly Stream _output;
    private readonly Stream _error;
    private readonly FixtureMilestoneWriter _milestones;

    internal ProtocolScenarioRunner(Stream input, Stream output, Stream error)
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
                return await ProbeAsync("fixture-ready").ConfigureAwait(false);
            case TestWorkerScenario.ProbeUnrelatedHandle:
                return await ProbeHandleAsync(request.NumericValue!.Value).ConfigureAwait(false);
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
                await WriteRawAsync(new byte[WorkerProtocol.MaximumMessageBytes + 1]).ConfigureAwait(false);
                await HangAsync().ConfigureAwait(false);
                return 1;
            case TestWorkerScenario.MalformedHello:
                await WriteLineAsync("{}").ConfigureAwait(false);
                return 0;
            case TestWorkerScenario.MalformedJson:
                await WriteLineAsync("{not-json}").ConfigureAwait(false);
                return 0;
            case TestWorkerScenario.DuplicateJsonProperty:
                await WriteLineAsync("{\"protocolVersion\":1,\"protocolVersion\":1,\"messageType\":\"hello\"}").ConfigureAwait(false);
                return 0;
            case TestWorkerScenario.TextBeforeHello:
                await WriteLineAsync("fixture text before hello").ConfigureAwait(false);
                await WriteHelloAsync().ConfigureAwait(false);
                return 0;
            case TestWorkerScenario.WrongProtocolVersion:
                await WriteRawHelloAsync(protocolVersion: 2).ConfigureAwait(false);
                return 0;
            case TestWorkerScenario.WrongWorkerId:
                await WriteRawHelloAsync(workerId: "wrong-worker").ConfigureAwait(false);
                return 0;
            case TestWorkerScenario.WrongWorkerProcessId:
                await WriteRawHelloAsync(processId: Environment.ProcessId + 1000).ConfigureAwait(false);
                return 0;
            case TestWorkerScenario.WrongRuntimeProfile:
                await WriteRawHelloAsync(runtimeProfile: "wrong-profile").ConfigureAwait(false);
                return 0;
            case TestWorkerScenario.WrongArchitecture:
                await WriteRawHelloAsync(architecture: "ARM64").ConfigureAwait(false);
                return 0;
            default:
                if (request.Scenario ==
                        TestWorkerScenario.CooperativeCancellation &&
                    request.NumericValue is long helloDelayMilliseconds)
                {
                    await Task.Delay(
                            checked((int)helloDelayMilliseconds))
                        .ConfigureAwait(false);
                }

                return await RunConversationAsync(request)
                    .ConfigureAwait(false);
        }
    }

    private async Task<int> RunConversationAsync(
        TestWorkerScenarioRequest request)
    {
        TestWorkerScenario scenario = request.Scenario;
        await WriteHelloAsync().ConfigureAwait(false);
        await _milestones.WriteAsync("FIXTURE:HELLO_WRITTEN").ConfigureAwait(false);
        if (scenario == TestWorkerScenario.CrashAfterHello)
        {
            Environment.FailFast("Fixture crash after hello.");
        }

        if (scenario == TestWorkerScenario.HangAfterHello)
        {
            await HangAsync().ConfigureAwait(false);
        }

        WorkerStartInspectionCommand start =
            await ReadCommandAsync<WorkerStartInspectionCommand>().ConfigureAwait(false);
        await _milestones.WriteAsync("FIXTURE:START_RECEIVED").ConfigureAwait(false);
        if (scenario == TestWorkerScenario.ObserveParentIdentity)
        {
            using Process parent = Process.GetProcessById(start.ParentProcessId);
            DateTimeOffset observedStart = new(
                parent.StartTime.ToUniversalTime(),
                TimeSpan.Zero);
            if (observedStart != start.ParentProcessStartTimeUtc)
            {
                throw new InvalidOperationException(
                    "The delivered parent identity did not match the observed process.");
            }

            await _milestones.WriteAsync("FIXTURE:PARENT_IDENTITY_OBSERVED")
                .ConfigureAwait(false);
        }
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
            await WriteProgressAsync(
                    start.RequestId,
                    WorkerStage.CheckModelPackage,
                    0)
                .ConfigureAwait(false);
            await HangAsync().ConfigureAwait(false);
        }

        if (scenario is TestWorkerScenario.SpawnChildAndWait or
            TestWorkerScenario.ExitRootWithLiveChild)
        {
            using Process child = ChildProcessScenario.StartWaitingChild();
            await _milestones.WriteAsync($"FIXTURE:CHILD_STARTED:{child.Id}")
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
            await WriteFailureAsync(wrong).ConfigureAwait(false);
            return 1;
        }

        await WriteStartedAsync(start.RequestId).ConfigureAwait(false);
        if (scenario == TestWorkerScenario.NonMonotonicProgress)
        {
            await WriteProgressAsync(
                    start.RequestId,
                    WorkerStage.ReadModelConfiguration,
                    1)
                .ConfigureAwait(false);
            await WriteProgressAsync(
                    start.RequestId,
                    WorkerStage.CheckModelPackage,
                    0)
                .ConfigureAwait(false);
            await HangAsync().ConfigureAwait(false);
        }

        if (scenario == TestWorkerScenario.ExitWithoutTerminal)
        {
            return 0;
        }

        if (scenario is TestWorkerScenario.CooperativeCancellation or
            TestWorkerScenario.CompletionAfterCancellation or
            TestWorkerScenario.IgnoreCancellation)
        {
            // the progress frame gives process tests a deterministic point after
            // StartSent at which to trigger caller cancellation.
            await WriteProgressAsync(
                    start.RequestId,
                    WorkerStage.CheckModelPackage,
                    0)
                .ConfigureAwait(false);
            Task<WorkerCancelInspectionCommand> cancelTask =
                ReadCommandAsync<WorkerCancelInspectionCommand>();
            if (scenario == TestWorkerScenario.CooperativeCancellation &&
                request.NumericValue.HasValue)
            {
                Task activeBudgetGate = Task.Delay(
                    DelayedHelloMinimumActiveBudget);
                if (await Task.WhenAny(cancelTask, activeBudgetGate)
                        .ConfigureAwait(false) == cancelTask)
                {
                    await _milestones.WriteAsync(
                            "FIXTURE:CANCEL_BEFORE_ACTIVE_BUDGET")
                        .ConfigureAwait(false);
                    return 2;
                }
            }

            WorkerCancelInspectionCommand cancel = await cancelTask
                .ConfigureAwait(false);
            if (cancel.RequestId != start.RequestId)
            {
                return 2;
            }

            await _milestones.WriteAsync("FIXTURE:CANCEL_RECEIVED")
                .ConfigureAwait(false);
            if (scenario == TestWorkerScenario.CompletionAfterCancellation)
            {
                await WriteFailureAsync(start.RequestId).ConfigureAwait(false);
                await _milestones.WriteAsync(
                        "FIXTURE:COMPLETION_AFTER_CANCEL")
                    .ConfigureAwait(false);
                return 1;
            }

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
            byte[] invalid = [0xC3, 0x28];
            await _error.WriteAsync(invalid).ConfigureAwait(false);
            await _error.FlushAsync().ConfigureAwait(false);
        }

        if (scenario == TestWorkerScenario.EchoEnvironmentKeys)
        {
            string keys = string.Join(
                ';',
                Environment.GetEnvironmentVariables().Keys.Cast<object>()
                    .Select(static value => value.ToString() ?? string.Empty)
                    .OrderBy(
                        static value => value,
                        StringComparer.OrdinalIgnoreCase));
            await _milestones.WriteAsync("FIXTURE:ENV_KEYS:" + keys)
                .ConfigureAwait(false);

            bool diagnosticsDisabled =
                IsEnvironmentZero("DOTNET_EnableDiagnostics") &&
                IsEnvironmentZero("DOTNET_EnableDiagnostics_IPC") &&
                IsEnvironmentZero("DOTNET_EnableDiagnostics_Debugger") &&
                IsEnvironmentZero("DOTNET_EnableDiagnostics_Profiler");
            await _milestones.WriteAsync(
                    "FIXTURE:DIAGNOSTICS_DISABLED:" +
                    diagnosticsDisabled.ToString().ToLowerInvariant())
                .ConfigureAwait(false);
        }

        await WriteFailureAsync(start.RequestId).ConfigureAwait(false);
        await _milestones.WriteAsync("FIXTURE:TERMINAL_WRITTEN")
            .ConfigureAwait(false);
        if (scenario == TestWorkerScenario.DuplicateTerminal)
        {
            await WriteFailureAsync(start.RequestId).ConfigureAwait(false);
        }

        return scenario == TestWorkerScenario.TerminalExitMismatch ? 0 : 1;
    }

    private async Task<TCommand> ReadCommandAsync<TCommand>()
        where TCommand : class
    {
        BoundedUtf8LineReader reader = new(
            _input,
            WorkerProtocol.MaximumMessageBytes);
        byte[] payload = await reader.ReadLineAsync(CancellationToken.None)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "Fixture command stream ended.");
        return WorkerProtocolJson.DeserializeCommand(payload) as TCommand
            ?? throw new InvalidOperationException(
                "Fixture received the wrong command.");
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
        int protocolVersion = 1,
        string workerId = WorkerProtocol.WorkerId,
        int? processId = null,
        string runtimeProfile = WorkerProtocol.RuntimeProfile,
        string architecture = "X64") =>
        WriteLineAsync(
            $"{{\"protocolVersion\":{protocolVersion}," +
            $"\"messageType\":\"hello\"," +
            $"\"workerId\":\"{workerId}\"," +
            "\"workerVersion\":\"fixture-1.0.0\"," +
            $"\"workerProcessId\":{processId ?? Environment.ProcessId}," +
            $"\"runtimeProfile\":\"{runtimeProfile}\"," +
            $"\"processArchitecture\":\"{architecture}\"}}");

    private Task WriteStartedAsync(Guid id) => WriteMessageAsync(new WorkerStartedMessage
    {
        ProtocolVersion = 1,
        MessageType = WorkerMessageKind.Started,
        RequestId = id
    });

    private Task WriteProgressAsync(
        Guid id,
        WorkerStage stage,
        int count) => WriteMessageAsync(new WorkerProgressMessage
    {
        ProtocolVersion = 1,
        MessageType = WorkerMessageKind.Progress,
        RequestId = id,
        Stage = stage,
        StageStatus = WorkerStageStatus.Active,
        CompletedStageCount = count,
        TotalStageCount = 5,
        StageFraction = 0.5
    });

    private Task WriteFailureAsync(Guid id) => WriteMessageAsync(new WorkerCompletedMessage
    {
        ProtocolVersion = 1,
        MessageType = WorkerMessageKind.Completed,
        RequestId = id,
        CompletionStatus = WorkerCompletionStatus.OperationalFailure,
        OperationalFailure = new WorkerOperationalFailure
        {
            Code = "MI-FIXTURE-CONTROLLED-FAILURE",
            Message = "The fixture completed with a controlled failure."
        }
    });

    private Task WriteCancelledAsync(Guid id) => WriteMessageAsync(new WorkerCompletedMessage
    {
        ProtocolVersion = 1,
        MessageType = WorkerMessageKind.Completed,
        RequestId = id,
        CompletionStatus = WorkerCompletionStatus.Cancelled
    });

    private async Task WriteMessageAsync(object message)
    {
        await WriteRawAsync(WorkerProtocolJson.Serialize(message))
            .ConfigureAwait(false);
        await WriteRawAsync([(byte)'\n']).ConfigureAwait(false);
    }

    private Task WriteLineAsync(string text) =>
        WriteRawAsync(StrictUtf8.GetBytes(text + "\n"));

    private async Task WriteRawAsync(byte[] bytes)
    {
        await _output.WriteAsync(bytes).ConfigureAwait(false);
        await _output.FlushAsync().ConfigureAwait(false);
    }

    private async Task<int> ProbeAsync(string milestone)
    {
        await WriteLineAsync(milestone).ConfigureAwait(false);
        using StreamReader reader = new(
            _input,
            StrictUtf8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true);
        _ = await reader.ReadLineAsync().ConfigureAwait(false);
        return 0;
    }

    private Task<int> ProbeHandleAsync(long value) =>
        ProbeAsync(
            SetEvent(new IntPtr(value))
                ? "handle-signaled"
                : "handle-unavailable");

    private static bool IsEnvironmentZero(string key) =>
        string.Equals(
            Environment.GetEnvironmentVariable(key),
            "0",
            StringComparison.Ordinal);

    private static Task HangAsync() =>
        Task.Delay(Timeout.InfiniteTimeSpan);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetEvent(IntPtr eventHandle);
}
