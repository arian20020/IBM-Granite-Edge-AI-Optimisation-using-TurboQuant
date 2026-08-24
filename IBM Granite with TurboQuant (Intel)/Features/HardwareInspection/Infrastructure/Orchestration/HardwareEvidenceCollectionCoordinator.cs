using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Resolution;
using GraniteEdgeAI.HardwareInspection.Foundation.Dxgi;
using GraniteEdgeAI.HardwareInspection.Foundation.LlamaCpp;
using GraniteEdgeAI.HardwareInspection.Foundation.LlmFit;
using GraniteEdgeAI.HardwareInspection.Foundation.NeuralProcessors;
using GraniteEdgeAI.HardwareInspection.Foundation.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.HardwareInspection.Orchestration;

internal sealed class HardwareEvidenceCollectionCoordinator : IHardwareEvidenceCollectionCoordinator
{
    private const int ProductionNativeConcurrency = 4;
    private const int ProductionExternalConcurrency = 1;
    private readonly IHardwareEvidenceCapture _capture;
    private readonly TimeProvider _timeProvider;
    private readonly int _nativeConcurrency;
    private readonly int _externalConcurrency;

    internal HardwareEvidenceCollectionCoordinator(
        IHardwareEvidenceCapture capture,
        TimeProvider timeProvider)
        : this(
            capture,
            timeProvider,
            ProductionNativeConcurrency,
            ProductionExternalConcurrency)
    {
    }

    internal HardwareEvidenceCollectionCoordinator(
        IHardwareEvidenceCapture capture,
        TimeProvider timeProvider,
        int nativeConcurrency,
        int externalConcurrency)
    {
        _capture = capture ?? throw new ArgumentNullException(nameof(capture));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        ArgumentOutOfRangeException.ThrowIfLessThan(nativeConcurrency, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(nativeConcurrency, 4);
        ArgumentOutOfRangeException.ThrowIfNotEqual(externalConcurrency, 1);
        _nativeConcurrency = nativeConcurrency;
        _externalConcurrency = externalConcurrency;
    }

    public async Task<HardwareEvidenceCollectionResult> CollectAsync(
        HardwareToolLease tools,
        IProgress<HardwareInspectionRunStage> progress,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(progress);
        token.ThrowIfCancellationRequested();

        using CancellationTokenSource lifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
        using SemaphoreSlim nativeLane = new(_nativeConcurrency, _nativeConcurrency);
        using SemaphoreSlim externalLane = new(_externalConcurrency, _externalConcurrency);
        List<Task> tasks = new(capacity: 7);
        Task<WindowsProcessorEvidence>? processor = null;
        Task<WindowsSystemEvidenceObservation>? system = null;
        Task<WindowsStorageEvidence>? storage = null;
        Task<DxgiGraphicsEvidence>? graphics = null;
        Task<NeuralProcessorEvidence>? neuralProcessor = null;
        Task<LlmFitHardwareEvidence>? llmFit = null;
        Task<LlamaCppCapabilityEvidence>? llamaCpp = null;

        try
        {
            progress.Report(HardwareInspectionRunStage.ReadingProcessorInformation);
            processor = RunNativeAsync(
                nativeLane,
                _capture.CaptureProcessorAsync,
                lifetime.Token);
            tasks.Add(processor);

            progress.Report(HardwareInspectionRunStage.ReadingSystemMemory);
            system = RunNativeAsync(nativeLane, CaptureSystemAsync, lifetime.Token);
            storage = RunNativeAsync(nativeLane, _capture.CaptureStorageAsync, lifetime.Token);
            tasks.Add(system);
            tasks.Add(storage);

            progress.Report(HardwareInspectionRunStage.DetectingGraphicsHardware);
            graphics = RunNativeAsync(nativeLane, _capture.CaptureGraphicsAsync, lifetime.Token);
            neuralProcessor = RunNativeAsync(
                nativeLane,
                _capture.CaptureNeuralProcessorAsync,
                lifetime.Token);
            tasks.Add(graphics);
            tasks.Add(neuralProcessor);

            progress.Report(HardwareInspectionRunStage.CheckingLocalInferenceRuntimes);
            llmFit = RunExternalAsync(
                externalLane,
                captureToken => _capture.CaptureLlmFitAsync(tools.LlmFit, captureToken),
                lifetime.Token);
            llamaCpp = RunExternalAsync(
                externalLane,
                captureToken => _capture.CaptureLlamaCppAsync(tools.LlamaCpp, captureToken),
                lifetime.Token);
            tasks.Add(llmFit);
            tasks.Add(llamaCpp);
        }
        catch (Exception) when (!token.IsCancellationRequested)
        {
            lifetime.Cancel();
            await ObserveCleanupAsync(tasks).ConfigureAwait(false);
            return HardwareEvidenceCollectionResult.Failure(
                HardwareEvidenceCollectionFailureCode.ProgressCallbackFailure);
        }
        catch
        {
            lifetime.Cancel();
            await ObserveCleanupAsync(tasks).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            throw;
        }

        List<Task> remaining = [.. tasks];
        while (remaining.Count > 0)
        {
            Task completed = await Task.WhenAny(remaining).ConfigureAwait(false);
            remaining.Remove(completed);
            if (completed.IsCompletedSuccessfully)
            {
                continue;
            }

            lifetime.Cancel();
            await ObserveCleanupAsync(tasks).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return HardwareEvidenceCollectionResult.Failure(
                IsOnlyCancellation(completed)
                    ? HardwareEvidenceCollectionFailureCode.ProviderUnavailable
                    : HardwareEvidenceCollectionFailureCode.OrchestrationFailure);
        }

        token.ThrowIfCancellationRequested();
        return HardwareEvidenceCollectionResult.Success(new CollectedHardwareEvidence(
            llmFit!.Result,
            processor!.Result,
            system!.Result,
            storage!.Result,
            graphics!.Result,
            neuralProcessor!.Result,
            llamaCpp!.Result));
    }

    private async ValueTask<WindowsSystemEvidenceObservation> CaptureSystemAsync(
        CancellationToken token)
    {
        try
        {
            WindowsSystemSnapshot snapshot = await _capture.CaptureSystemAsync(token)
                .ConfigureAwait(false);
            return WindowsSystemEvidenceObservation.Available(snapshot);
        }
        catch (WindowsSystemSnapshotException error)
        {
            return WindowsSystemEvidenceObservation.Unavailable(
                _timeProvider.GetUtcNow().ToUniversalTime(),
                error.ClosedDiagnostic switch
                {
                    WindowsSystemSnapshotDiagnosticCode.MemoryUnavailable =>
                        WindowsSystemObservationDiagnosticCode.MemoryUnavailable,
                    WindowsSystemSnapshotDiagnosticCode.MemoryOverflow =>
                        WindowsSystemObservationDiagnosticCode.MemoryOverflow,
                    WindowsSystemSnapshotDiagnosticCode.MemoryInconsistent =>
                        WindowsSystemObservationDiagnosticCode.MemoryInconsistent,
                    _ => throw new InvalidOperationException(
                        "The Windows system diagnostic is unsupported."),
                });
        }
    }

    private static Task<T> RunNativeAsync<T>(
        SemaphoreSlim lane,
        Func<CancellationToken, ValueTask<T>> action,
        CancellationToken token) => Task.Run(async () =>
        {
            await lane.WaitAsync(token).ConfigureAwait(false);
            try
            {
                return await action(token).ConfigureAwait(false);
            }
            finally
            {
                lane.Release();
            }
        }, CancellationToken.None);

    private static Task<T> RunExternalAsync<T>(
        SemaphoreSlim lane,
        Func<CancellationToken, Task<T>> action,
        CancellationToken token) => Task.Run(async () =>
        {
            await lane.WaitAsync(token).ConfigureAwait(false);
            try
            {
                return await action(token).ConfigureAwait(false);
            }
            finally
            {
                lane.Release();
            }
        }, CancellationToken.None);

    private static async Task ObserveCleanupAsync(IEnumerable<Task> tasks)
    {
        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Every task is complete here; provider details intentionally remain private.
        }
    }

    private static bool IsOnlyCancellation(Task task)
    {
        if (task.IsCanceled)
        {
            return true;
        }

        return task.Exception?.Flatten().InnerExceptions.All(
            exception => exception is OperationCanceledException) == true;
    }
}
