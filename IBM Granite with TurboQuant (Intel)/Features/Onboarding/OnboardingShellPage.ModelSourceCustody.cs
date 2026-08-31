using System;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.SourceCustody;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.Features.Onboarding;

public sealed partial class OnboardingShellPage
{
    private readonly ModelSourceCustodyRegistry _modelSourceCustodyRegistry = new();
    private CurrentModelChatLaunchRegistry? _currentModelChatLaunchRegistry;

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
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }
        _lifetimeCancellation.Cancel();
        InvalidateActiveHardwareJourney();
        _currentModelChatLaunchRegistry?.Dispose();
        _modelSourceCustodyRegistry.Dispose();
        _handoffRegistry.Dispose();
        _lifetimeCancellation.Dispose();
    }

    internal async Task ShutdownAsync()
    {
        StageFrame.IsHitTestVisible = false;

        if (_attachedModelInspectionPage is { } inspectionPage)
        {
            await inspectionPage.RetireForNavigationAsync();
        }

        await RetireOptimizationAsync();

        if (_attachedChatPage is { } chatPage)
        {
            chatPage.ImportModelRequested -= ChatPage_ImportModelRequested;
            _attachedChatPage = null;
        }

        if (_chatController is { } chatController)
        {
            _chatController = null;
            await chatController.DisposeAsync();
        }

        DetachCompatibilityPage();
        DetachHardwareInspectionPage();
        DetachModelInspectionPage();
        DetachModelImportPage();
        Dispose();
    }
}
