using System;
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
    private OptimizationDestinationLifecycle? _optimizationDestinationLifecycle;
    private OptimizationDestinationLifecycle? _optimizedChatDestinationLifecycle;

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
        if (_optimizationDestinationLifecycle is not null)
        {
            throw new InvalidOperationException(
                "The previous optimization destination lifecycle is still active.");
        }

        _optimizationDestinationLifecycle = new OptimizationDestinationLifecycle(
            destinations, _lifetimeCancellation.Token);
    }

    private Task<OptimizationChatTargetUse?> CreateOptimizationChatTargetAsync(
        OptimizationExecutionResult result)
    {
        OptimizationDestinationLifecycle? lifecycle =
            _optimizationDestinationLifecycle;
        return lifecycle is null
            ? Task.FromResult<OptimizationChatTargetUse?>(null)
            : lifecycle.CreateChatTargetUseAsync(result);
    }

    private Task<OptimizationDestinationExportResult> ExportOptimizedModelAsync(
        OptimizationExecutionResult result,
        string destination,
        ulong maximumBytes)
    {
        OptimizationDestinationLifecycle? lifecycle =
            _optimizationDestinationLifecycle;
        return lifecycle is null
            ? Task.FromResult(OptimizationDestinationExportResult.For(
                result,
                OptimizationDestinationExportDisposition.ResultRejected))
            : lifecycle.ExportPersistentAsync(result, destination, maximumBytes);
    }

    private async Task RetireOptimizationDestinationLifecycleAsync(
        bool preserveChatTarget)
    {
        OptimizationDestinationLifecycle? lifecycle = Interlocked.Exchange(
            ref _optimizationDestinationLifecycle, null);
        if (lifecycle is null)
        {
            return;
        }

        try
        {
            await lifecycle.RetireAsync();
        }
        finally
        {
            if (preserveChatTarget)
            {
                OptimizationDestinationLifecycle? previous =
                    Interlocked.CompareExchange(
                        ref _optimizedChatDestinationLifecycle,
                        lifecycle,
                        null);
                if (previous is not null)
                {
                    lifecycle.RetireChatTarget();
                    throw new InvalidOperationException(
                        "An optimized chat destination already owns custody.");
                }
            }
            else
            {
                lifecycle.RetireChatTarget();
            }
        }
    }

    private void RetireActiveOptimizationChatTarget()
    {
        _optimizationDestinationLifecycle?.RetireChatTarget();
        Interlocked.Exchange(
            ref _optimizedChatDestinationLifecycle, null)?.RetireChatTarget();
    }

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
