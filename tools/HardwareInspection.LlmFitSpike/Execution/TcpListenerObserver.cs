using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace HardwareInspection.LlmFitSpike.Execution;

public sealed record LlmFitProcessObservation(
    bool CandidateSocketObserved,
    bool DashboardPortObserved,
    IReadOnlyList<int> CandidateListeningPorts,
    bool CandidateProcessRemainedAfterExit);

internal readonly record struct CandidateProcessIdentity(int ProcessId, long StartTimeUtcTicks);

internal interface INetstatProcess : IDisposable
{
    Stream StandardOutput { get; }

    Stream StandardError { get; }

    int ExitCode { get; }

    bool HasExited { get; }

    bool Start();

    Task WaitForExitAsync();

    void KillEntireProcessTree();
}

/// <summary>
/// Samples candidate-owned Windows TCP rows as diagnostic evidence. A clean
/// sample is not proof that no network system call occurred; the controlled
/// offline gate supplies that separate operational control.
/// </summary>
public sealed class TcpListenerObserver
{
    public const int LlmFitDashboardPort = 8787;

    private const int MaximumNetstatBytesPerStream = 1_048_576;
    private static readonly TimeSpan HelperCleanupDeadline = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan HelperExecutionTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RequiredPollInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan ResidualObservationWindow = TimeSpan.FromSeconds(2);
    private readonly string _candidateProcessName;
    private readonly Func<CancellationToken, Task<string>> _captureNetstat;
    private readonly Func<int, CandidateProcessIdentity?> _captureRootIdentity;
    private readonly object _completionLock = new();
    private readonly ConcurrentDictionary<int, byte> _listeningPorts = new();
    private readonly TimeSpan _pollInterval;
    private readonly Dictionary<int, CandidateProcessIdentity> _prelaunchProcesses;
    private readonly TimeSpan _residualObservationWindow;
    private readonly Func<IReadOnlyList<CandidateProcessIdentity>> _snapshotCandidateProcesses;
    private readonly TaskCompletionSource _observationEnded = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private Task<LlmFitProcessObservation>? _completionTask;
    private CandidateProcessIdentity? _rootProcess;
    private int _candidateSocketObserved;
    private int _dashboardPortObserved;
    private int _observationStarted;
    private int _sampleCount;

    public TcpListenerObserver(string candidateImageName, TimeSpan pollInterval)
        : this(
            candidateImageName,
            pollInterval,
            CaptureNetstatAsync,
            ResidualObservationWindow)
    {
    }

    internal TcpListenerObserver(
        string candidateImageName,
        TimeSpan pollInterval,
        Func<CancellationToken, Task<string>> captureNetstat,
        TimeSpan residualObservationWindow)
        : this(
            candidateImageName,
            pollInterval,
            captureNetstat,
            residualObservationWindow,
            snapshotCandidateProcesses: null,
            captureRootIdentity: null)
    {
    }

    internal TcpListenerObserver(
        string candidateImageName,
        TimeSpan pollInterval,
        Func<CancellationToken, Task<string>> captureNetstat,
        TimeSpan residualObservationWindow,
        Func<IReadOnlyList<CandidateProcessIdentity>>? snapshotCandidateProcesses,
        Func<int, CandidateProcessIdentity?>? captureRootIdentity)
    {
        string validatedImageName = ValidateCandidateImageName(candidateImageName);
        _candidateProcessName = Path.GetFileNameWithoutExtension(validatedImageName);
        if (pollInterval != RequiredPollInterval)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pollInterval),
                "LLM Fit socket observation uses a fixed 50 millisecond interval.");
        }

        if (residualObservationWindow <= TimeSpan.Zero ||
            residualObservationWindow > ResidualObservationWindow)
        {
            throw new ArgumentOutOfRangeException(nameof(residualObservationWindow));
        }

        _pollInterval = pollInterval;
        _captureNetstat = captureNetstat ?? throw new ArgumentNullException(nameof(captureNetstat));
        _residualObservationWindow = residualObservationWindow;
        _snapshotCandidateProcesses = snapshotCandidateProcesses ?? SnapshotCandidateProcessIdentities;
        _captureRootIdentity = captureRootIdentity ?? CaptureRootProcessIdentity;
        _prelaunchProcesses = CaptureCandidateProcessSnapshot();
    }

    internal int SampleCount => Volatile.Read(ref _sampleCount);

    public async Task ObserveWhileRunningAsync(
        int rootProcessId,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Candidate-owned TCP observation requires Windows netstat.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rootProcessId);
        if (Interlocked.CompareExchange(ref _observationStarted, 1, 0) != 0)
        {
            throw new InvalidOperationException("This socket observer has already been started.");
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _rootProcess = CaptureVerifiedRootIdentity(rootProcessId);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Dictionary<int, CandidateProcessIdentity> processesBeforeSample =
                    CaptureCandidateProcessSnapshot();
                string output = await _captureNetstat(cancellationToken).ConfigureAwait(false);
                Dictionary<int, CandidateProcessIdentity> processesAfterSample =
                    CaptureCandidateProcessSnapshot();
                HashSet<int> attributedProcessIds = GetStableAttributedProcessIds(
                    processesBeforeSample,
                    processesAfterSample);
                RecordCandidateRows(output, attributedProcessIds);
                Interlocked.Increment(ref _sampleCount);
                await Task.Delay(_pollInterval, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _observationEnded.TrySetResult();
        }
    }

    public Task<LlmFitProcessObservation> CompleteAsync(
        CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _observationStarted) == 0)
        {
            return Task.FromException<LlmFitProcessObservation>(
                new InvalidOperationException("Socket observation has not been started."));
        }

        Task<LlmFitProcessObservation> completion;
        lock (_completionLock)
        {
            _completionTask ??= CompleteCoreAsync();
            completion = _completionTask;
        }

        return cancellationToken.CanBeCanceled
            ? completion.WaitAsync(cancellationToken)
            : completion;
    }

    private async Task<LlmFitProcessObservation> CompleteCoreAsync()
    {
        try
        {
            await _observationEnded.Task.WaitAsync(_residualObservationWindow)
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            throw new InvalidOperationException("Socket observation did not stop within its deadline.");
        }

        bool residualProcess = await ObserveResidualProcessesAsync().ConfigureAwait(false);
        int[] ports = _listeningPorts.Keys.Order().ToArray();
        return new LlmFitProcessObservation(
            Volatile.Read(ref _candidateSocketObserved) != 0,
            Volatile.Read(ref _dashboardPortObserved) != 0,
            Array.AsReadOnly(ports),
            residualProcess);
    }

    private async Task<bool> ObserveResidualProcessesAsync()
    {
        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            Dictionary<int, CandidateProcessIdentity> currentProcesses =
                CaptureCandidateProcessSnapshot();
            bool hasNewCandidateProcess = currentProcesses.Values.Any(IsNewCandidateProcess);
            if (!hasNewCandidateProcess)
            {
                return false;
            }

            TimeSpan remaining = _residualObservationWindow - elapsed.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                return true;
            }

            await Task.Delay(
                    remaining < _pollInterval ? remaining : _pollInterval,
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    private HashSet<int> GetStableAttributedProcessIds(
        Dictionary<int, CandidateProcessIdentity> processesBeforeSample,
        Dictionary<int, CandidateProcessIdentity> processesAfterSample)
    {
        var attributed = new HashSet<int>();
        foreach (CandidateProcessIdentity identity in processesBeforeSample.Values)
        {
            if ((IsNewCandidateProcess(identity) ||
                    _rootProcess is not null && identity == _rootProcess) &&
                processesAfterSample.TryGetValue(
                    identity.ProcessId,
                    out CandidateProcessIdentity identityAfterSample) &&
                identityAfterSample == identity)
            {
                attributed.Add(identity.ProcessId);
            }
        }

        return attributed;
    }

    private bool IsNewCandidateProcess(CandidateProcessIdentity identity)
    {
        return !_prelaunchProcesses.TryGetValue(
                identity.ProcessId,
                out CandidateProcessIdentity previous) ||
            previous != identity;
    }

    private void RecordCandidateRows(string output, HashSet<int> attributedProcessIds)
    {
        ArgumentNullException.ThrowIfNull(output);
        foreach (string line in output.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] parts = line.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 5 ||
                !string.Equals(parts[0], "TCP", StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(
                    parts[4],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int processId) ||
                processId <= 0 ||
                !attributedProcessIds.Contains(processId))
            {
                continue;
            }

            string state = parts[3].ToUpperInvariant();
            if (state is not ("LISTENING" or "ESTABLISHED"))
            {
                continue;
            }

            Interlocked.Exchange(ref _candidateSocketObserved, 1);
            bool localPortParsed = TryParseEndpointPort(parts[1], out int localPort);
            if (state == "LISTENING" && localPortParsed)
            {
                _listeningPorts.TryAdd(localPort, 0);
                if (localPort == LlmFitDashboardPort)
                {
                    Interlocked.Exchange(ref _dashboardPortObserved, 1);
                }
            }
        }
    }

    private static bool TryParseEndpointPort(string endpoint, out int port)
    {
        port = 0;
        int separator = endpoint.LastIndexOf(':');
        return separator >= 0 &&
            separator < endpoint.Length - 1 &&
            int.TryParse(
                endpoint.AsSpan(separator + 1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out port) &&
            port is >= 1 and <= 65_535;
    }

    private CandidateProcessIdentity? CaptureVerifiedRootIdentity(int processId)
    {
        CandidateProcessIdentity? identity = _captureRootIdentity(processId);
        if (identity is { } captured &&
            (captured.ProcessId != processId || captured.StartTimeUtcTicks <= 0))
        {
            throw new InvalidOperationException(
                "The observed root process identity could not be verified.");
        }

        return identity;
    }

    private CandidateProcessIdentity? CaptureRootProcessIdentity(int processId)
    {
        Process process;
        try
        {
            process = Process.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            return null;
        }

        using (process)
        {
            string processName;
            long startTimeUtcTicks;
            try
            {
                process.Refresh();
                processName = process.ProcessName;
                startTimeUtcTicks = process.StartTime.ToUniversalTime().Ticks;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            catch (Exception exception) when (
                exception is Win32Exception or NotSupportedException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException(
                    "The observed root process identity could not be verified.");
            }

            if (!string.Equals(
                    processName,
                    _candidateProcessName,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The observed root process does not match the candidate image.");
            }

            return new CandidateProcessIdentity(process.Id, startTimeUtcTicks);
        }
    }

    private Dictionary<int, CandidateProcessIdentity> CaptureCandidateProcessSnapshot()
    {
        IReadOnlyList<CandidateProcessIdentity> snapshot =
            _snapshotCandidateProcesses() ??
            throw new InvalidOperationException("Candidate process identities could not be enumerated.");
        var identities = new Dictionary<int, CandidateProcessIdentity>(snapshot.Count);
        foreach (CandidateProcessIdentity identity in snapshot)
        {
            if (identity.ProcessId <= 0 ||
                identity.StartTimeUtcTicks <= 0 ||
                !identities.TryAdd(identity.ProcessId, identity))
            {
                throw new InvalidOperationException(
                    "A candidate process identity could not be verified.");
            }
        }

        return identities;
    }

    private List<CandidateProcessIdentity> SnapshotCandidateProcessIdentities()
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName(_candidateProcessName);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or NotSupportedException or Win32Exception)
        {
            throw new InvalidOperationException("Candidate process identities could not be enumerated.");
        }

        var identities = new List<CandidateProcessIdentity>(processes.Length);
        try
        {
            foreach (Process process in processes)
            {
                try
                {
                    process.Refresh();
                    if (!string.Equals(
                            process.ProcessName,
                            _candidateProcessName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var identity = new CandidateProcessIdentity(
                        process.Id,
                        process.StartTime.ToUniversalTime().Ticks);
                    identities.Add(identity);
                }
                catch (InvalidOperationException)
                {
                    // The enumerated process exited before its identity could be captured.
                }
                catch (Exception exception) when (
                    exception is Win32Exception or NotSupportedException or UnauthorizedAccessException)
                {
                    throw new InvalidOperationException("A candidate process identity could not be verified.");
                }
            }
        }
        finally
        {
            foreach (Process process in processes)
            {
                process.Dispose();
            }
        }

        return identities;
    }

    private static string ValidateCandidateImageName(string candidateImageName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateImageName);
        if (!string.Equals(
                candidateImageName,
                Path.GetFileName(candidateImageName),
                StringComparison.Ordinal) ||
            Path.IsPathRooted(candidateImageName) ||
            candidateImageName.Contains(':', StringComparison.Ordinal) ||
            candidateImageName.Contains('/', StringComparison.Ordinal) ||
            candidateImageName.Contains('\\', StringComparison.Ordinal) ||
            candidateImageName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            !string.Equals(
                Path.GetExtension(candidateImageName),
                ".exe",
                StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(candidateImageName)))
        {
            throw new ArgumentException(
                "The candidate image name must be one executable base file name.",
                nameof(candidateImageName));
        }

        return candidateImageName;
    }

    private static async Task<string> CaptureNetstatAsync(CancellationToken cancellationToken)
    {
        return await CaptureNetstatCoreAsync(
                static startInfo => new SystemNetstatProcess(startInfo),
                HelperExecutionTimeout,
                HelperCleanupDeadline,
                MaximumNetstatBytesPerStream,
                cancellationToken)
            .ConfigureAwait(false);
    }

    internal static Task<string> CaptureNetstatForTestsAsync(
        Func<ProcessStartInfo, INetstatProcess> processFactory,
        TimeSpan executionTimeout,
        TimeSpan cleanupDeadline,
        int maximumBytesPerStream,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processFactory);
        if (executionTimeout <= TimeSpan.Zero || executionTimeout > HelperExecutionTimeout)
        {
            throw new ArgumentOutOfRangeException(nameof(executionTimeout));
        }

        if (cleanupDeadline <= TimeSpan.Zero || cleanupDeadline > HelperCleanupDeadline)
        {
            throw new ArgumentOutOfRangeException(nameof(cleanupDeadline));
        }

        if (maximumBytesPerStream <= 0 ||
            maximumBytesPerStream > MaximumNetstatBytesPerStream)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBytesPerStream));
        }

        return CaptureNetstatCoreAsync(
            processFactory,
            executionTimeout,
            cleanupDeadline,
            maximumBytesPerStream,
            cancellationToken);
    }

    private static async Task<string> CaptureNetstatCoreAsync(
        Func<ProcessStartInfo, INetstatProcess> processFactory,
        TimeSpan executionTimeout,
        TimeSpan cleanupDeadline,
        int maximumBytesPerStream,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ProcessStartInfo startInfo = CreateNetstatStartInfo();
        INetstatProcess process;
        try
        {
            process = processFactory(startInfo) ??
                throw new InvalidOperationException();
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            throw new InvalidOperationException(
                "The trusted TCP observation helper could not be started.");
        }

        try
        {
            bool started;
            try
            {
                started = process.Start();
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or Win32Exception or NotSupportedException)
            {
                throw new InvalidOperationException(
                    "The trusted TCP observation helper could not be started.");
            }

            if (!started)
            {
                throw new InvalidOperationException(
                    "The trusted TCP observation helper could not be started.");
            }

            Task<BoundedTextCapture>? standardOutput = null;
            Task<BoundedTextCapture>? standardError = null;
            Task? exitTask = null;
            CancellationTokenSource? timeoutLifetime = null;
            CancellationTokenRegistration cancellationRegistration = default;
            bool cancellationRegistrationCreated = false;
            HelperTerminal terminal = HelperTerminal.Failed;
            bool cleanupComplete = false;

            try
            {
                standardOutput = BoundedTextReader.ReadAsync(
                    process.StandardOutput,
                    maximumBytesPerStream);
                standardError = BoundedTextReader.ReadAsync(
                    process.StandardError,
                    maximumBytesPerStream);
                exitTask = process.WaitForExitAsync();
                timeoutLifetime = new CancellationTokenSource();
                Task timeoutTask = Task.Delay(executionTimeout, timeoutLifetime.Token);
                var cancellationSignal = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                cancellationRegistration = cancellationToken.Register(
                    static state => ((TaskCompletionSource)state!).TrySetResult(),
                    cancellationSignal);
                cancellationRegistrationCreated = true;

                terminal = await WaitForHelperTerminalAsync(
                        exitTask,
                        standardOutput,
                        standardError,
                        timeoutTask,
                        cancellationSignal.Task)
                    .ConfigureAwait(false);
            }
            catch
            {
                // Every post-start setup and observation failure is converted to
                // a fixed diagnostic after the owned helper has been cleaned up.
                terminal = HelperTerminal.Failed;
            }
            finally
            {
                if (terminal != HelperTerminal.Exited || !HasExited(process))
                {
                    TryKillProcessTree(process);
                }

                if (timeoutLifetime is not null)
                {
                    TryCancel(timeoutLifetime);
                }

                if (cancellationRegistrationCreated)
                {
                    try
                    {
                        cancellationRegistration.Dispose();
                    }
                    catch
                    {
                        terminal = HelperTerminal.Failed;
                    }
                }

                try
                {
                    cleanupComplete = await AwaitHelperCleanupAsync(
                            process,
                            exitTask,
                            standardOutput,
                            standardError,
                            cleanupDeadline)
                        .ConfigureAwait(false);
                }
                catch
                {
                    cleanupComplete = false;
                }

                TryDisposeCancellationSource(timeoutLifetime);
            }

            if (!cleanupComplete)
            {
                throw new InvalidOperationException(
                    "The TCP observation helper could not be cleaned up.");
            }

            if (terminal == HelperTerminal.Cancelled)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (terminal == HelperTerminal.TimedOut)
            {
                throw new InvalidOperationException(
                    "The TCP observation helper exceeded its deadline.");
            }

            int? exitCode = TryGetExitCode(process);
            if (terminal != HelperTerminal.Exited ||
                standardOutput is null ||
                standardError is null ||
                !standardOutput.IsCompletedSuccessfully ||
                !standardError.IsCompletedSuccessfully ||
                standardOutput.Result.Truncated ||
                standardError.Result.Truncated ||
                exitCode != 0)
            {
                throw new InvalidOperationException("The TCP observation helper failed.");
            }

            return standardOutput.Result.Text;
        }
        finally
        {
            TryDisposeProcess(process);
        }
    }

    private static ProcessStartInfo CreateNetstatStartInfo()
    {
        (string HelperPath, string SystemDirectory) helper = ResolveNetstatHelper();
        var startInfo = new ProcessStartInfo
        {
            FileName = helper.HelperPath,
            WorkingDirectory = helper.SystemDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("-a");
        startInfo.ArgumentList.Add("-n");
        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add("-p");
        startInfo.ArgumentList.Add("tcp");
        return startInfo;
    }

    private static async Task<HelperTerminal> WaitForHelperTerminalAsync(
        Task exitTask,
        Task standardOutput,
        Task standardError,
        Task timeoutTask,
        Task cancellationTask)
    {
        while (true)
        {
            if (cancellationTask.IsCompleted)
            {
                return HelperTerminal.Cancelled;
            }

            if (standardOutput.IsCompleted && !standardOutput.IsCompletedSuccessfully ||
                standardError.IsCompleted && !standardError.IsCompletedSuccessfully)
            {
                return HelperTerminal.Failed;
            }

            if (exitTask.IsCompleted)
            {
                return exitTask.IsCompletedSuccessfully
                    ? HelperTerminal.Exited
                    : HelperTerminal.Failed;
            }

            if (timeoutTask.IsCompleted)
            {
                return timeoutTask.IsCompletedSuccessfully
                    ? HelperTerminal.TimedOut
                    : HelperTerminal.Failed;
            }

            var pending = new List<Task> { exitTask, timeoutTask, cancellationTask };
            if (!standardOutput.IsCompleted)
            {
                pending.Add(standardOutput);
            }

            if (!standardError.IsCompleted)
            {
                pending.Add(standardError);
            }

            _ = await Task.WhenAny(pending).ConfigureAwait(false);
        }
    }

    private static async Task<bool> AwaitHelperCleanupAsync(
        INetstatProcess process,
        Task? exitTask,
        Task? standardOutput,
        Task? standardError,
        TimeSpan cleanupDeadline)
    {
        Task freshExitTask;
        try
        {
            freshExitTask = process.WaitForExitAsync();
        }
        catch
        {
            return false;
        }

        var cleanupTasks = new List<Task> { freshExitTask };
        if (exitTask is not null && !ReferenceEquals(exitTask, freshExitTask))
        {
            cleanupTasks.Add(exitTask);
        }

        if (standardOutput is not null)
        {
            cleanupTasks.Add(standardOutput);
        }

        if (standardError is not null)
        {
            cleanupTasks.Add(standardError);
        }

        Task cleanup = Task.WhenAll(cleanupTasks);
        ObserveFault(cleanup);
        using var deadlineLifetime = new CancellationTokenSource();
        Task deadline = Task.Delay(cleanupDeadline, deadlineLifetime.Token);
        Task completed = await Task.WhenAny(cleanup, deadline).ConfigureAwait(false);
        if (!ReferenceEquals(completed, cleanup))
        {
            return false;
        }

        TryCancel(deadlineLifetime);

        try
        {
            await cleanup.ConfigureAwait(false);
        }
        catch
        {
            // Completion is distinguished from success by the caller.
        }

        return freshExitTask.IsCompletedSuccessfully &&
            (exitTask is null || exitTask.IsCompleted) &&
            (standardOutput is null || standardOutput.IsCompleted) &&
            (standardError is null || standardError.IsCompleted) &&
            HasExited(process);
    }

    private static (string HelperPath, string SystemDirectory) ResolveNetstatHelper()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Candidate-owned TCP observation requires Windows netstat.");
        }

        try
        {
            if (string.IsNullOrWhiteSpace(Environment.SystemDirectory))
            {
                throw new InvalidDataException();
            }

            string systemDirectory = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(Environment.SystemDirectory));
            string helperPath = Path.GetFullPath(Path.Combine(systemDirectory, "netstat.exe"));
            if (!string.Equals(
                    Path.GetDirectoryName(helperPath),
                    systemDirectory,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    Path.GetFileName(helperPath),
                    "netstat.exe",
                    StringComparison.OrdinalIgnoreCase) ||
                !Directory.Exists(systemDirectory) ||
                (File.GetAttributes(systemDirectory) &
                    (FileAttributes.Directory | FileAttributes.ReparsePoint)) != FileAttributes.Directory ||
                !File.Exists(helperPath) ||
                (File.GetAttributes(helperPath) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException();
            }

            return (helperPath, systemDirectory);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidDataException or IOException or
            NotSupportedException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException("The trusted TCP observation helper is unavailable.");
        }
    }

    private static void TryKillProcessTree(INetstatProcess process)
    {
        try
        {
            process.KillEntireProcessTree();
        }
        catch
        {
            // Bounded cleanup below is the authoritative termination check.
        }
    }

    private static bool HasExited(INetstatProcess process)
    {
        try
        {
            return process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static int? TryGetExitCode(INetstatProcess process)
    {
        try
        {
            return process.ExitCode;
        }
        catch
        {
            return null;
        }
    }

    private static void TryDisposeProcess(INetstatProcess process)
    {
        try
        {
            process.Dispose();
        }
        catch
        {
            // The helper lifetime was already decided by bounded cleanup.
        }
    }

    private static void TryDisposeCancellationSource(CancellationTokenSource? source)
    {
        if (source is null)
        {
            return;
        }

        try
        {
            source.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // A private source may already have been released on an exceptional path.
        }
    }

    private static void TryCancel(CancellationTokenSource cancellation)
    {
        try
        {
            cancellation.Cancel();
        }
        catch (AggregateException)
        {
            // This private source has no user callbacks.
        }
    }

    private static void ObserveFault(Task task)
    {
        if (task.IsFaulted)
        {
            _ = task.Exception;
        }
        else if (!task.IsCompleted)
        {
            _ = task.ContinueWith(
                static completed => _ = completed.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private enum HelperTerminal
    {
        Exited,
        Cancelled,
        TimedOut,
        Failed,
    }

    private sealed class SystemNetstatProcess : INetstatProcess
    {
        private readonly Process _process;

        internal SystemNetstatProcess(ProcessStartInfo startInfo)
        {
            _process = new Process { StartInfo = startInfo };
        }

        public Stream StandardOutput => _process.StandardOutput.BaseStream;

        public Stream StandardError => _process.StandardError.BaseStream;

        public int ExitCode => _process.ExitCode;

        public bool HasExited => _process.HasExited;

        public bool Start()
        {
            return _process.Start();
        }

        public Task WaitForExitAsync()
        {
            return _process.WaitForExitAsync(CancellationToken.None);
        }

        public void KillEntireProcessTree()
        {
            _process.Kill(entireProcessTree: true);
        }

        public void Dispose()
        {
            _process.Dispose();
        }
    }
}
