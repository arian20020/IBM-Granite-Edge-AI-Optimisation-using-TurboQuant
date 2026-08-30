using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Contracts;
using GraniteEdgeAI.Features.ApplicationFaults;
using GraniteEdgeAI.Features.GgufRuntime;
using GraniteEdgeAI.Features.ModelInspection.SourceCustody;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;

internal interface ICurrentModelChatRouteLauncher
{
    OptimizationRoute Route { get; }

    Task<CurrentModelChatLaunchResult> LaunchAsync(
        CurrentModelLaunchContext context,
        ModelSourceLease sourceLease,
        CancellationToken cancellationToken);
}

internal sealed class CurrentModelChatLaunchRegistry
    : ICurrentModelChatLaunchAuthority, IDisposable
{
    private readonly object _gate = new();
    private readonly ModelSourceCustodyRegistry _sourceCustody;
    private readonly Dictionary<string, CurrentModelLaunchContext> _contexts =
        new(StringComparer.Ordinal);
    private readonly Dictionary<OptimizationRoute, ICurrentModelChatRouteLauncher>
        _launchers = [];
    private readonly IApplicationFaultReporter _faultReporter;
    private bool _disposed;
    private int _unexpectedGgufFaultReported;

    internal CurrentModelChatLaunchRegistry(
        ModelSourceCustodyRegistry sourceCustody,
        IApplicationFaultReporter? faultReporter = null)
    {
        _sourceCustody = sourceCustody
            ?? throw new ArgumentNullException(nameof(sourceCustody));
        _faultReporter = faultReporter ?? BoundedApplicationFaultReporter.Shared;
    }

    internal bool RegisterContext(CurrentModelLaunchContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_contexts.TryGetValue(
                    context.Handoff.CompatibilityDecisionId,
                    out CurrentModelLaunchContext? existing))
            {
                return existing == context;
            }
            _contexts.Add(context.Handoff.CompatibilityDecisionId, context);
            return true;
        }
    }

    internal bool RegisterRoute(ICurrentModelChatRouteLauncher launcher)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return Enum.IsDefined(launcher.Route)
                && _launchers.TryAdd(launcher.Route, launcher);
        }
    }

    public async Task<CurrentModelChatLaunchResult> LaunchAsync(
        CurrentModelLaunchHandoff handoff,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handoff);
        CurrentModelLaunchContext? context;
        ICurrentModelChatRouteLauncher? launcher;
        lock (_gate)
        {
            if (_disposed
                || !_contexts.TryGetValue(
                    handoff.CompatibilityDecisionId,
                    out context)
                || context.Handoff != handoff)
            {
                return CurrentModelChatLaunchResult.Failed(
                    CurrentModelChatSupportCode.BindingMismatch);
            }
            if (!_launchers.TryGetValue(handoff.Route, out launcher))
            {
                return CurrentModelChatLaunchResult.Failed(
                    CurrentModelChatSupportCode.RuntimeUnavailable);
            }
        }

        if (!_sourceCustody.TryAcquire(context.SourceKey, out ModelSourceLease? lease))
        {
            return CurrentModelChatLaunchResult.Failed(
                CurrentModelChatSupportCode.SourceUnavailable);
        }

        using (lease!)
        {
            try
            {
                CurrentModelChatLaunchResult? result = await launcher.LaunchAsync(
                    context,
                    lease!,
                    cancellationToken).ConfigureAwait(false);
                return result ?? CurrentModelChatLaunchResult.Failed(
                    CurrentModelChatSupportCode.LaunchFailed);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return CurrentModelChatLaunchResult.Failed(
                    CurrentModelChatSupportCode.CancelledByUser);
            }
            catch (Exception exception)
            {
                if (launcher.Route == OptimizationRoute.Gguf
                    && !ChatDemoController.TryClassifyOperationalFailure(
                        exception,
                        out _)
                    && Interlocked.Exchange(
                        ref _unexpectedGgufFaultReported,
                        1) == 0)
                {
                    _faultReporter.Report(ApplicationFault.FromException(
                        ApplicationFaultCode.GgufChatOperationUnexpected,
                        exception));
                }
                return CurrentModelChatLaunchResult.Failed(
                    CurrentModelChatSupportCode.LaunchFailed);
            }
        }
    }

    internal void Retire(Guid modelInspectionHandoffId)
    {
        lock (_gate)
        {
            foreach ((string decisionId, CurrentModelLaunchContext context) in
                _contexts.ToArray())
            {
                if (context.Handoff.ModelInspectionHandoffId
                    == modelInspectionHandoffId)
                {
                    _contexts.Remove(decisionId);
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _contexts.Clear();
            _launchers.Clear();
        }
    }
}
