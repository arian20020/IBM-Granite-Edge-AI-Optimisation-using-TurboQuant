using GraniteEdgeAI.ModelInspection.Contracts;
using GraniteEdgeAI.ModelInspection.Transport;
using GraniteEdgeAI.ModelInspection.WorkerClient.ProtectedWorker;
using GraniteEdgeAI.HardwareInspection.Foundation.Processes;

namespace GraniteEdgeAI.ModelInspection.WorkerClient;

/// <summary>
/// Runs one validated inspection command through one short-lived, creation-time
/// contained worker process. The adapter returns either one trusted terminal
/// message or one controlled infrastructure failure, never partial evidence.
/// </summary>
public sealed class InspectionWorkerClient : IInspectionWorkerClient
{
    private const string HandshakeTimeoutMessage =
        "The Model Inspection worker did not complete its handshake in time.";
    private const string HandshakeInvalidMessage =
        "The Model Inspection worker identity could not be verified.";
    private const string ProtocolInvalidMessage =
        "The Model Inspection worker protocol conversation was invalid.";
    private const string OutputLimitMessage =
        "The Model Inspection worker output exceeded the approved byte limit.";
    private const string WorkerCrashedMessage =
        "The Model Inspection worker process ended unexpectedly.";
    private const string OverallTimeoutMessage =
        "The Model Inspection worker exceeded the approved execution time.";
    private const string CancellationForcedMessage =
        "The Model Inspection worker did not stop within the cancellation grace period.";
    private const string ExitMismatchMessage =
        "The Model Inspection worker terminal result did not match its process exit.";
    private const string ProcessTreeMessage =
        "The Model Inspection worker process tree did not become empty.";
    private static readonly Task NeverCompletingTask =
        Task.Delay(Timeout.InfiniteTimeSpan);

    private readonly WorkerClientOptions _options;
    private readonly WorkerExecutableResolver _resolver;
    private readonly IProtectedWorkerSessionFactory _sessionFactory;
    private readonly IReadOnlyList<string> _testOnlyArguments;
    private readonly Func<IReadOnlyDictionary<string, string?>>
        _parentEnvironmentProvider;

    /// <summary>
    /// Creates the production adapter for the one fixed package-relative
    /// worker executable. Production composition cannot select another path or
    /// a fixture mode.
    /// </summary>
    public InspectionWorkerClient(WorkerClientOptions options)
        : this(
            options,
            WorkerInstallationLayout.WorkerExecutableRelativePath,
            Array.Empty<string>(),
            CaptureParentEnvironment)
    {
    }

    /// <summary>
    /// Internal constructor used only by the process-test assembly to select a
    /// deterministic abnormal fixture scenario through the reviewed launcher.
    /// </summary>
    internal InspectionWorkerClient(
        WorkerClientOptions options,
        string fixedWorkerRelativePath,
        IReadOnlyList<string> testOnlyArguments)
        : this(
            options,
            fixedWorkerRelativePath,
            testOnlyArguments,
            CaptureParentEnvironment)
    {
    }

    internal InspectionWorkerClient(
        WorkerClientOptions options,
        string fixedWorkerRelativePath,
        IReadOnlyList<string> testOnlyArguments,
        Func<IReadOnlyDictionary<string, string?>> parentEnvironmentProvider)
        : this(
            options,
            fixedWorkerRelativePath,
            testOnlyArguments,
            parentEnvironmentProvider,
            new ProtectedWorkerSessionFactory())
    {
    }

    private InspectionWorkerClient(
        WorkerClientOptions options,
        string fixedWorkerRelativePath,
        IReadOnlyList<string> testOnlyArguments,
        Func<IReadOnlyDictionary<string, string?>> parentEnvironmentProvider,
        IProtectedWorkerSessionFactory sessionFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(fixedWorkerRelativePath);
        ArgumentNullException.ThrowIfNull(testOnlyArguments);
        ArgumentNullException.ThrowIfNull(parentEnvironmentProvider);
        ArgumentNullException.ThrowIfNull(sessionFactory);

        options.Validate();
        _options = options;
        _resolver = new WorkerExecutableResolver(fixedWorkerRelativePath);
        _sessionFactory = sessionFactory;
        _testOnlyArguments = Array.AsReadOnly([.. testOnlyArguments]);
        _parentEnvironmentProvider = parentEnvironmentProvider;
    }

    /// <inheritdoc />
    public async Task<WorkerClientResult> ExecuteAsync(
        WorkerStartInspectionCommand command,
        IProgress<WorkerProgressMessage>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        WorkerFailureAccumulator failures = new();
        ProtectedWorkerSession? session = null;
        Task<StandardErrorSnapshot>? standardErrorTask = null;
        Task? processExitTask = null;
        StandardErrorSnapshot standardError = new(
            retainedText: string.Empty,
            isTruncated: false,
            invalidUtf8Detected: false);
        WorkerCompletedMessage? terminal = null;
        int? exitCode = null;
        bool forcedTermination = false;
        bool handshakeCompleted = false;
        bool startSent = false;
        bool rethrowPreStartCancellation = false;
        TrustedToolOperationEnvironment? operationEnvironment = null;

        try
        {
            using VerifiedWorkerExecutable executable =
                _resolver.Resolve(_options.ApprovedWorkerRoot);
            operationEnvironment = TrustedToolOperationEnvironment.Create(
                _parentEnvironmentProvider());
            ProtectedWorkerLaunchSpec launchSpec = new(
                executable,
                _testOnlyArguments,
                operationEnvironment.Variables,
                WorkerProtocol.MaximumMessageBytes,
                WorkerProtocol.MaximumMessageBytes,
                _options.MaximumRetainedStandardErrorBytes,
                _options.StartupTimeout,
                _options.CancellationGracePeriod,
                _options.ProcessTreeCleanupTimeout);

            session = await _sessionFactory.StartAsync(
                    launchSpec,
                    cancellationToken)
                .ConfigureAwait(false);

            // Start all independent observations immediately. No redirected
            // stream waits behind another stream or behind process exit.
            standardErrorTask = session.ReadStandardErrorAsync();
            processExitTask = session.WaitForExitAsync(CancellationToken.None);
            BoundedUtf8LineReader stdoutReader = session.StandardOutput;
            Task callerSignal = cancellationToken.CanBeCanceled
                ? Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)
                : NeverCompletingTask;

            WorkerHelloMessage hello = await ReadAndValidateHelloAsync(
                    stdoutReader,
                    session,
                    processExitTask,
                    callerSignal,
                    cancellationToken)
                .ConfigureAwait(false);
            handshakeCompleted = true;

            // Cancellation before StartSent cannot produce a trusted worker
            // Cancelled terminal because no request has become active.
            cancellationToken.ThrowIfCancellationRequested();

            BoundedUtf8LineWriter stdinWriter = session.StandardInput;
            await stdinWriter.WriteLineAsync(
                    WorkerProtocolJson.Serialize(command),
                    CancellationToken.None)
                .ConfigureAwait(false);
            startSent = true;
            Task overallSignal = Task.Delay(
                _options.OverallTimeout,
                CancellationToken.None);

            WorkerConversation conversation = new(
                hello,
                command.RequestId);
            Task<WorkerCompletedMessage> conversationTask =
                ReadConversationToEndAsync(
                    stdoutReader,
                    conversation,
                    progress);
            WorkerCancellationCoordinator cancellationCoordinator = new(
                stdinWriter,
                command.RequestId);

            ActiveRunResult activeResult = await RunActiveRequestAsync(
                    session,
                    conversationTask,
                    processExitTask,
                    cancellationCoordinator,
                    callerSignal,
                    overallSignal)
                .ConfigureAwait(false);
            terminal = activeResult.TerminalMessage;
            exitCode = activeResult.ExitCode;
            forcedTermination = activeResult.ForcedTermination;
            if (activeResult.Failure is not null)
            {
                _ = failures.TrySetPrimary(activeResult.Failure);
            }

            // Closing stdin after completion prevents a worker from waiting for
            // more commands and makes the parent-loss signal unambiguous.
            await session.CompleteInputAsync().ConfigureAwait(false);
        }
        catch (WorkerClientPolicyException error)
        {
            failures.RetainCleanupIntegrity(error);
            WorkerClientFailure policyFailure = error.Failure;
            if (policyFailure.Code == WorkerClientFailureCodes.WorkerProtocolInvalid &&
                session is not null &&
                processExitTask?.IsCompleted == true &&
                TryGetActiveProcessCount(session) != 0)
            {
                policyFailure = new WorkerClientFailure(
                    WorkerClientFailureCodes.WorkerProcessTreeIntegrityFailed,
                    ProcessTreeMessage);
            }

            _ = failures.TrySetPrimary(policyFailure);
        }
        catch (ProtocolStreamException error)
        {
            string code = error.ErrorKind ==
                ProtocolStreamErrorKind.LineTooLong
                ? WorkerClientFailureCodes.WorkerOutputLimitExceeded
                : handshakeCompleted
                    ? WorkerClientFailureCodes.WorkerProtocolInvalid
                    : WorkerClientFailureCodes.WorkerHandshakeInvalid;
            string message = code ==
                WorkerClientFailureCodes.WorkerOutputLimitExceeded
                ? OutputLimitMessage
                : handshakeCompleted
                    ? ProtocolInvalidMessage
                    : HandshakeInvalidMessage;
            _ = failures.TrySetPrimary(new WorkerClientFailure(code, message));
        }
        catch (WorkerProtocolException)
        {
            _ = failures.TrySetPrimary(new WorkerClientFailure(
                handshakeCompleted
                    ? WorkerClientFailureCodes.WorkerProtocolInvalid
                    : WorkerClientFailureCodes.WorkerHandshakeInvalid,
                handshakeCompleted
                    ? ProtocolInvalidMessage
                    : HandshakeInvalidMessage));
        }
        catch (OperationCanceledException)
            when (!startSent && cancellationToken.IsCancellationRequested)
        {
            rethrowPreStartCancellation = true;
        }
        catch (Exception error) when (IsExpectedProcessFailure(error))
        {
            _ = failures.TrySetPrimary(new WorkerClientFailure(
                WorkerClientFailureCodes.WorkerCrashed,
                WorkerCrashedMessage));
        }
        finally
        {
            if (session is not null)
            {
                // Any non-success path must leave the Job Object empty before
                // its handle is released. TerminateJobObject is recorded as a
                // forced operational outcome, never ordinary cancellation.
                if (rethrowPreStartCancellation ||
                    failures.PrimaryFailure is not null)
                {
                    try
                    {
                        forcedTermination |=
                            await session.TerminateAndVerifyEmptyAsync()
                                .ConfigureAwait(false);
                    }
                    catch (WorkerClientPolicyException error)
                    {
                        failures.RetainCleanupIntegrity(error);
                        if (!failures.TrySetPrimary(error.Failure))
                        {
                            failures.AddSecondary(error);
                        }
                    }
                    catch (Exception error) when (
                        IsExpectedProcessFailure(error))
                    {
                        failures.AddSecondary(error);
                        _ = failures.TrySetPrimary(new WorkerClientFailure(
                            WorkerClientFailureCodes.WorkerCleanupFailed,
                            "The Model Inspection worker cleanup could not be verified."));
                    }
                }

                if (exitCode is null && processExitTask?.IsCompleted == true)
                {
                    try
                    {
                        exitCode = session.GetExitCode();
                    }
                    catch (Exception error) when (
                        IsExpectedProcessFailure(error) ||
                        error is WorkerClientPolicyException)
                    {
                        failures.AddSecondary(error);
                    }
                }

                if (standardErrorTask is not null)
                {
                    try
                    {
                        standardError = await standardErrorTask
                            .WaitAsync(
                                _options.ProcessTreeCleanupTimeout,
                                CancellationToken.None)
                            .ConfigureAwait(false);
                    }
                    catch (Exception error) when (
                        IsExpectedProcessFailure(error) ||
                        error is TimeoutException)
                    {
                        failures.AddSecondary(error);
                    }
                }

                try
                {
                    await session.DisposeAsync().ConfigureAwait(false);
                }
                catch (WorkerClientPolicyException error)
                {
                    failures.RetainCleanupIntegrity(error);
                    if (!failures.TrySetPrimary(error.Failure))
                    {
                        failures.AddSecondary(error);
                    }
                }
                catch (Exception error) when (IsExpectedProcessFailure(error))
                {
                    failures.AddSecondary(error);
                    _ = failures.TrySetPrimary(new WorkerClientFailure(
                        WorkerClientFailureCodes.WorkerCleanupFailed,
                        "The Model Inspection worker cleanup could not be verified."));
                }
            }

            operationEnvironment?.Dispose();
            if (operationEnvironment is not null && !operationEnvironment.CleanupSucceeded)
            {
                _ = failures.TrySetPrimary(new WorkerClientFailure(
                    WorkerClientFailureCodes.WorkerCleanupFailed,
                    "The Model Inspection worker cleanup could not be verified."));
            }
        }

        if (rethrowPreStartCancellation)
        {
            if (failures.PrimaryFailure is WorkerClientFailure cleanupFailure)
            {
                throw new OperationCanceledException(
                    "The Model Inspection operation was cancelled and cleanup integrity failed.",
                    failures.CleanupIntegrityCause ??
                        new WorkerClientPolicyException(cleanupFailure),
                    cancellationToken);
            }

            throw new OperationCanceledException(cancellationToken);
        }

        WorkerClientFailure? failure = failures.PrimaryFailure;
        if (failure is null && terminal is null)
        {
            failure = new WorkerClientFailure(
                WorkerClientFailureCodes.WorkerCrashed,
                WorkerCrashedMessage);
        }

        WorkerClientResult result = new(
            failure is null ? terminal : null,
            failure,
            exitCode,
            forcedTermination,
            standardError.IsTruncated,
            standardError.RetainedText,
            failures.SecondaryDiagnostics,
            standardError.InvalidUtf8Detected);
        result.Validate();
        return result;
    }

    private async Task<ActiveRunResult> RunActiveRequestAsync(
        ProtectedWorkerSession session,
        Task<WorkerCompletedMessage> conversationTask,
        Task processExitTask,
        WorkerCancellationCoordinator cancellationCoordinator,
        Task callerSignal,
        Task overallSignal)
    {
        Task first = await Task.WhenAny(
                conversationTask,
                processExitTask,
                callerSignal,
                overallSignal)
            .ConfigureAwait(false);

        if (first == callerSignal)
        {
            return await FinishAfterCancellationAsync(
                    session,
                    conversationTask,
                    processExitTask,
                    cancellationCoordinator,
                    overallTimeoutTriggered: false)
                .ConfigureAwait(false);
        }

        if (first == overallSignal)
        {
            return await FinishAfterCancellationAsync(
                    session,
                    conversationTask,
                    processExitTask,
                    cancellationCoordinator,
                    overallTimeoutTriggered: true)
                .ConfigureAwait(false);
        }

        if (first == processExitTask)
        {
            return await FinishAfterRootExitAsync(
                    session,
                    conversationTask,
                    processExitTask)
                .ConfigureAwait(false);
        }

        WorkerCompletedMessage terminal = await conversationTask
            .ConfigureAwait(false);
        return await FinishAfterTerminalAsync(
                session,
                terminal,
                processExitTask,
                overallSignal)
            .ConfigureAwait(false);
    }

    private async Task<ActiveRunResult> FinishAfterCancellationAsync(
        ProtectedWorkerSession session,
        Task<WorkerCompletedMessage> conversationTask,
        Task processExitTask,
        WorkerCancellationCoordinator cancellationCoordinator,
        bool overallTimeoutTriggered)
    {
        WorkerClientFailure triggerFailure = new(
            overallTimeoutTriggered
                ? WorkerClientFailureCodes.WorkerOverallTimeout
                : WorkerClientFailureCodes.WorkerCancellationForced,
            overallTimeoutTriggered
                ? OverallTimeoutMessage
                : CancellationForcedMessage);

        try
        {
            using CancellationTokenSource sendDeadline = new(
                _options.CancellationGracePeriod);
            _ = await cancellationCoordinator.RequestAsync(sendDeadline.Token)
                .ConfigureAwait(false);
        }
        catch (Exception error) when (
            error is OperationCanceledException or
            IOException or
            ObjectDisposedException or
            ProtocolStreamException)
        {
            bool forced = await session.TerminateAndVerifyEmptyAsync()
                .ConfigureAwait(false);
            return new ActiveRunResult(
                TerminalMessage: null,
                ExitCode: TryGetExitCode(session),
                ForcedTermination: forced,
                Failure: triggerFailure);
        }

        Task<ProcessCompletion> completionTask =
            CompleteConversationAndExitAsync(
                session,
                conversationTask,
                processExitTask,
                cancellationWasRequested: true);
        Task graceExpired = Task.Delay(_options.CancellationGracePeriod);
        Task winner = await Task.WhenAny(completionTask, graceExpired)
            .ConfigureAwait(false);

        if (winner != completionTask)
        {
            bool forced = await session.TerminateAndVerifyEmptyAsync()
                .ConfigureAwait(false);
            return new ActiveRunResult(
                TerminalMessage: null,
                ExitCode: TryGetExitCode(session),
                ForcedTermination: forced,
                Failure: triggerFailure);
        }

        try
        {
            ProcessCompletion completion = await completionTask
                .ConfigureAwait(false);
            if (overallTimeoutTriggered)
            {
                // The cooperative terminal proves orderly cleanup, but it does
                // not erase that the safety timeout initiated cancellation.
                return new ActiveRunResult(
                    TerminalMessage: null,
                    ExitCode: completion.ExitCode,
                    ForcedTermination: false,
                    Failure: triggerFailure);
            }

            return new ActiveRunResult(
                completion.TerminalMessage,
                completion.ExitCode,
                ForcedTermination: false,
                Failure: null);
        }
        catch (WorkerClientPolicyException error)
        {
            if (overallTimeoutTriggered)
            {
                return new ActiveRunResult(
                    TerminalMessage: null,
                    ExitCode: TryGetExitCode(session),
                    ForcedTermination: false,
                    Failure: triggerFailure);
            }

            return new ActiveRunResult(
                TerminalMessage: null,
                ExitCode: TryGetExitCode(session),
                ForcedTermination: false,
                Failure: error.Failure);
        }
    }

    private async Task<ActiveRunResult> FinishAfterRootExitAsync(
        ProtectedWorkerSession session,
        Task<WorkerCompletedMessage> conversationTask,
        Task processExitTask)
    {
        await processExitTask.ConfigureAwait(false);
        if (session.GetActiveProcessCount() != 0)
        {
            bool forced = await session.TerminateAndVerifyEmptyAsync()
                .ConfigureAwait(false);
            return new ActiveRunResult(
                TerminalMessage: null,
                ExitCode: session.GetExitCode(),
                ForcedTermination: forced,
                Failure: new WorkerClientFailure(
                    WorkerClientFailureCodes.WorkerProcessTreeIntegrityFailed,
                    ProcessTreeMessage));
        }

        WorkerCompletedMessage terminal;
        try
        {
            terminal = await conversationTask
                .WaitAsync(
                    _options.ProcessTreeCleanupTimeout,
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            bool forced = await session.TerminateAndVerifyEmptyAsync()
                .ConfigureAwait(false);
            return new ActiveRunResult(
                TerminalMessage: null,
                ExitCode: session.GetExitCode(),
                ForcedTermination: forced,
                Failure: new WorkerClientFailure(
                    WorkerClientFailureCodes.WorkerProcessTreeIntegrityFailed,
                    ProcessTreeMessage));
        }

        int exitCode = session.GetExitCode();
        ValidateCompletion(
            terminal,
            exitCode,
            cancellationWasRequested: false);
        return new ActiveRunResult(
            terminal,
            exitCode,
            ForcedTermination: false,
            Failure: null);
    }

    private static async Task<ActiveRunResult> FinishAfterTerminalAsync(
        ProtectedWorkerSession session,
        WorkerCompletedMessage terminal,
        Task processExitTask,
        Task overallSignal)
    {
        if (!processExitTask.IsCompleted)
        {
            Task winner = await Task.WhenAny(processExitTask, overallSignal)
                .ConfigureAwait(false);
            if (winner == overallSignal)
            {
                bool forced = await session.TerminateAndVerifyEmptyAsync()
                    .ConfigureAwait(false);
                return new ActiveRunResult(
                    TerminalMessage: null,
                    ExitCode: TryGetExitCode(session),
                    ForcedTermination: forced,
                    Failure: new WorkerClientFailure(
                        WorkerClientFailureCodes.WorkerOverallTimeout,
                        OverallTimeoutMessage));
            }
        }

        await processExitTask.ConfigureAwait(false);
        int exitCode = session.GetExitCode();
        if (!await session.WaitForTreeEmptyAsync()
            .ConfigureAwait(false))
        {
            bool forced = await session.TerminateAndVerifyEmptyAsync()
                .ConfigureAwait(false);
            return new ActiveRunResult(
                TerminalMessage: null,
                ExitCode: exitCode,
                ForcedTermination: forced,
                Failure: new WorkerClientFailure(
                    WorkerClientFailureCodes.WorkerProcessTreeIntegrityFailed,
                    ProcessTreeMessage));
        }

        ValidateCompletion(
            terminal,
            exitCode,
            cancellationWasRequested: false);
        return new ActiveRunResult(
            terminal,
            exitCode,
            ForcedTermination: false,
            Failure: null);
    }

    private static async Task<ProcessCompletion> CompleteConversationAndExitAsync(
        ProtectedWorkerSession session,
        Task<WorkerCompletedMessage> conversationTask,
        Task processExitTask,
        bool cancellationWasRequested)
    {
        WorkerCompletedMessage terminal = await conversationTask
            .ConfigureAwait(false);
        await processExitTask.ConfigureAwait(false);
        int exitCode = session.GetExitCode();
        if (!await session.WaitForTreeEmptyAsync()
            .ConfigureAwait(false))
        {
            throw PolicyFailure(
                WorkerClientFailureCodes.WorkerProcessTreeIntegrityFailed,
                ProcessTreeMessage);
        }

        ValidateCompletion(
            terminal,
            exitCode,
            cancellationWasRequested);
        return new ProcessCompletion(terminal, exitCode);
    }

    private static void ValidateCompletion(
        WorkerCompletedMessage terminal,
        int exitCode,
        bool cancellationWasRequested)
    {
        if (terminal.CompletionStatus == WorkerCompletionStatus.Cancelled &&
            !cancellationWasRequested)
        {
            throw PolicyFailure(
                WorkerClientFailureCodes.WorkerExitMismatch,
                ExitMismatchMessage);
        }

        if (!WorkerExitConsistencyValidator.IsConsistent(
                terminal.CompletionStatus,
                exitCode,
                forcedTermination: false))
        {
            throw PolicyFailure(
                WorkerClientFailureCodes.WorkerExitMismatch,
                ExitMismatchMessage);
        }
    }

    private async Task<WorkerHelloMessage> ReadAndValidateHelloAsync(
        BoundedUtf8LineReader stdoutReader,
        ProtectedWorkerSession session,
        Task processExitTask,
        Task callerSignal,
        CancellationToken callerToken)
    {
        Task<byte[]?> helloTask = stdoutReader
            .ReadLineAsync(CancellationToken.None)
            .AsTask();
        Task startupExpired = Task.Delay(
            _options.StartupTimeout,
            CancellationToken.None);
        Task winner = await Task.WhenAny(
                helloTask,
                startupExpired,
                callerSignal,
                processExitTask)
            .ConfigureAwait(false);

        if (winner == callerSignal)
        {
            throw new OperationCanceledException(callerToken);
        }

        if (winner == startupExpired)
        {
            throw PolicyFailure(
                WorkerClientFailureCodes.WorkerHandshakeTimeout,
                HandshakeTimeoutMessage);
        }

        byte[]? payload;
        if (winner == processExitTask && !helloTask.IsCompleted)
        {
            try
            {
                payload = await helloTask
                    .WaitAsync(
                        TimeSpan.FromMilliseconds(250),
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                throw PolicyFailure(
                    WorkerClientFailureCodes.WorkerCrashed,
                    WorkerCrashedMessage);
            }
        }
        else
        {
            payload = await helloTask.ConfigureAwait(false);
        }

        if (payload is null)
        {
            throw PolicyFailure(
                processExitTask.IsCompleted
                    ? WorkerClientFailureCodes.WorkerCrashed
                    : WorkerClientFailureCodes.WorkerHandshakeInvalid,
                processExitTask.IsCompleted
                    ? WorkerCrashedMessage
                    : HandshakeInvalidMessage);
        }

        object parsed;
        try
        {
            parsed = WorkerProtocolJson.DeserializeMessage(payload);
        }
        catch (WorkerProtocolException)
        {
            throw PolicyFailure(
                WorkerClientFailureCodes.WorkerHandshakeInvalid,
                HandshakeInvalidMessage);
        }

        WorkerHelloMessage hello = parsed as WorkerHelloMessage
            ?? throw PolicyFailure(
                WorkerClientFailureCodes.WorkerHandshakeInvalid,
                HandshakeInvalidMessage);
        WorkerHandshakeValidator.Validate(
            hello,
            checked((int)session.ProcessId));
        return hello;
    }

    private static async Task<WorkerCompletedMessage>
        ReadConversationToEndAsync(
            BoundedUtf8LineReader stdoutReader,
            WorkerConversation conversation,
            IProgress<WorkerProgressMessage>? progress)
    {
        while (true)
        {
            byte[]? payload = await stdoutReader
                .ReadLineAsync(CancellationToken.None)
                .ConfigureAwait(false);
            if (payload is null)
            {
                conversation.CompleteOutput();
                return conversation.TerminalMessage!;
            }

            object message = WorkerProtocolJson.DeserializeMessage(payload);
            conversation.Accept(message);
            if (message is WorkerProgressMessage progressMessage)
            {
                progress?.Report(progressMessage);
            }
        }
    }

    private static IReadOnlyDictionary<string, string?>
        CaptureParentEnvironment()
    {
        Dictionary<string, string?> values =
            new(StringComparer.OrdinalIgnoreCase);
        foreach (string key in new[]
                 {
                     "SystemRoot", "WINDIR", "DOTNET_ROOT", "DOTNET_ROOT_X64",
                 })
        {
            values[key] = Environment.GetEnvironmentVariable(key);
        }

        return values;
    }

    private static uint TryGetActiveProcessCount(
        ProtectedWorkerSession session)
    {
        try
        {
            return session.GetActiveProcessCount();
        }
        catch (Exception error) when (IsExpectedProcessFailure(error))
        {
            return 0;
        }
    }

    private static int? TryGetExitCode(ProtectedWorkerSession session)
    {
        try
        {
            return session.GetExitCode();
        }
        catch (Exception error) when (
            IsExpectedProcessFailure(error) ||
            error is WorkerClientPolicyException)
        {
            return null;
        }
    }

    private static WorkerClientPolicyException PolicyFailure(
        string code,
        string message) =>
        WorkerClientPolicyException.For(code, message);

    private static bool IsExpectedProcessFailure(Exception error) =>
        error is IOException or
        UnauthorizedAccessException or
        ObjectDisposedException or
        InvalidOperationException or
        System.ComponentModel.Win32Exception or
        OverflowException;

    private sealed record ProcessCompletion(
        WorkerCompletedMessage TerminalMessage,
        int ExitCode);

    private sealed record ActiveRunResult(
        WorkerCompletedMessage? TerminalMessage,
        int? ExitCode,
        bool ForcedTermination,
        WorkerClientFailure? Failure);
}
