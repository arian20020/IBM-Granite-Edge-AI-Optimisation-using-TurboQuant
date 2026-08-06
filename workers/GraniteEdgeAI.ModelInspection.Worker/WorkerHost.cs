using System.Text;
using GraniteEdgeAI.ModelInspection.Contracts;
using GraniteEdgeAI.ModelInspection.Transport;

namespace GraniteEdgeAI.ModelInspection.Worker;

/// <summary>
/// Owns the one-request worker protocol lifecycle. It does not inspect model
/// files directly; all runtime work is delegated through the engine seam.
/// </summary>
internal sealed class WorkerHost
{
    private readonly BoundedUtf8LineReader _input;
    private readonly BoundedUtf8LineWriter _output;
    private readonly Stream _standardError;
    private readonly IWorkerInspectionEngine _engine;
    private readonly IParentProcessMonitor _parentMonitor;
    private readonly int _workerProcessId;
    private readonly string _workerVersion;

    internal WorkerHost(
        Stream input,
        Stream output,
        Stream standardError,
        IWorkerInspectionEngine engine,
        IParentProcessMonitor parentMonitor,
        int workerProcessId,
        string workerVersion)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(standardError);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(parentMonitor);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(workerProcessId, 0);
        ArgumentException.ThrowIfNullOrWhiteSpace(workerVersion);

        _input = new BoundedUtf8LineReader(
            input,
            WorkerProtocol.MaximumMessageBytes);
        _output = new BoundedUtf8LineWriter(
            output,
            WorkerProtocol.MaximumMessageBytes);
        _standardError = standardError;
        _engine = engine;
        _parentMonitor = parentMonitor;
        _workerProcessId = workerProcessId;
        _workerVersion = workerVersion;
    }

    internal async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        WorkerCommandSequenceValidator commandSequence = new();
        WorkerMessageSequenceValidator messageSequence = new();
        WorkerTerminalCoordinator terminalCoordinator = new();
        using CancellationTokenSource sessionCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            if (!Environment.Is64BitProcess)
            {
                await WriteFixedErrorAsync("MI-WORKER-ARCHITECTURE-FAILURE")
                    .ConfigureAwait(false);
                return WorkerExitCodes.OperationalFailure;
            }

            WorkerHelloMessage hello = CreateHello();
            messageSequence.AcceptHello(hello);
            await WriteMessageAsync(hello, cancellationToken)
                .ConfigureAwait(false);

            WorkerStartInspectionCommand start = await ReadStartAsync(
                    cancellationToken)
                .ConfigureAwait(false);
            commandSequence.AcceptStart(start);
            messageSequence.SetExpectedRequest(start.RequestId);

            Task parentLoss = _parentMonitor.MonitorAsync(
                start,
                sessionCancellation.Token);

            WorkerStartedMessage started = new()
            {
                ProtocolVersion = WorkerProtocol.Version,
                MessageType = WorkerMessageKind.Started,
                RequestId = start.RequestId
            };
            messageSequence.AcceptStarted(started);
            await WriteMessageAsync(started, cancellationToken)
                .ConfigureAwait(false);

            using CancellationTokenSource operationCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    sessionCancellation.Token);
            SerializedProgressReporter progress = new(
                _output,
                messageSequence);
            Task<WorkerEngineResult> engineTask = _engine.InspectAsync(
                start,
                progress,
                operationCancellation.Token);
            Task<CommandPumpOutcome> commandPump = PumpCommandsAsync(
                commandSequence,
                sessionCancellation.Token);

            Task firstCompletion = await Task.WhenAny(
                    engineTask,
                    commandPump,
                    parentLoss)
                .ConfigureAwait(false);
            if (ReferenceEquals(firstCompletion, commandPump))
            {
                _ = await commandPump.ConfigureAwait(false);
                operationCancellation.Cancel();
            }
            else if (ReferenceEquals(firstCompletion, parentLoss))
            {
                await parentLoss.ConfigureAwait(false);
                operationCancellation.Cancel();
            }

            WorkerEngineResult result = await ReadEngineResultAsync(engineTask)
                .ConfigureAwait(false);

            // A command that completed while the engine was finishing must not
            // be ignored. Await it before terminal output so protocol misuse
            // cannot race past the one-request state machine.
            if (commandPump.IsCompleted)
            {
                _ = await commandPump.ConfigureAwait(false);
            }

            // Accepted progress is serialized and fully drained before any
            // cancellation or terminal write, so output ordering is stable.
            await progress.DrainAsync().ConfigureAwait(false);
            sessionCancellation.Cancel();

            WorkerCompletedMessage terminal = CreateTerminal(start, result);
            if (!terminalCoordinator.TryBeginTerminal())
            {
                throw new WorkerProtocolException(
                    "Only one terminal message may be written.");
            }

            messageSequence.AcceptCompleted(terminal);
            commandSequence.MarkTerminal();
            await WriteMessageAsync(terminal, CancellationToken.None)
                .ConfigureAwait(false);
            return MapExitCode(result.CompletionStatus);
        }
        catch (Exception error) when (
            error is WorkerProtocolException or ProtocolStreamException)
        {
            sessionCancellation.Cancel();
            await WriteFixedErrorAsync("MI-WORKER-PROTOCOL-FAILURE")
                .ConfigureAwait(false);
            return WorkerExitCodes.ProtocolFailure;
        }
        catch (OperationCanceledException)
        {
            sessionCancellation.Cancel();
            await WriteFixedErrorAsync("MI-WORKER-OPERATION-CANCELLED")
                .ConfigureAwait(false);
            return WorkerExitCodes.OperationalFailure;
        }
        catch (Exception)
        {
            sessionCancellation.Cancel();
            await WriteFixedErrorAsync("MI-WORKER-OPERATIONAL-FAILURE")
                .ConfigureAwait(false);
            return WorkerExitCodes.OperationalFailure;
        }
        finally
        {
            _output.Dispose();
        }
    }

    private WorkerHelloMessage CreateHello() => new()
    {
        ProtocolVersion = WorkerProtocol.Version,
        MessageType = WorkerMessageKind.Hello,
        WorkerId = WorkerProtocol.WorkerId,
        WorkerVersion = _workerVersion,
        WorkerProcessId = _workerProcessId,
        RuntimeProfile = WorkerProtocol.RuntimeProfile,
        ProcessArchitecture = "X64"
    };

    private async Task<WorkerStartInspectionCommand> ReadStartAsync(
        CancellationToken cancellationToken)
    {
        byte[]? payload = await _input.ReadLineAsync(cancellationToken)
            .ConfigureAwait(false);
        if (payload is null)
        {
            throw new WorkerProtocolException(
                "The command stream ended before startInspection.");
        }

        object command = WorkerProtocolJson.DeserializeCommand(payload);
        return command as WorkerStartInspectionCommand ??
            throw new WorkerProtocolException(
                "The first command must be startInspection.");
    }

    private async Task<CommandPumpOutcome> PumpCommandsAsync(
        WorkerCommandSequenceValidator sequence,
        CancellationToken cancellationToken)
    {
        byte[]? payload = await _input.ReadLineAsync(cancellationToken)
            .ConfigureAwait(false);
        if (payload is null)
        {
            return CommandPumpOutcome.EndOfStream;
        }

        object command = WorkerProtocolJson.DeserializeCommand(payload);
        if (command is not WorkerCancelInspectionCommand cancel)
        {
            throw new WorkerProtocolException(
                "Only cancelInspection is valid after startInspection.");
        }

        sequence.AcceptCancel(cancel);
        return CommandPumpOutcome.CancelRequested;
    }

    private static async Task<WorkerEngineResult> ReadEngineResultAsync(
        Task<WorkerEngineResult> engineTask)
    {
        try
        {
            return await engineTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return WorkerEngineResult.Cancelled();
        }
        catch (Exception)
        {
            return WorkerEngineResult.ControlledFailure(
                "MI-OP-ENGINE-FAILED",
                "The model inspection engine could not produce reliable evidence.");
        }
    }

    private static WorkerCompletedMessage CreateTerminal(
        WorkerStartInspectionCommand start,
        WorkerEngineResult result) => new()
    {
        ProtocolVersion = WorkerProtocol.Version,
        MessageType = WorkerMessageKind.Completed,
        RequestId = start.RequestId,
        CompletionStatus = result.CompletionStatus,
        Evidence = result.Evidence,
        OperationalFailure = result.OperationalFailure
    };

    private async Task WriteMessageAsync(
        object message,
        CancellationToken cancellationToken)
    {
        byte[] payload = WorkerProtocolJson.Serialize(message);
        await _output.WriteLineAsync(payload, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task WriteFixedErrorAsync(string code)
    {
        byte[] payload = Encoding.UTF8.GetBytes(code + "\n");
        await _standardError.WriteAsync(payload, CancellationToken.None)
            .ConfigureAwait(false);
        await _standardError.FlushAsync(CancellationToken.None)
            .ConfigureAwait(false);
    }

    private static int MapExitCode(WorkerCompletionStatus status) =>
        status switch
        {
            WorkerCompletionStatus.Completed => WorkerExitCodes.Completed,
            WorkerCompletionStatus.Cancelled => WorkerExitCodes.Cancelled,
            WorkerCompletionStatus.OperationalFailure =>
                WorkerExitCodes.OperationalFailure,
            _ => WorkerExitCodes.OperationalFailure
        };

    private enum CommandPumpOutcome
    {
        EndOfStream,
        CancelRequested
    }

    /// <summary>
    /// Serializes progress reports and exposes a drain task so the terminal
    /// message cannot overtake queued progress.
    /// </summary>
    private sealed class SerializedProgressReporter :
        IProgress<WorkerProgressMessage>
    {
        private readonly object _gate = new();
        private readonly BoundedUtf8LineWriter _writer;
        private readonly WorkerMessageSequenceValidator _sequence;
        private Task _tail = Task.CompletedTask;

        internal SerializedProgressReporter(
            BoundedUtf8LineWriter writer,
            WorkerMessageSequenceValidator sequence)
        {
            _writer = writer;
            _sequence = sequence;
        }

        public void Report(WorkerProgressMessage value)
        {
            ArgumentNullException.ThrowIfNull(value);
            lock (_gate)
            {
                _tail = _tail.ContinueWith(
                        _ => WriteAsync(value),
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default)
                    .Unwrap();
            }
        }

        internal Task DrainAsync()
        {
            lock (_gate)
            {
                return _tail;
            }
        }

        private async Task WriteAsync(WorkerProgressMessage value)
        {
            _sequence.AcceptProgress(value);
            byte[] payload = WorkerProtocolJson.Serialize(value);
            await _writer.WriteLineAsync(payload, CancellationToken.None)
                .ConfigureAwait(false);
        }
    }
}
