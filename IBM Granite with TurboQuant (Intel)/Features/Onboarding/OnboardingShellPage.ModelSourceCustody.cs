using System;
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
        if (sourcePage.Request is not { } request
            || request.ExpectedFileIdentity.LengthBytes
                != handoff.ModelLengthBytes)
        {
            return false;
        }

        try
        {
            ModelSourceCustodyKey key = new(
                handoff.ModelInspectionHandoffId,
                handoff.ModelSha256,
                handoff.ModelLengthBytes,
                OptimizationRoute.Gguf);
            return _modelSourceCustodyRegistry.Register(
                new ModelSourceCustodyRecord(key, request.ModelPath));
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
        InvalidateActiveHardwareJourney();
        _currentModelChatLaunchRegistry?.Dispose();
        _modelSourceCustodyRegistry.Dispose();
        _handoffRegistry.Dispose();
    }
}
