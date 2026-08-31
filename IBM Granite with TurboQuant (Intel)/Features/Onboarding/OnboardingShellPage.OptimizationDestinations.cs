using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.ModelOptimization.Execution.OpenVino;
using GraniteEdgeAI.Features.ModelOptimization.Journey;
using GraniteEdgeAI.Features.ModelOptimization.Storage;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.Features.Onboarding;

public sealed partial class OnboardingShellPage
{
    private readonly object _optimizationDestinationGate = new();
    private readonly HashSet<Task> _optimizationDestinationOperations = [];
    private OptimizationDestinationFacade? _optimizationDestinations;
    private CancellationTokenSource? _optimizationLifecycleCancellation;
    private OptimizationChatTarget? _activeOptimizationChatTarget;

    private OptimizationDestinationFacade CreateOptimizationDestinationFacade(
        OptimizationExecutionPlan plan,
        IOptimizationExecutor executor,
        OptimizationOutputRegistry outputs,
        string appRoot)
    {
        var gguf = new GgufOptimizationDestinationRoute(
            plan, outputs, _modelSourceCustodyRegistry);
        IOptimizationDestinationRoute openVino;
        if (executor is OpenVinoOptimizationExecutor openVinoExecutor)
        {
            openVino = new OpenVinoOptimizationDestinationRoute(openVinoExecutor);
        }
        else if (_activeOpenVinoAuthority is { } authority
                 && _activeOpenVinoOptimizationService is { } service)
        {
            var fallbackOpenVinoExecutor = new OpenVinoOptimizationExecutor(
                _modelSourceCustodyRegistry,
                authority,
                service,
                Path.Combine(appRoot, "OpenVinoOutputs"));
            openVino = new OpenVinoOptimizationDestinationRoute(
                fallbackOpenVinoExecutor);
        }
        else
        {
            openVino = new UnavailableOptimizationDestinationRoute(
                OptimizationRoute.OpenVino);
        }

        return new OptimizationDestinationFacade(gguf, openVino);
    }

    private void BeginOptimizationDestinationLifecycle(
        OptimizationDestinationFacade destinations)
    {
        ArgumentNullException.ThrowIfNull(destinations);
        if (_optimizationDestinations is not null
            || _optimizationLifecycleCancellation is not null)
        {
            throw new InvalidOperationException(
                "The previous optimization destination lifecycle is still active.");
        }

        _optimizationDestinations = destinations;
        _optimizationLifecycleCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeCancellation.Token);
    }

    private CancellationToken OptimizationDestinationToken =>
        _optimizationLifecycleCancellation?.Token
        ?? _lifetimeCancellation.Token;

    private async Task<OptimizationChatTarget?>
        CreateOptimizationChatTargetAsync(
            OptimizationExecutionResult result,
            CancellationToken cancellationToken)
    {
        OptimizationDestinationFacade? destinations = _optimizationDestinations;
        if (destinations is null)
        {
            return null;
        }

        OptimizationChatTarget? target = await TrackOptimizationDestinationOperationAsync(
            destinations.CreateChatTargetAsync(result, cancellationToken));
        if (target is null)
        {
            return null;
        }

        OptimizationChatTarget? previous = Interlocked.Exchange(
            ref _activeOptimizationChatTarget, target);
        previous?.Dispose();
        return target;
    }

    private Task<OptimizationDestinationExportResult> ExportOptimizedModelAsync(
        OptimizationExecutionResult result,
        string destination,
        ulong maximumBytes,
        CancellationToken cancellationToken)
    {
        OptimizationDestinationFacade? destinations = _optimizationDestinations;
        return destinations is null
            ? Task.FromResult(OptimizationDestinationExportResult.For(
                result,
                OptimizationDestinationExportDisposition.ResultRejected))
            : TrackOptimizationDestinationOperationAsync(
                destinations.ExportPersistentAsync(
                    result,
                    destination,
                    maximumBytes,
                    cancellationToken));
    }

    private async Task<T> TrackOptimizationDestinationOperationAsync<T>(
        Task<T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        lock (_optimizationDestinationGate)
        {
            _optimizationDestinationOperations.Add(operation);
        }

        try
        {
            return await operation;
        }
        finally
        {
            lock (_optimizationDestinationGate)
            {
                _optimizationDestinationOperations.Remove(operation);
            }
        }
    }

    private async Task RetireOptimizationDestinationLifecycleAsync()
    {
        CancellationTokenSource? cancellation = Interlocked.Exchange(
            ref _optimizationLifecycleCancellation, null);
        if (cancellation is not null)
        {
            await cancellation.CancelAsync();
        }

        while (true)
        {
            Task[] operations;
            lock (_optimizationDestinationGate)
            {
                operations = [.. _optimizationDestinationOperations];
            }
            if (operations.Length == 0)
            {
                break;
            }

            try
            {
                await Task.WhenAll(operations);
            }
            catch (OperationCanceledException) when (
                cancellation?.IsCancellationRequested == true)
            {
                // Lifecycle retirement owns this cancellation.
            }
            catch (Exception) when (cancellation is not null)
            {
                // The initiating UI operation observes its own typed failure;
                // retirement only joins it before releasing journey custody.
            }
        }

        _optimizationDestinations = null;
        cancellation?.Dispose();
    }

    private void RetireActiveOptimizationChatTarget() =>
        Interlocked.Exchange(ref _activeOptimizationChatTarget, null)?.Dispose();

    private sealed class UnavailableOptimizationDestinationRoute(
        OptimizationRoute route) : IOptimizationDestinationRoute
    {
        public OptimizationRoute Route { get; } = route;

        public Task<OptimizationChatTarget?> CreateChatTargetAsync(
            OptimizationExecutionResult result,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<OptimizationChatTarget?>(null);
        }

        public Task<OptimizationDestinationExportResult> ExportPersistentAsync(
            OptimizationExecutionResult result,
            string destination,
            ulong maximumBytes,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(OptimizationDestinationExportResult.For(
                result,
                OptimizationDestinationExportDisposition.ResultRejected));
        }
    }
}
