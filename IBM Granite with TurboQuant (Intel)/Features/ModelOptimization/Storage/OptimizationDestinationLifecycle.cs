using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.Features.ModelOptimization.Storage;

internal sealed class OptimizationChatHandoffLease : IDisposable
{
    private OptimizationChatHandoffGate? _owner;

    internal OptimizationChatHandoffLease(OptimizationChatHandoffGate owner) =>
        _owner = owner;

    public void Dispose() =>
        Interlocked.Exchange(ref _owner, null)?.Release();
}

internal sealed class OptimizationChatHandoffGate : IDisposable
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _admission = new(1, 1);
    private bool _closing;
    private Task? _closure;

    internal async Task<OptimizationChatHandoffLease?> TryEnterAsync(
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_closing)
            {
                return null;
            }
        }

        await _admission.WaitAsync(cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            if (_closing)
            {
                _admission.Release();
                return null;
            }

            return new OptimizationChatHandoffLease(this);
        }
    }

    internal Task CloseAsync()
    {
        lock (_gate)
        {
            if (_closure is not null)
            {
                return _closure;
            }

            _closing = true;
            _closure = CloseCoreAsync();
            return _closure;
        }
    }

    internal void Release() => _admission.Release();

    public void Dispose() => _admission.Dispose();

    private async Task CloseCoreAsync()
    {
        await _admission.WaitAsync().ConfigureAwait(false);
        _admission.Release();
    }
}

internal sealed class OptimizationChatTargetUse : IDisposable
{
    private OptimizationDestinationLifecycle? _owner;

    internal OptimizationChatTargetUse(
        OptimizationDestinationLifecycle owner,
        OptimizationChatTarget target,
        CancellationToken cancellationToken)
    {
        _owner = owner;
        Target = target;
        CancellationToken = cancellationToken;
    }

    internal OptimizationChatTarget Target { get; }
    internal CancellationToken CancellationToken { get; }

    public void Dispose() =>
        Interlocked.Exchange(ref _owner, null)?.ReleaseChatTargetUse();
}

/// <summary>
/// Owns admission, cancellation, and destination custody for one optimization
/// journey. Retirement closes admission before it cancels or joins work.
/// </summary>
internal sealed class OptimizationDestinationLifecycle
{
    private enum LifecycleState
    {
        Active,
        Retiring,
        Retired,
    }

    private readonly object _gate = new();
    private readonly HashSet<Task> _operations = [];
    private readonly CancellationTokenSource _cancellation;
    private OptimizationDestinationFacade? _facade;
    private LifecycleState _state;
    private bool _chatAdmissionPending;
    private OptimizationChatTarget? _chatTarget;
    private int _chatTargetUses;
    private TaskCompletionSource? _chatTargetUsesCompleted;
    private Task? _retirement;

    internal OptimizationDestinationLifecycle(
        OptimizationDestinationFacade facade,
        CancellationToken lifetimeCancellation)
    {
        ArgumentNullException.ThrowIfNull(facade);
        _facade = facade;
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            lifetimeCancellation);
    }

    internal Task<OptimizationChatTargetUse?> CreateChatTargetUseAsync(
        OptimizationExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Task<OptimizationChatTarget?> operation;
        lock (_gate)
        {
            if (_state != LifecycleState.Active
                || _facade is null
                || _chatAdmissionPending
                || _chatTarget is not null)
            {
                return Task.FromResult<OptimizationChatTargetUse?>(null);
            }

            _chatAdmissionPending = true;
            try
            {
                operation = _facade.CreateChatTargetAsync(
                    result, _cancellation.Token);
                _operations.Add(operation);
            }
            catch
            {
                _chatAdmissionPending = false;
                throw;
            }
        }

        return CompleteChatAdmissionAsync(operation);
    }

    internal Task<OptimizationDestinationExportResult> ExportPersistentAsync(
        OptimizationExecutionResult result,
        string destination,
        ulong maximumBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        Task<OptimizationDestinationExportResult> operation;
        lock (_gate)
        {
            if (_state != LifecycleState.Active || _facade is null)
            {
                return Task.FromResult(OptimizationDestinationExportResult.For(
                    result,
                    OptimizationDestinationExportDisposition.ResultRejected));
            }

            operation = ExportWithLinkedCancellationAsync(
                _facade,
                result,
                destination,
                maximumBytes,
                cancellationToken);
            _operations.Add(operation);
        }

        return CompleteExportAsync(operation);
    }

    internal Task<GgufRuntimeProfileBundleExportResult>
        ExportGgufRuntimeBundleAsync(
            OptimizationExecutionResult result,
            string destination,
            ulong maximumBytes,
            IProgress<GgufRuntimeProfileBundleExportStage> progress,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(progress);
        Task<GgufRuntimeProfileBundleExportResult> operation;
        lock (_gate)
        {
            if (_state != LifecycleState.Active || _facade is null)
            {
                return Task.FromResult(
                    GgufRuntimeProfileBundleExportResult.For(
                        GgufRuntimeProfileBundleExportDisposition.ResultRejected));
            }

            operation = ExportRuntimeBundleWithLinkedCancellationAsync(
                _facade,
                result,
                destination,
                maximumBytes,
                progress,
                cancellationToken);
            _operations.Add(operation);
        }
        return CompleteRuntimeBundleExportAsync(operation);
    }

    private async Task<GgufRuntimeProfileBundleExportResult>
        ExportRuntimeBundleWithLinkedCancellationAsync(
            OptimizationDestinationFacade facade,
            OptimizationExecutionResult result,
            string destination,
            ulong maximumBytes,
            IProgress<GgufRuntimeProfileBundleExportStage> progress,
            CancellationToken cancellationToken)
    {
        // never invoke route or progress callbacks while the lifecycle gate is
        // held by the admission path that creates this operation
        await Task.Yield();
        using CancellationTokenSource linked =
            CancellationTokenSource.CreateLinkedTokenSource(
                _cancellation.Token, cancellationToken);
        return await facade.ExportGgufRuntimeBundleAsync(
            result,
            destination,
            maximumBytes,
            progress,
            linked.Token).ConfigureAwait(false);
    }

    private async Task<OptimizationDestinationExportResult>
        ExportWithLinkedCancellationAsync(
            OptimizationDestinationFacade facade,
            OptimizationExecutionResult result,
            string destination,
            ulong maximumBytes,
            CancellationToken cancellationToken)
    {
        using CancellationTokenSource linked =
            CancellationTokenSource.CreateLinkedTokenSource(
                _cancellation.Token, cancellationToken);
        return await facade.ExportPersistentAsync(
            result,
            destination,
            maximumBytes,
            linked.Token).ConfigureAwait(false);
    }

    internal Task RetireAsync()
    {
        TaskCompletionSource completion;
        lock (_gate)
        {
            if (_retirement is not null)
            {
                return _retirement;
            }

            completion = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _retirement = completion.Task;
            _state = LifecycleState.Retiring;
            _facade = null;
        }

        Exception? cancellationFailure = null;
        try
        {
            // Retirement is an admission-and-cancellation barrier.  Request
            // cancellation synchronously after closing admission so an
            // already-admitted operation cannot complete successfully in the
            // gap between RetireAsync returning and asynchronous cancellation
            // callbacks reaching its linked token
            _cancellation.Cancel();
        }
        catch (Exception exception)
        {
            // Preserve the existing contract: cancellation callback failures
            // are reported by the retirement task, never thrown synchronously
            // from RetireAsync.
            cancellationFailure = exception;
        }

        _ = CompleteRetirementAsync(completion, cancellationFailure);
        return completion.Task;
    }

    internal void RetireChatTarget()
    {
        OptimizationChatTarget? target;
        lock (_gate)
        {
            if (_chatTargetUses != 0)
            {
                throw new InvalidOperationException(
                    "The optimization chat target is still in use.");
            }

            target = _chatTarget;
            _chatTarget = null;
        }

        target?.Dispose();
    }

    internal void ReleaseChatTargetUse()
    {
        TaskCompletionSource? completed = null;
        lock (_gate)
        {
            if (_chatTargetUses <= 0)
            {
                throw new InvalidOperationException(
                    "The optimization chat target use is not active.");
            }

            _chatTargetUses--;
            if (_chatTargetUses == 0)
            {
                completed = _chatTargetUsesCompleted;
                _chatTargetUsesCompleted = null;
            }
        }

        completed?.TrySetResult();
    }

    private async Task<OptimizationChatTargetUse?> CompleteChatAdmissionAsync(
        Task<OptimizationChatTarget?> operation)
    {
        OptimizationChatTarget? target = null;
        try
        {
            target = await operation.ConfigureAwait(false);
            if (target is null)
            {
                return null;
            }

            lock (_gate)
            {
                if (_state == LifecycleState.Active && _chatTarget is null)
                {
                    _chatTarget = target;
                    _chatTargetUses++;
                    _chatTargetUsesCompleted ??= new TaskCompletionSource(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    return new OptimizationChatTargetUse(
                        this, target, _cancellation.Token);
                }
            }

            target.Dispose();
            target = null;
            return null;
        }
        finally
        {
            lock (_gate)
            {
                _chatAdmissionPending = false;
                _operations.Remove(operation);
            }
        }
    }

    private async Task<OptimizationDestinationExportResult> CompleteExportAsync(
        Task<OptimizationDestinationExportResult> operation)
    {
        try
        {
            return await operation.ConfigureAwait(false);
        }
        finally
        {
            lock (_gate)
            {
                _operations.Remove(operation);
            }
        }
    }

    private async Task<GgufRuntimeProfileBundleExportResult>
        CompleteRuntimeBundleExportAsync(
            Task<GgufRuntimeProfileBundleExportResult> operation)
    {
        try
        {
            return await operation.ConfigureAwait(false);
        }
        finally
        {
            lock (_gate)
            {
                _operations.Remove(operation);
            }
        }
    }

    private async Task CompleteRetirementAsync(
        TaskCompletionSource completion,
        Exception? cancellationFailure)
    {
        try
        {
            while (true)
            {
                Task[] operations;
                Task targetUses;
                lock (_gate)
                {
                    operations = [.. _operations];
                    targetUses = _chatTargetUses == 0
                        ? Task.CompletedTask
                        : _chatTargetUsesCompleted!.Task;
                }

                if (operations.Length == 0 && targetUses.IsCompleted)
                {
                    break;
                }

                try
                {
                    await Task.WhenAll(operations).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // the caller that initiated each operation observes its
                    // failure. Retirement only joins admitted work.
                }
                await targetUses.ConfigureAwait(false);
            }

            lock (_gate)
            {
                _state = LifecycleState.Retired;
            }
            _cancellation.Dispose();
            if (cancellationFailure is null)
            {
                completion.TrySetResult();
            }
            else
            {
                completion.TrySetException(cancellationFailure);
            }
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }
}
