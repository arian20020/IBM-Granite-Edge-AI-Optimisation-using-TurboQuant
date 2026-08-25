using System.Collections;
using GraniteEdgeAI.ModelInspection.Transport;
using GraniteEdgeAI.ModelInspection.WorkerClient;
using GraniteEdgeAI.ModelInspection.WorkerClient.ProtectedWorker;
using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.OpenVino.WorkerClient;

/// <summary>
/// Connects the strict OpenVINO JSONL contract to the shared protected-process
/// facade. Payload data crosses stdin only and never enters process arguments.
/// </summary>
public sealed class OpenVinoWorkerClient : IOpenVinoInspectionProgressClient
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

    public async Task<IOpenVinoEvent> InspectAsync(
        StartInspectionCommand command,
        CancellationToken cancellationToken) =>
        await InspectAsync(command, progress: null, cancellationToken)
            .ConfigureAwait(false);

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
        try
        {
            DateTimeOffset startupDeadline =
                DateTimeOffset.UtcNow + _options.StartupTimeout;
            (session, stderrTask, closure) = await StartProtectedAsync(cancellationToken)
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
            return terminal;
        }
        catch (Exception error) when (IsControlled(error))
        {
            throw await ConvertFailureAsync(
                    error,
                    session,
                    stderrTask,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            if (session is not null)
            {
                await session.DisposeAsync().ConfigureAwait(false);
            }

            closure?.Dispose();
        }
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
        try
        {
            DateTimeOffset startupDeadline =
                DateTimeOffset.UtcNow + _options.StartupTimeout;
            (session, stderrTask, closure) = await StartProtectedAsync(cancellationToken)
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
            await WriteWithDeadlineAsync(
                    session,
                    OpenVinoProtocolJson.Serialize(command),
                    startupDeadline,
                    cancellationToken)
                .ConfigureAwait(false);

            IOpenVinoEvent started = await ReadEventWithDeadlineAsync(
                    session,
                    processExit,
                    RemainingUntil(startupDeadline),
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

            OpenVinoConversation result = new(
                session,
                stderrTask,
                processExit,
                validator,
                command.SessionId,
                startupEvidence,
                _options,
                closure);
            session = null;
            stderrTask = null;
            closure = null;
            return result;
        }
        catch (Exception error) when (IsControlled(error))
        {
            throw await ConvertFailureAsync(
                    error,
                    session,
                    stderrTask,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            if (session is not null)
            {
                await session.DisposeAsync().ConfigureAwait(false);
            }

            closure?.Dispose();
        }
    }

    private async Task<(
        ProtectedWorkerSession Session,
        Task<StandardErrorSnapshot> StandardError,
        VerifiedOpenVinoWorkerClosure Closure)>
        StartProtectedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        VerifiedOpenVinoWorkerClosure closure = _closureResolver.Resolve(
            _options.Installation);
        try
        {
            IReadOnlyDictionary<string, string> environment =
                WorkerEnvironmentPolicy.Create(_environmentProvider());
            ProtectedWorkerLaunchSpec spec = new(
                closure.Executable,
                ["--protocol", _options.Installation.ExpectedProtocolId],
                environment,
                _options.MaximumLineBytes,
                _options.MaximumLineBytes,
                _options.MaximumRetainedStandardErrorBytes,
                _options.StartupTimeout,
                _options.CancellationGrace,
                _options.CleanupTimeout);
            ProtectedWorkerSession session = await _sessionFactory.StartAsync(
                    spec,
                    cancellationToken)
                .ConfigureAwait(false);
            return (session, session.ReadStandardErrorAsync(), closure);
        }
        catch
        {
            closure.Dispose();
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
            if (@event is InspectionProgressEvent progressEvent &&
                progressEvent.InspectionRunId != inspectionRunId ||
                @event is InspectionCompletedEvent completed &&
                completed.InspectionRunId != inspectionRunId ||
                @event is InspectionFailedEvent failed &&
                failed.InspectionRunId != inspectionRunId)
            {
                continue;
            }

            validator.Accept(@event);
            if (@event is InspectionProgressEvent acceptedProgress)
            {
                progress?.Report(acceptedProgress);
            }
            if (@event is InspectionCompletedEvent or InspectionFailedEvent)
            {
                return @event;
            }
        }
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
        if (session is not null)
        {
            try
            {
                _ = await session.TerminateAndVerifyEmptyAsync()
                    .ConfigureAwait(false);
            }
            catch (Exception cleanupError) when (IsControlled(cleanupError))
            {
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
            }
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
            SanitizeDiagnostic(stderr?.RetainedText ?? string.Empty),
            stderr?.IsTruncated ?? false);
    }

    private static string SanitizeDiagnostic(string value)
    {
        char[] characters = value.ToCharArray();
        for (int index = 0; index < characters.Length; index++)
        {
            char character = characters[index];
            if (char.IsControl(character) &&
                character is not '\r' and not '\n' and not '\t')
            {
                characters[index] = ' ';
            }
        }

        return new string(characters);
    }

    private static IReadOnlyDictionary<string, string?>
        CaptureParentEnvironment()
    {
        Dictionary<string, string?> values =
            new(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string key)
            {
                values[key] = entry.Value as string;
            }
        }

        return values;
    }
}
