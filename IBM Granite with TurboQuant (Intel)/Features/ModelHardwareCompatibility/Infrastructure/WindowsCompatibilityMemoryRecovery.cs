using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;

/// <summary>
/// Releases only memory owned by this application and optionally opens the
/// operating system's own management surface. It never discovers or controls
/// another application.
/// </summary>
internal sealed class WindowsCompatibilityMemoryRecovery : ICompatibilityMemoryRecovery
{
    private readonly IReadOnlyList<Func<CancellationToken, Task>> _cacheReleaseCallbacks;
    private readonly ITaskManagerProcessStarter _taskManagerProcessStarter;
    private readonly Action _collect;

    /// <summary>
    /// Captures a closed callback list for this adapter's lifetime. Composition
    /// code may register only callbacks for caches it owns; UI text and user
    /// input can never become a callback or command argument.
    /// </summary>
    internal WindowsCompatibilityMemoryRecovery(
        IEnumerable<Func<CancellationToken, Task>> cacheReleaseCallbacks,
        ITaskManagerProcessStarter? taskManagerProcessStarter = null,
        Action? collect = null)
    {
        ArgumentNullException.ThrowIfNull(cacheReleaseCallbacks);

        Func<CancellationToken, Task>[] callbacks = cacheReleaseCallbacks.ToArray();
        if (callbacks.Any(callback => callback is null))
        {
            throw new ArgumentException(
                "A cache-release callback cannot be null.",
                nameof(cacheReleaseCallbacks));
        }

        _cacheReleaseCallbacks = Array.AsReadOnly(callbacks);
        _taskManagerProcessStarter = taskManagerProcessStarter
            ?? new WindowsTaskManagerProcessStarter();
        _collect = collect ?? CollectAfterOwnedRelease;
    }

    public async Task ReleaseApplicationMemoryAsync(CancellationToken cancellationToken)
    {
        // Registration order is execution order. Failures and cancellation stop
        // the sequence so later callbacks cannot run against a partially reset
        // application state, and collection never follows a partial release.
        foreach (Func<CancellationToken, Task> release in _cacheReleaseCallbacks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await release(cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (_cacheReleaseCallbacks.Count > 0)
        {
            // Best effort only. No amount is measured, promised, or surfaced.
            _collect();
        }
    }

    public Task OpenTaskManagerAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _taskManagerProcessStarter.Start(TaskManagerLaunchRequest.Fixed);
        return Task.CompletedTask;
    }

    private static void CollectAfterOwnedRelease() =>
        GC.Collect(
            GC.MaxGeneration,
            GCCollectionMode.Optimized,
            blocking: false,
            compacting: false);
}

/// <summary>
/// The one immutable launch descriptor admitted by the compatibility feature.
/// Its private constructor prevents callers from supplying an executable,
/// arguments, verb, or window policy.
/// </summary>
internal sealed class TaskManagerLaunchRequest
{
    private static readonly string SystemTaskManagerPath = Path.Combine(
        Environment.SystemDirectory,
        "Taskmgr.exe");

    private TaskManagerLaunchRequest()
    {
    }

    internal static TaskManagerLaunchRequest Fixed { get; } = new();

    internal string FileName => SystemTaskManagerPath;

    internal string Arguments => string.Empty;

    internal string Verb => string.Empty;

    internal bool UseShellExecute => true;

    internal ProcessWindowStyle WindowStyle => ProcessWindowStyle.Normal;

    /// <summary>
    /// Produces an isolated descriptor from the closed request. Mutating a
    /// returned instance cannot alter the next launch or the fixed request.
    /// </summary>
    internal ProcessStartInfo CreateProcessStartInfo() => new()
    {
        FileName = FileName,
        Arguments = Arguments,
        Verb = Verb,
        UseShellExecute = UseShellExecute,
        WindowStyle = WindowStyle
    };
}

/// <summary>A closed process boundary that can launch only the fixed request.</summary>
internal interface ITaskManagerProcessStarter
{
    void Start(TaskManagerLaunchRequest request);
}

internal sealed class WindowsTaskManagerProcessStarter : ITaskManagerProcessStarter
{
    public void Start(TaskManagerLaunchRequest request)
    {
        if (!ReferenceEquals(request, TaskManagerLaunchRequest.Fixed))
        {
            throw new ArgumentException("Only the fixed Task Manager request is accepted.",
                nameof(request));
        }

        using Process process = Process.Start(request.CreateProcessStartInfo())
            ?? throw new InvalidOperationException("Task Manager launch was not accepted.");
    }
}
