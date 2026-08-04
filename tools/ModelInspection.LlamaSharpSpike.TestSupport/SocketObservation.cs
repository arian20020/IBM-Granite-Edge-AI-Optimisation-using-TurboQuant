using System.Collections.Concurrent;
using System.Diagnostics;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;

/// <summary>
/// Records one TCP listener or established connection owned by the observed
/// feasibility process.
/// </summary>
public sealed record TcpSocketObservation(
    string Protocol,
    string LocalEndpoint,
    string RemoteEndpoint,
    string State,
    int ProcessId);

/// <summary>
/// Polls Windows netstat while a child process is alive and records only TCP
/// LISTENING or ESTABLISHED entries owned by that exact PID.
/// </summary>
public sealed class SocketObservation
{
    private readonly ConcurrentDictionary<string, TcpSocketObservation>
        _observations = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _pollInterval;
    private int _sampleCount;

    public SocketObservation()
        : this(TimeSpan.FromMilliseconds(100))
    {
    }

    public SocketObservation(TimeSpan pollInterval)
    {
        if (pollInterval <= TimeSpan.Zero ||
            pollInterval == Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pollInterval),
                "Socket observation interval must be positive and finite.");
        }

        _pollInterval = pollInterval;
    }

    /// <summary>
    /// Gets the number of completed netstat samples.
    /// </summary>
    public int SampleCount => Volatile.Read(ref _sampleCount);

    /// <summary>
    /// Observes the supplied PID until the runner cancels the observer.
    /// </summary>
    public async Task ObserveAsync(
        int processId,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Process-owned TCP observation currently requires Windows netstat.");
        }

        if (processId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId));
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string output = await CaptureNetstatAsync(cancellationToken);
            Interlocked.Increment(ref _sampleCount);

            foreach (TcpSocketObservation entry in ParseNetstatOutput(
                output,
                processId))
            {
                string key = string.Join(
                    "|",
                    entry.Protocol,
                    entry.LocalEndpoint,
                    entry.RemoteEndpoint,
                    entry.State,
                    entry.ProcessId);
                _observations.TryAdd(key, entry);
            }

            await Task.Delay(_pollInterval, cancellationToken);
        }
    }

    /// <summary>
    /// Returns an independent, stable snapshot of all observed endpoints.
    /// </summary>
    public IReadOnlyList<TcpSocketObservation> GetSnapshot()
    {
        return _observations.Values
            .OrderBy(entry => entry.State, StringComparer.Ordinal)
            .ThenBy(entry => entry.LocalEndpoint, StringComparer.Ordinal)
            .ThenBy(entry => entry.RemoteEndpoint, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Parses ordinary <c>netstat -ano -p tcp</c> output for one process.
    /// </summary>
    public static IReadOnlyList<TcpSocketObservation> ParseNetstatOutput(
        string output,
        int processId)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (processId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId));
        }

        var results = new List<TcpSocketObservation>();

        foreach (string line in output.Split(
            new[] { '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = line.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 5 ||
                !string.Equals(
                    parts[0],
                    "TCP",
                    StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(parts[^1], out int owningProcessId) ||
                owningProcessId != processId)
            {
                continue;
            }

            string state = parts[^2].ToUpperInvariant();
            if (state is not ("LISTENING" or "ESTABLISHED"))
            {
                continue;
            }

            results.Add(new TcpSocketObservation(
                Protocol: "TCP",
                LocalEndpoint: parts[1],
                RemoteEndpoint: parts[2],
                State: state,
                ProcessId: owningProcessId));
        }

        return results;
    }

    private static async Task<string> CaptureNetstatAsync(
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "netstat.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-a");
        startInfo.ArgumentList.Add("-n");
        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add("-p");
        startInfo.ArgumentList.Add("tcp");

        using var process = new Process
        {
            StartInfo = startInfo
        };

        if (!process.Start())
        {
            throw new InvalidOperationException(
                "netstat.exe could not be started.");
        }

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }

            throw;
        }

        string output = await outputTask;
        string error = await errorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"netstat.exe exited with code {process.ExitCode}: {error.Trim()}");
        }

        return output;
    }
}
