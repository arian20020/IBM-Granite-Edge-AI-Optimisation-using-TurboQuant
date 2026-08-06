using System.Collections;
using GraniteEdgeAI.ModelInspection.Contracts;
using GraniteEdgeAI.ModelInspection.Transport;
using GraniteEdgeAI.ModelInspection.WorkerClient.Windows;

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
    private const string ExitMismatchMessage =
        "The Model Inspection worker terminal result did not match its process exit.";
    private const string ProcessTreeMessage =
        "The Model Inspection worker process tree did not become empty.";

    private readonly WorkerClientOptions _options;
    private readonly WorkerExecutableResolver _resolver;
    private readonly IReadOnlyList<string> _testOnlyArguments;
    private readonly Func<IReadOnlyDictionary<string, string?>>
        _parentEnvironmentProvider;

    /// <summary>
    /// Creates the production adapter. Production composition supplies the
    /// fixed worker-relative executable path and cannot select fixture modes.
    /// </summary>
    public InspectionWorkerClient(
        WorkerClientOptions options,
        string fixedWorkerRelativePath)
        : this(
            options,
            fixedWorkerRelativePath,
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
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(fixedWorkerRelativePath);
        ArgumentNullException.ThrowIfNull(testOnlyArguments);
        ArgumentNullException.ThrowIfNull(parentEnvironmentProvider);

        options.Validate();
        _options = options;
        _resolver = new WorkerExecutableResolver(fixedWorkerRelativePath);
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
        WorkerProcessSession? session = null;
        Task<StandardErrorSnapshot>? standardErrorTask = null;
        StandardErrorSnapshot standardError = new(
            retainedText: string.Empty,
            isTruncated: false,
            invalidUtf8Detected: false);
        WorkerCompletedMessage? terminal = null;
        int? exitCode = null;
        bool forcedTermination = false;
        bool handshakeCompleted = false;

        try
        {
            using VerifiedWorkerExecutable executable =
                _resolver.Resolve(_options.ApprovedWorkerRoot);
            IReadOnlyDictionary<string, string> environment =
                WorkerEnvironmentPolicy.Create(_parentEnvironmentProvider());
            WindowsProcessLaunchRequest launchRequest = new(
                executable,
                _options.ApprovedWorkerRoot,
                environment,
                _testOnlyArguments);

            session = WindowsWorkerProcessLauncher.Launch(launchRequest);

            // Start both observation tasks immediately so neither redirected
            // stream nor the process handle can deadlock behind sequential I/O.
            BoundedStandardErrorCollector stderrCollector = new(
                _options.MaximumRetainedStandardErrorBytes);
            standardErrorTask = stderrCollector.DrainAsync(
                session.StandardError,
                CancellationToken.None);
            Task processExitTask = session.WaitForExitAsync(
                CancellationToken.None);
            BoundedUtf8LineReader stdoutReader = new(
                session.StandardOutput,
                WorkerProtocol.MaximumMessageBytes);

            using CancellationTokenSource overallTimeout =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);
            overallTimeout.CancelAfter(_options.OverallTimeout);

            WorkerHelloMessage hello = await ReadAndValidateHelloAsync(
                    stdoutReader,
                    session,
                    processExitTask,
                    overallTimeout.Token,
                    cancellationToken)
                .ConfigureAwait(false);
            handshakeCompleted = true;

            using BoundedUtf8LineWriter stdinWriter = new(
                session.StandardInput,
                WorkerProtocol.MaximumMessageBytes);
            await stdinWriter.WriteLineAsync(
                    WorkerProtocolJson.Serialize(command),
                    overallTimeout.Token)
                .ConfigureAwait(false);

            WorkerConversation conversation = new(
                hello,
                command.RequestId);
            terminal = await ReadConversationToEndAsync(
                    stdoutReader,
                    conversation,
                    progress,
                    overallTimeout.Token)
                .ConfigureAwait(false);

            // No further commands are valid after the terminal result. Closing
            // stdin also lets a worker waiting for EOF complete naturally.
            await session.StandardInput.DisposeAsync().ConfigureAwait(false);
            await processExitTask.WaitAsync(overallTimeout.Token)
                .ConfigureAwait(false);
            exitCode = session.GetExitCode();
            standardError = await standardErrorTask.ConfigureAwait(false);

            if (session.Job.GetActiveProcessCount() != 0)
            {
                throw PolicyFailure(
                    WorkerClientFailureCodes.WorkerProcessTreeIntegrityFailed,
                    ProcessTreeMessage);
            }

            if (!WorkerExitConsistencyValidator.IsConsistent(
                    terminal.CompletionStatus,
                    exitCode.Value,
                    forcedTermination: false))
            {
                throw PolicyFailure(
                    WorkerClientFailureCodes.WorkerExitMismatch,
                    ExitMismatchMessage);
            }
        }
        catch (WorkerClientPolicyException error)
        {
            _ = failures.TrySetPrimary(error.Failure);
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
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _ = failures.TrySetPrimary(new WorkerClientFailure(
                handshakeCompleted
                    ? WorkerClientFailureCodes.WorkerOverallTimeout
                    : WorkerClientFailureCodes.WorkerHandshakeTimeout,
                handshakeCompleted
                    ? OverallTimeoutMessage
                    : HandshakeTimeoutMessage));
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
                try
                {
                    forcedTermination =
                        session.Job.GetActiveProcessCount() != 0;
                }
                catch (Exception error) when (IsExpectedProcessFailure(error))
                {
                    failures.AddSecondary(error);
                }

                try
                {
                    await session.DisposeAsync().ConfigureAwait(false);
                }
                catch (WorkerClientPolicyException error)
                {
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

            if (standardErrorTask is not null &&
                !standardErrorTask.IsCompletedSuccessfully)
            {
                try
                {
                    standardError = await standardErrorTask
                        .WaitAsync(_options.ProcessTreeCleanupTimeout)
                        .ConfigureAwait(false);
                }
                catch (Exception error) when (
                    IsExpectedProcessFailure(error) ||
                    error is TimeoutException)
                {
                    failures.AddSecondary(error);
                }
            }
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
            failures.SecondaryDiagnostics);
        result.Validate();
        return result;
    }

    private async Task<WorkerHelloMessage> ReadAndValidateHelloAsync(
        BoundedUtf8LineReader stdoutReader,
        WorkerProcessSession session,
        Task processExitTask,
        CancellationToken operationToken,
        CancellationToken callerToken)
    {
        using CancellationTokenSource startupTimeout =
            CancellationTokenSource.CreateLinkedTokenSource(operationToken);
        startupTimeout.CancelAfter(_options.StartupTimeout);

        byte[]? payload;
        try
        {
            payload = await stdoutReader.ReadLineAsync(startupTimeout.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (!callerToken.IsCancellationRequested &&
                  !operationToken.IsCancellationRequested)
        {
            throw PolicyFailure(
                WorkerClientFailureCodes.WorkerHandshakeTimeout,
                HandshakeTimeoutMessage);
        }

        if (payload is null)
        {
            if (processExitTask.IsCompleted)
            {
                throw PolicyFailure(
                    WorkerClientFailureCodes.WorkerCrashed,
                    WorkerCrashedMessage);
            }

            throw PolicyFailure(
                WorkerClientFailureCodes.WorkerHandshakeInvalid,
                HandshakeInvalidMessage);
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
            IProgress<WorkerProgressMessage>? progress,
            CancellationToken cancellationToken)
    {
        while (true)
        {
            byte[]? payload = await stdoutReader
                .ReadLineAsync(cancellationToken)
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
        foreach (DictionaryEntry entry in
                 Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string key)
            {
                values[key] = entry.Value as string;
            }
        }

        return values;
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
}
