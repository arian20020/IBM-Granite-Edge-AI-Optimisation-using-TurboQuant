using GraniteEdgeAI.HardwareInspection.Foundation.Processes;
using GraniteEdgeAI.ModelInspection.Transport;
using GraniteEdgeAI.ModelInspection.WorkerClient;
using GraniteEdgeAI.ModelInspection.WorkerClient.ProtectedWorker;
using GraniteEdgeAI.OpenVino.Contracts;
using System.Runtime.ExceptionServices;

namespace GraniteEdgeAI.OpenVino.WorkerClient;

/// <summary>
/// Connects the strict OpenVINO JSONL contract to the shared protected-process
/// facade. Payload data crosses stdin only and never enters process arguments.
/// </summary>
public sealed class OpenVinoWorkerClient : IOpenVinoWorkerClient
{
    private const string ProtocolFailureMessage =
        "The OpenVINO worker protocol conversation failed validation.";
    private const string TimeoutMessage =
        "The OpenVINO worker exceeded an approved time limit.";
    private const string CancellationMessage =
        "The OpenVINO operation was cancelled.";
    private const string RuntimeFailureMessage =
        "The OpenVINO worker could not run inside the protected boundary.";

    private readonly OpenVinoWorkerClientOptions _options;
    private readonly OpenVinoWorkerClosureResolver _closureResolver;
    private readonly IProtectedWorkerSessionFactory _sessionFactory;
    private readonly Func<IReadOnlyDictionary<string, string?>>
        _environmentProvider;

    public OpenVinoWorkerClient(OpenVinoWorkerClientOptions options)
        : this(
            options,
            new ProtectedWorkerSessionFactory(),
            CaptureParentEnvironment)
    {
    }

    internal OpenVinoWorkerClient(
        OpenVinoWorkerClientOptions options,
        IProtectedWorkerSessionFactory sessionFactory,
        Func<IReadOnlyDictionary<string, string?>> environmentProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(sessionFactory);
        ArgumentNullException.ThrowIfNull(environmentProvider);
        options.Validate();
        _options = options;
        _closureResolver = new OpenVinoWorkerClosureResolver();
        _sessionFactory = sessionFactory;
        _environmentProvider = environmentProvider;
    }

    public Task<IOpenVinoEvent> InspectAsync(
        StartInspectionCommand command,
        CancellationToken cancellationToken) =>
        InspectAsync(command, progress: null, cancellationToken);

    public async Task<IOpenVinoEvent> InspectAsync(
        StartInspectionCommand command,
        IProgress<InspectionProgressEvent>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Validate();
        ProtectedWorkerSession? session = null;
        Task<StandardErrorSnapshot>? stderrTask = null;
        VerifiedOpenVinoWorkerClosure? closure = null;
        TrustedToolOperationEnvironment? operationEnvironment = null;
        IOpenVinoEvent? result = null;
        OpenVinoWorkerClientException? mappedFailure = null;
        Exception? controlledPrimary = null;
        Exception? unexpectedFailure = null;
        CleanupOutcome? cleanupOutcome = null;
        try
        {
            DateTimeOffset startupDeadline =
                DateTimeOffset.UtcNow + _options.StartupTimeout;
            (session, stderrTask, closure, operationEnvironment) =
                await StartProtectedAsync(cancellationToken)
                .ConfigureAwait(false);
            Task processExit = session.WaitForExitAsync(CancellationToken.None);
            OpenVinoConversationValidator validator = new();
            HelloEvent hello = await ReadHelloAsync(
                    session,
                    processExit,
                    startupDeadline,
                    cancellationToken)
                .ConfigureAwait(false);
            validator.Accept(hello);
            validator.Accept(command);
            DateTimeOffset operationDeadline =
                DateTimeOffset.UtcNow + _options.TurnTimeout;
            await WriteWithDeadlineAsync(
                    session,
                    OpenVinoProtocolJson.Serialize(command),
                    operationDeadline,
                    cancellationToken)
                .ConfigureAwait(false);

            IOpenVinoEvent terminal = await ReadInspectionTerminalAsync(
                    session,
                    processExit,
                    validator,
                    command.InspectionRunId,
                    progress,
                    operationDeadline,
                    cancellationToken)
                .ConfigureAwait(false);
            await session.CompleteInputAsync().ConfigureAwait(false);
            await RequireCleanExitAsync(session, processExit)
                .ConfigureAwait(false);
            result = terminal;
        }
        catch (Exception error) when (IsControlled(error))
        {
            controlledPrimary = error;
            mappedFailure = await ConvertFailureAsync(
                    error,
                    session,
                    stderrTask,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception error)
        {
            unexpectedFailure = error;
        }
        finally
        {
            cleanupOutcome = await CleanupOwnedAsync(
                    session,
                    closure,
                    operationEnvironment)
                .ConfigureAwait(false);
        }

        if (!cleanupOutcome.Succeeded)
        {
            Exception integrity = CleanupIntegrityException.PreserveCancellation(
                cleanupOutcome.Failures,
                controlledPrimary ?? unexpectedFailure);
            if (integrity is OperationCanceledException cancellation)
            {
                throw cancellation;
            }
            throw RuntimeFailure(integrity);
        }

        if (mappedFailure is not null)
        {
            throw mappedFailure;
        }

        if (unexpectedFailure is not null)
        {
            ExceptionDispatchInfo.Capture(unexpectedFailure).Throw();
        }

        return result ?? throw RuntimeFailure();
    }

    public async Task<OpenVinoConversation> StartSessionAsync(
        StartSessionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Validate();
        ProtectedWorkerSession? session = null;
        Task<StandardErrorSnapshot>? stderrTask = null;
        VerifiedOpenVinoWorkerClosure? closure = null;
        TrustedToolOperationEnvironment? operationEnvironment = null;
        OpenVinoConversation? result = null;
        OpenVinoWorkerClientException? mappedFailure = null;
        Exception? controlledPrimary = null;
        Exception? unexpectedFailure = null;
        CleanupOutcome? cleanupOutcome = null;
        try
        {
            DateTimeOffset startupDeadline =
                DateTimeOffset.UtcNow + _options.StartupTimeout;
            (session, stderrTask, closure, operationEnvironment) =
                await StartProtectedAsync(cancellationToken)
                .ConfigureAwait(false);
            Task processExit = session.WaitForExitAsync(CancellationToken.None);
            OpenVinoConversationValidator validator = new();
            HelloEvent hello = await ReadHelloAsync(
                    session,
                    processExit,
                    startupDeadline,
                    cancellationToken)
                .ConfigureAwait(false);
            validator.Accept(hello);
            validator.Accept(command);
            DateTimeOffset sessionLoadDeadline =
                DateTimeOffset.UtcNow + _options.SessionLoadTimeout;
            await WriteWithDeadlineAsync(
                    session,
                    OpenVinoProtocolJson.Serialize(command),
                    sessionLoadDeadline,
                    cancellationToken)
                .ConfigureAwait(false);

            IOpenVinoEvent started = await ReadEventWithDeadlineAsync(
                    session,
                    processExit,
                    RemainingUntil(sessionLoadDeadline),
                    cancellationToken)
                .ConfigureAwait(false);
            validator.Accept(started);
            if (started is SessionFailedEvent failed)
            {
                throw WorkerReportedFailure(failed.SupportCode);
            }

            if (started is not SessionStartedEvent startupEvidence)
            {
                throw ProtocolFailure();
            }

            result = new OpenVinoConversation(
                session,
                stderrTask,
                processExit,
                validator,
                command.SessionId,
                startupEvidence,
                _options,
                closure,
                operationEnvironment);
            session = null;
            stderrTask = null;
            closure = null;
            operationEnvironment = null;
        }
        catch (Exception error) when (IsControlled(error))
        {
            controlledPrimary = error;
            mappedFailure = await ConvertFailureAsync(
                    error,
                    session,
                    stderrTask,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception error)
        {
            unexpectedFailure = error;
        }
        finally
        {
            cleanupOutcome = await CleanupOwnedAsync(
                    session,
                    closure,
                    operationEnvironment)
                .ConfigureAwait(false);
        }

        if (!cleanupOutcome.Succeeded)
        {
            Exception integrity = CleanupIntegrityException.PreserveCancellation(
                cleanupOutcome.Failures,
                controlledPrimary ?? unexpectedFailure);
            if (integrity is OperationCanceledException cancellation)
            {
                throw cancellation;
            }
            throw RuntimeFailure(integrity);
        }

        if (mappedFailure is not null)
        {
            throw mappedFailure;
        }

        if (unexpectedFailure is not null)
        {
            ExceptionDispatchInfo.Capture(unexpectedFailure).Throw();
        }

        return result ?? throw RuntimeFailure();
    }

    private async Task<(
        ProtectedWorkerSession Session,
        Task<StandardErrorSnapshot> StandardError,
        VerifiedOpenVinoWorkerClosure Closure,
        TrustedToolOperationEnvironment OperationEnvironment)>
        StartProtectedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        VerifiedOpenVinoWorkerClosure closure = _closureResolver.Resolve(
            _options.Installation);
        TrustedToolOperationEnvironment? operationEnvironment = null;
        ProtectedWorkerSession? session = null;
        try
        {
            operationEnvironment = TrustedToolOperationEnvironment.Create(
                _environmentProvider());
            ProtectedWorkerLaunchSpec spec = new(
                closure.Executable,
                ["--protocol", _options.Installation.ExpectedProtocolId],
                operationEnvironment.Variables,
                _options.MaximumLineBytes,
                _options.MaximumLineBytes,
                _options.MaximumRetainedStandardErrorBytes,
                _options.StartupTimeout,
                _options.CancellationGrace,
                _options.CleanupTimeout);
            session = await _sessionFactory.StartAsync(
                    spec,
                    cancellationToken)
                .ConfigureAwait(false);
            return (session, session.ReadStandardErrorAsync(), closure, operationEnvironment);
        }
        catch (Exception primaryFailure)
        {
            CleanupOutcome cleanup = await CleanupOwnedAsync(
                    session,
                    closure,
                    operationEnvironment)
                .ConfigureAwait(false);
            if (!cleanup.Succeeded)
            {
                throw RuntimeFailure(new CleanupIntegrityException(
                    cleanup.Failures,
                    primaryFailure));
            }

            throw;
        }
    }

    private async Task<HelloEvent> ReadHelloAsync(
        ProtectedWorkerSession session,
        Task processExit,
        DateTimeOffset startupDeadline,
        CancellationToken cancellationToken)
    {
        IOpenVinoEvent first = await ReadEventWithDeadlineAsync(
                session,
                processExit,
                RemainingUntil(startupDeadline),
                cancellationToken)
            .ConfigureAwait(false);
        if (first is not HelloEvent hello ||
            !string.Equals(
                hello.ProtocolId,
                _options.Installation.ExpectedProtocolId,
                StringComparison.Ordinal) ||
            hello.BuildEvidence != _options.Installation.ExpectedBuildEvidence)
        {
            throw ProtocolFailure();
        }

        return hello;
    }

    private static async Task<IOpenVinoEvent> ReadInspectionTerminalAsync(
        ProtectedWorkerSession session,
        Task processExit,
        OpenVinoConversationValidator validator,
        Guid inspectionRunId,
        IProgress<InspectionProgressEvent>? progress,
        DateTimeOffset deadline,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                throw TimeoutFailure();
            }

            IOpenVinoEvent @event = await ReadEventWithDeadlineAsync(
                    session,
                    processExit,
                    remaining,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!TryAcceptInspectionEvent(
                    @event,
                    validator,
                    inspectionRunId,
                    progress,
                    out IOpenVinoEvent? terminal))
            {
                continue;
            }

            if (terminal is not null)
            {
                return terminal;
            }
        }
    }

    internal static bool TryAcceptInspectionEvent(
        IOpenVinoEvent @event,
        OpenVinoConversationValidator validator,
        Guid inspectionRunId,
        IProgress<InspectionProgressEvent>? progress,
        out IOpenVinoEvent? terminal)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(validator);
        terminal = null;
        Guid? eventRunId = @event switch
        {
            InspectionStartedEvent started => started.InspectionRunId,
            InspectionProgressEvent update => update.InspectionRunId,
            InspectionCompletedEvent completed => completed.InspectionRunId,
            InspectionFailedEvent failed => failed.InspectionRunId,
            _ => null
        };
        if (eventRunId.HasValue && eventRunId.Value != inspectionRunId)
        {
            return false;
        }

        validator.Accept(@event);
        if (@event is InspectionProgressEvent progressEvent)
        {
            progress?.Report(progressEvent);
        }
        else if (@event is InspectionCompletedEvent or InspectionFailedEvent)
        {
            terminal = @event;
        }

        return true;
    }

    private static async Task WriteWithDeadlineAsync(
        ProtectedWorkerSession session,
        ReadOnlyMemory<byte> payload,
        DateTimeOffset deadline,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource deadlineCancellation = new(
            RemainingUntil(deadline));
        using CancellationTokenSource linked =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                deadlineCancellation.Token);
        try
        {
            await session.StandardInput.WriteLineAsync(payload, linked.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested &&
                deadlineCancellation.IsCancellationRequested)
        {
            throw TimeoutFailure();
        }
    }

    private static TimeSpan RemainingUntil(DateTimeOffset deadline)
    {
        TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            throw TimeoutFailure();
        }

        return remaining;
    }

    internal static async Task<IOpenVinoEvent> ReadEventWithDeadlineAsync(
        ProtectedWorkerSession session,
        Task processExit,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        Task<byte[]?> read = session.StandardOutput
            .ReadLineAsync(cancellationToken)
            .AsTask();
        Task deadline = Task.Delay(timeout, CancellationToken.None);
        Task caller = cancellationToken.CanBeCanceled
            ? Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)
            : Task.Delay(Timeout.InfiniteTimeSpan, CancellationToken.None);
        Task winner = await Task.WhenAny(read, processExit, deadline, caller)
            .ConfigureAwait(false);
        if (winner == caller)
        {
            try
            {
                _ = await read.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }

            throw new OperationCanceledException(cancellationToken);
        }

        if (winner == deadline)
        {
            throw TimeoutFailure();
        }

        if (winner == processExit && !read.IsCompleted)
        {
            try
            {
                await read.WaitAsync(
                        session.CleanupTimeout,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                throw ProtocolFailure();
            }
        }

        byte[]? payload = await read.ConfigureAwait(false);
        if (payload is null)
        {
            throw ProtocolFailure();
        }

        return OpenVinoProtocolJson.DeserializeEvent(payload);
    }

    private async Task RequireCleanExitAsync(
        ProtectedWorkerSession session,
        Task processExit)
    {
        await processExit.WaitAsync(_options.CleanupTimeout)
            .ConfigureAwait(false);
        if (!await session.WaitForTreeEmptyAsync().ConfigureAwait(false))
        {
            throw ProtocolFailure();
        }

        byte[]? extra = await session.StandardOutput
            .ReadLineAsync(CancellationToken.None)
            .ConfigureAwait(false);
        if (extra is not null)
        {
            throw ProtocolFailure();
        }
    }

    internal static OpenVinoWorkerClientException ProtocolFailure() => new(
        OpenVinoSupportCode.RuntimeProtocolFailed,
        ProtocolFailureMessage);

    internal static OpenVinoWorkerClientException TimeoutFailure() => new(
        OpenVinoSupportCode.RuntimeTimedOut,
        TimeoutMessage);

    internal static OpenVinoWorkerClientException CancellationFailure() => new(
        OpenVinoSupportCode.OperationCancelled,
        CancellationMessage);

    internal static OpenVinoWorkerClientException WorkerReportedFailure(
        OpenVinoSupportCode supportCode) => new(
        supportCode,
        RuntimeFailureMessage);

    internal static OpenVinoWorkerClientException RuntimeFailure(
        Exception? innerException = null) => new(
        OpenVinoSupportCode.RuntimeIntegrityFailed,
        RuntimeFailureMessage,
        innerException: innerException);

    private static Task<CleanupOutcome> CleanupOwnedAsync(
        ProtectedWorkerSession? session,
        VerifiedOpenVinoWorkerClosure? closure,
        TrustedToolOperationEnvironment? operationEnvironment) =>
        new BoundedCleanupCoordinator(
            new OwnedCleanupAction(OwnedCleanupStage.Session, () =>
                session?.DisposeAsync() ?? ValueTask.CompletedTask),
            Sync(OwnedCleanupStage.Closure, () => closure?.Dispose()),
            Sync(OwnedCleanupStage.OperationEnvironment, () =>
            {
                operationEnvironment?.Dispose();
                if (operationEnvironment is not null &&
                    !operationEnvironment.CleanupSucceeded)
                {
                    throw new InvalidOperationException(
                        "The OpenVINO operation environment cleanup could not be verified.");
                }
            })).ExecuteAsync();

    private static OwnedCleanupAction Sync(
        OwnedCleanupStage stage,
        Action action) => new(stage, () =>
        {
            action();
            return ValueTask.CompletedTask;
        });

    internal static void RequireCleanupSucceeded(
        TrustedToolOperationEnvironment operationEnvironment)
    {
        if (!operationEnvironment.CleanupSucceeded)
        {
            throw RuntimeFailure();
        }
    }

    internal static bool IsControlled(Exception error) =>
        error is OpenVinoWorkerClientException or
        OpenVinoProtocolException or
        ProtocolStreamException or
        WorkerClientPolicyException or
        OperationCanceledException or
        TimeoutException or
        IOException or
        UnauthorizedAccessException or
        InvalidOperationException or
        ObjectDisposedException or
        System.ComponentModel.Win32Exception;

    internal static async Task<OpenVinoWorkerClientException>
        ConvertFailureAsync(
            Exception error,
            ProtectedWorkerSession? session,
            Task<StandardErrorSnapshot>? stderrTask,
            CancellationToken callerToken)
    {
        bool cleanupIntegrityFailed = false;
        if (session is not null)
        {
            try
            {
                _ = await session.TerminateAndVerifyEmptyAsync()
                    .ConfigureAwait(false);
            }
            catch (Exception cleanupError) when (IsControlled(cleanupError))
            {
                cleanupIntegrityFailed = true;
            }
        }

        StandardErrorSnapshot? stderr = null;
        if (stderrTask is not null)
        {
            try
            {
                stderr = await stderrTask.WaitAsync(
                        TimeSpan.FromSeconds(5),
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception stderrError) when (IsControlled(stderrError))
            {
                cleanupIntegrityFailed = true;
            }
        }

        if (cleanupIntegrityFailed)
        {
            return RuntimeFailure(error);
        }

        OpenVinoWorkerClientException mapped = error switch
        {
            OpenVinoWorkerClientException known => known,
            OperationCanceledException when callerToken.IsCancellationRequested =>
                CancellationFailure(),
            TimeoutException => TimeoutFailure(),
            OpenVinoProtocolException or ProtocolStreamException =>
                ProtocolFailure(),
            IOException when session is not null => ProtocolFailure(),
            _ => new OpenVinoWorkerClientException(
                OpenVinoSupportCode.RuntimeIntegrityFailed,
                RuntimeFailureMessage)
        };
        return new OpenVinoWorkerClientException(
            mapped.SupportCode,
            mapped.Message,
            retainedStandardError: string.Empty,
            standardErrorTruncated: stderr?.IsTruncated ?? false);
    }

    internal static IReadOnlyDictionary<string, string?>
        CaptureParentEnvironment()
    {
        Dictionary<string, string?> values =
            new(StringComparer.OrdinalIgnoreCase);
        foreach (string key in new[] { "SystemRoot", "WINDIR" })
        {
            values[key] = Environment.GetEnvironmentVariable(key);
        }

        return values;
    }
}
