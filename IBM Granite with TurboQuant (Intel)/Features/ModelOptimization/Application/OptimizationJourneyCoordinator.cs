using System;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.Features.ModelOptimization.Journey;

internal sealed class OptimizationJourneyCoordinator : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly OptimizationExecutorRouter _router;
    private readonly IOptimizationRevalidator _revalidator;
    private readonly IOptimizationAttemptContextFactory _contextFactory;
    private readonly TimeProvider _timeProvider;
    private CancellationTokenSource? _attemptCancellation;
    private long _lastGeneration;
    private bool _retired;

    internal OptimizationJourneyCoordinator(
        OptimizationJourneyEntryContext entry,
        OptimizationExecutorRouter router,
        IOptimizationRevalidator revalidator,
        IOptimizationAttemptContextFactory contextFactory,
        TimeProvider? timeProvider = null,
        long initialGeneration = 0)
    {
        if (initialGeneration < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialGeneration));
        }
        State = OptimizationJourneyState.Initial(entry);
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _revalidator = revalidator
            ?? throw new ArgumentNullException(nameof(revalidator));
        _contextFactory = contextFactory
            ?? throw new ArgumentNullException(nameof(contextFactory));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _lastGeneration = initialGeneration;
    }

    internal event EventHandler<OptimizationJourneyState>? StateChanged;

    internal OptimizationJourneyState State { get; private set; }

    internal bool IsRetired
    {
        get { lock (_gate) { return _retired; } }
    }

    internal Task ConfirmAsync()
    {
        long generation;
        CancellationToken token;
        OptimizationJourneyState started;
        lock (_gate)
        {
            if (_retired || State.Kind != OptimizationJourneyKind.Confirmation)
            {
                return Task.CompletedTask;
            }
            try
            {
                generation = checked(_lastGeneration + 1);
            }
            catch (OverflowException)
            {
                _retired = true;
                return Task.CompletedTask;
            }
            _lastGeneration = generation;
            _attemptCancellation = new CancellationTokenSource();
            token = _attemptCancellation.Token;
            State = OptimizationJourneyReducer.Apply(
                State,
                new OptimizationStarted(generation));
            started = State;
        }
        StateChanged?.Invoke(this, started);
        return ExecuteAsync(generation, token);
    }

    internal Task CancelAsync()
    {
        CancellationTokenSource? cancellation;
        OptimizationJourneyState? cancelling = null;
        OptimizationJourneyState? cancelled = null;
        lock (_gate)
        {
            if (State.Kind != OptimizationJourneyKind.Running)
            {
                return Task.CompletedTask;
            }
            long generation = State.Generation;
            State = OptimizationJourneyReducer.Apply(
                State,
                new OptimizationCancellationRequested(generation));
            cancelling = State;
            cancellation = _attemptCancellation;
            State = OptimizationJourneyReducer.Apply(
                State,
                new OptimizationCancelled(generation));
            cancelled = State;
        }
        StateChanged?.Invoke(this, cancelling);
        cancellation?.Cancel();
        StateChanged?.Invoke(this, cancelled);
        return Task.CompletedTask;
    }

    internal bool Retry()
    {
        OptimizationJourneyState next;
        lock (_gate)
        {
            if (_retired || !State.IsTerminal)
            {
                return false;
            }
            next = OptimizationJourneyState.Initial(State.Entry);
            State = next;
        }
        StateChanged?.Invoke(this, next);
        return true;
    }

    private async Task ExecuteAsync(long generation, CancellationToken token)
    {
        OptimizationExecutionPlan plan = State.Entry.OptimizationHandoff.Plan;
        OptimizationAttemptContext? context = null;
        OptimizationExecutionResult result;
        try
        {
            OptimizationRevalidationResult beforeStaging =
                await _revalidator.RevalidateAsync(plan, token).ConfigureAwait(false);
            if (!beforeStaging.IsCurrent)
            {
                result = OptimizationExecutionResult.ReplanRequired(
                    plan,
                    ReplanCode(beforeStaging.SupportCode),
                    sourceUnchanged: false,
                    _timeProvider.GetUtcNow());
                Complete(generation, result);
                context?.Dispose();
                return;
            }

            context = (await _contextFactory.CreateAsync(generation, token)
                    .ConfigureAwait(false))
                .Validate();
            if (context.Generation != generation
                || !string.Equals(
                    context.Source.SourceSha256,
                    plan.Binding.ModelSha256,
                    StringComparison.Ordinal)
                || context.Source.SourceLengthBytes != plan.Binding.ModelLengthBytes)
            {
                result = OptimizationExecutionResult.ReplanRequired(
                    plan,
                    OptimizationSupportCode.SourceIdentityMismatch,
                    sourceUnchanged: false,
                    _timeProvider.GetUtcNow());
                Complete(generation, result);
                context.Dispose();
                return;
            }

            OptimizationRevalidationResult beforeExecution =
                await _revalidator.RevalidateAsync(plan, token).ConfigureAwait(false);
            if (!beforeExecution.IsCurrent)
            {
                result = OptimizationExecutionResult.ReplanRequired(
                    plan,
                    ReplanCode(beforeExecution.SupportCode),
                    sourceUnchanged: true,
                    _timeProvider.GetUtcNow());
                Complete(generation, result);
                context.Dispose();
                return;
            }

            if (!_router.TryResolve(plan.Route, out IOptimizationExecutor? executor)
                || executor is null)
            {
                result = OptimizationExecutionResult.ReplanRequired(
                    plan,
                    OptimizationSupportCode.ToolNotAdmitted,
                    sourceUnchanged: true,
                    _timeProvider.GetUtcNow());
                Complete(generation, result);
                context.Dispose();
                return;
            }

            var progress = new InlineProgress<OptimizationProgress>(value =>
                ReportProgress(value));
            result = await executor.ExecuteAsync(
                    plan,
                    context,
                    progress,
                    token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            result = OptimizationExecutionResult.Cancelled(
                plan,
                sourceUnchanged: context is not null,
                _timeProvider.GetUtcNow());
        }
        catch
        {
            result = OptimizationExecutionResult.Failed(
                plan,
                OptimizationSupportCode.UnexpectedFailure,
                sourceUnchanged: false,
                _timeProvider.GetUtcNow());
        }
        Complete(generation, result);
        context?.Dispose();
    }

    private void ReportProgress(OptimizationProgress progress)
    {
        OptimizationJourneyState? next = null;
        lock (_gate)
        {
            OptimizationJourneyState candidate = OptimizationJourneyReducer.Apply(
                State,
                new OptimizationProgressed(progress));
            if (!ReferenceEquals(candidate, State) && candidate != State)
            {
                State = candidate;
                next = candidate;
            }
        }
        if (next is not null)
        {
            StateChanged?.Invoke(this, next);
        }
    }

    private void Complete(long generation, OptimizationExecutionResult result)
    {
        OptimizationJourneyState? completed = null;
        CancellationTokenSource? cancellation = null;
        lock (_gate)
        {
            OptimizationJourneyState candidate = OptimizationJourneyReducer.Apply(
                State,
                new OptimizationCompleted(generation, result));
            if (candidate != State)
            {
                State = candidate;
                completed = candidate;
                cancellation = _attemptCancellation;
                _attemptCancellation = null;
            }
        }
        cancellation?.Dispose();
        if (completed is not null)
        {
            StateChanged?.Invoke(this, completed);
        }
    }

    private static OptimizationSupportCode ReplanCode(
        OptimizationSupportCode code) => code is
            OptimizationSupportCode.SourceIdentityMismatch
            or OptimizationSupportCode.CapabilityDrift
            or OptimizationSupportCode.ModelBindingMismatch
            or OptimizationSupportCode.HardwareBindingMismatch
            or OptimizationSupportCode.ToolNotAdmitted
                ? code
                : OptimizationSupportCode.CapabilityDrift;

    public async ValueTask DisposeAsync()
    {
        CancellationTokenSource? cancellation;
        lock (_gate)
        {
            _retired = true;
            cancellation = _attemptCancellation;
            _attemptCancellation = null;
        }
        if (cancellation is not null)
        {
            await cancellation.CancelAsync().ConfigureAwait(false);
            cancellation.Dispose();
        }
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
