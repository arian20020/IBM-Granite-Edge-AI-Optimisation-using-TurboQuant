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
    private readonly Action _startTaskManager;
    private readonly Action _collect;

    /// <summary>
    /// Captures a closed callback list for this adapter's lifetime. Composition
    /// code may register only callbacks for caches it owns; UI text and user
    /// input can never become a callback or command argument.
    /// </summary>
    internal WindowsCompatibilityMemoryRecovery(
        IEnumerable<Func<CancellationToken, Task>> cacheReleaseCallbacks,
        Action? startTaskManager = null,
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
        _startTaskManager = startTaskManager ?? StartTrustedTaskManager;
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

        _startTaskManager();
        return Task.CompletedTask;
    }

    private static ProcessStartInfo CreateTrustedTaskManagerStartInfo() =>
        new()
        {
            FileName = "taskmgr.exe",
            Arguments = string.Empty,
            Verb = string.Empty,
            UseShellExecute = true
        };

    private static void StartTrustedTaskManager() =>
        _ = Process.Start(CreateTrustedTaskManagerStartInfo());

    private static void CollectAfterOwnedRelease() =>
        GC.Collect(
            GC.MaxGeneration,
            GCCollectionMode.Optimized,
            blocking: false,
            compacting: false);
}
