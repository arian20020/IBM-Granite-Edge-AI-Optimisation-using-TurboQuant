using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;

internal delegate bool CompatibilityFreshInputBinder(
    CompatibilityFreshResourcesInput freshResources,
    out CompatibilityProductionInput? input);

internal sealed class CompatibilityEvaluationOrchestrator
{
    private readonly ICompatibilityFreshResourcesSource _freshResourcesSource;
    private readonly TimeProvider _timeProvider;

    internal CompatibilityEvaluationOrchestrator(
        ICompatibilityFreshResourcesSource freshResourcesSource,
        TimeProvider timeProvider)
    {
        _freshResourcesSource = freshResourcesSource
            ?? throw new ArgumentNullException(nameof(freshResourcesSource));
        _timeProvider = timeProvider
            ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    internal Task<CompatibilityEvaluation> EvaluateAuthorityAsync(
        IReadOnlySet<string> optedInEvidence,
        Func<CompatibilityFreshResourcesInput, IReadOnlySet<string>,
            DateTimeOffset, CancellationToken, CompatibilityEvaluation> evaluator,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(optedInEvidence);
        ArgumentNullException.ThrowIfNull(evaluator);
        return ExecuteAsync(
            async () =>
            {
                CompatibilityFreshResourcesInput fresh =
                    await _freshResourcesSource.CaptureAsync(cancellationToken);
                DateTimeOffset evaluatedAtUtc = _timeProvider.GetUtcNow();
                return await Task.Run(
                    () => evaluator(
                        fresh,
                        optedInEvidence,
                        evaluatedAtUtc,
                        cancellationToken),
                    cancellationToken);
            },
            () => new CompatibilityEvaluation(Fallback(cancellationToken), null, null));
    }

    internal Task<CompatibilityScreenModel> EvaluateBoundAsync(
        CompatibilityFreshInputBinder binder,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return ExecuteAsync(
            async () =>
            {
                CompatibilityFreshResourcesInput fresh =
                    await _freshResourcesSource.CaptureAsync(cancellationToken);
                if (!binder(fresh, out CompatibilityProductionInput? input))
                {
                    return Fallback(cancellationToken);
                }
                return await Task.Run(
                    () => CompatibilityEngine.Run(
                        input!,
                        _timeProvider,
                        cancellationToken),
                    cancellationToken);
            },
            () => Fallback(cancellationToken));
    }

    private static async Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation,
        Func<T> fallback)
    {
        try
        {
            return await operation();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return fallback();
        }
    }

    private static CompatibilityScreenModel Fallback(
        CancellationToken cancellationToken) =>
        CompatibilityEngine.RunWithAvailableAdapters(cancellationToken);
}
