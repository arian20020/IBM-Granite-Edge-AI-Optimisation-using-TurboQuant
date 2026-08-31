using System;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.ApplicationFaults;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.SourceCustody;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.Features.Onboarding;

public sealed partial class OnboardingShellPage
{
    private readonly object _shutdownGate = new();
    private readonly ModelSourceCustodyRegistry _modelSourceCustodyRegistry = new();
    private CurrentModelChatLaunchRegistry? _currentModelChatLaunchRegistry;
    private Task? _shutdownTask;

    private CurrentModelChatLaunchRegistry CurrentModelChatLaunchRegistry =>
        _currentModelChatLaunchRegistry ??=
            new CurrentModelChatLaunchRegistry(_modelSourceCustodyRegistry);

    private bool TryRegisterModelSourceCustody(
        ModelInspectionPage sourcePage,
        ModelInspectionHandoff handoff)
    {
        OptimizationRoute route;
        string sourcePath;
        if (sourcePage.Request is { } request &&
            request.ExpectedFileIdentity.LengthBytes == handoff.ModelLengthBytes)
        {
            route = OptimizationRoute.Gguf;
            sourcePath = request.ModelPath;
        }
        else if (sourcePage.TryGetOpenVinoSourceDirectory(
            handoff, out string? directoryPath) &&
            !string.IsNullOrWhiteSpace(directoryPath))
        {
            route = OptimizationRoute.OpenVino;
            sourcePath = directoryPath;
        }
        else
        {
            return false;
        }

        try
        {
            ModelSourceCustodyKey key = new(
                handoff.ModelInspectionHandoffId,
                handoff.ModelSha256,
                handoff.ModelLengthBytes,
                route);
            return _modelSourceCustodyRegistry.Register(
                new ModelSourceCustodyRecord(key, sourcePath));
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private void InvalidateModelHandoff(Guid handoffId)
    {
        _handoffRegistry.Invalidate(handoffId);
        _modelSourceCustodyRegistry.Retire(handoffId);
        _currentModelChatLaunchRegistry?.Retire(handoffId);
    }

    public void Dispose()
    {
        Task shutdown = EnsureShutdownStarted();
        _ = shutdown.ContinueWith(
            static task => _ = task.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted
                | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    internal Task ShutdownAsync() => EnsureShutdownStarted();

    private Task EnsureShutdownStarted()
    {
        TaskCompletionSource completion;
        lock (_shutdownGate)
        {
            if (_shutdownTask is not null)
            {
                return _shutdownTask;
            }

            completion = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _shutdownTask = completion.Task;
            Interlocked.Exchange(ref _disposed, 1);
        }

        _ = CompleteShutdownAsync(completion);
        return completion.Task;
    }

    private async Task CompleteShutdownAsync(TaskCompletionSource completion)
    {
        Exception? failure = null;
        try
        {
            try
            {
                await _lifetimeCancellation.CancelAsync();
            }
            catch (Exception exception)
            {
                ReportShutdownFault(exception);
            }

            await _optimizationChatHandoff.CloseAsync();
            await ShutdownCoreAsync();
        }
        catch (Exception exception)
        {
            failure = exception;
            BoundedApplicationFaultReporter.Shared.Report(
                ApplicationFault.FromException(
                    ApplicationFaultCode.ShutdownUnexpected,
                    exception));
        }
        finally
        {
            try
            {
                InvalidateActiveHardwareJourney();
                _currentModelChatLaunchRegistry?.Dispose();
                _modelSourceCustodyRegistry.Dispose();
                _handoffRegistry.Dispose();
                _optimizationChatHandoff.Dispose();
                _lifetimeCancellation.Dispose();
            }
            catch (Exception exception)
            {
                failure ??= exception;
            }
        }

        if (failure is null)
        {
            completion.TrySetResult();
        }
        else
        {
            completion.TrySetException(failure);
        }
    }

    private async Task ShutdownCoreAsync()
    {
        StageFrame.IsHitTestVisible = false;

        if (_attachedModelInspectionPage is { } inspectionPage)
        {
            try
            {
                await inspectionPage.RetireForNavigationAsync();
            }
            catch (Exception exception)
            {
                ReportShutdownFault(exception);
            }
        }

        try
        {
            await RetireOptimizationAsync();
        }
        catch (Exception exception)
        {
            ReportShutdownFault(exception);
        }

        if (_attachedChatPage is { } chatPage)
        {
            chatPage.ImportModelRequested -= ChatPage_ImportModelRequested;
            _attachedChatPage = null;
        }

        if (_chatController is { } chatController)
        {
            _chatController = null;
            try
            {
                await chatController.DisposeAsync();
            }
            catch (Exception exception)
            {
                ReportShutdownFault(exception);
            }
        }
        try
        {
            RetireActiveOptimizationChatTarget();
        }
        catch (Exception exception)
        {
            ReportShutdownFault(exception);
        }

        DetachCompatibilityPage();
        DetachHardwareInspectionPage();
        DetachModelInspectionPage();
        DetachModelImportPage();
    }

    private static void ReportShutdownFault(Exception exception) =>
        BoundedApplicationFaultReporter.Shared.Report(
            ApplicationFault.FromException(
                ApplicationFaultCode.ShutdownUnexpected,
                exception));
}
