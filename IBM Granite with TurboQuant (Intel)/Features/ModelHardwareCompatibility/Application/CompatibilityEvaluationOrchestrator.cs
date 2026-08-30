using System;
using System.Collections.Generic;
using System.IO;
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

    internal async Task<CompatibilityEvaluation> EvaluateAuthorityAsync(
        IReadOnlySet<string> optedInEvidence,
        Func<CompatibilityFreshResourcesInput, IReadOnlySet<string>,
            DateTimeOffset, CancellationToken, CompatibilityEvaluation> evaluator,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(optedInEvidence);
        ArgumentNullException.ThrowIfNull(evaluator);
        CompatibilityFreshResourcesInput? fresh =
            await CaptureFreshAsync(cancellationToken);
        if (fresh is null)
        {
            return new CompatibilityEvaluation(
                Fallback(cancellationToken), null, null);
        }

        DateTimeOffset evaluatedAtUtc = _timeProvider.GetUtcNow();
        return await Task.Run(
            () => evaluator(
                fresh,
                optedInEvidence,
                evaluatedAtUtc,
                cancellationToken),
            cancellationToken);
    }

    internal async Task<CompatibilityScreenModel> EvaluateBoundAsync(
        CompatibilityFreshInputBinder binder,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(binder);
        CompatibilityFreshResourcesInput? fresh =
            await CaptureFreshAsync(cancellationToken);
        if (fresh is null ||
            !binder(fresh, out CompatibilityProductionInput? input))
        {
            return Fallback(cancellationToken);
        }

        return await Task.Run(
            () => CompatibilityEngine.Run(
                input!,
                _timeProvider,
                cancellationToken),
            cancellationToken);
    }

    private async Task<CompatibilityFreshResourcesInput?> CaptureFreshAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            return await _freshResourcesSource.CaptureAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException)
        {
            return null;
        }
    }

    private static CompatibilityScreenModel Fallback(
        CancellationToken cancellationToken) =>
        CompatibilityEngine.RunWithAvailableAdapters(cancellationToken);
}
