using System;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.Features.ModelInspection.SourceCustody;
using GraniteEdgeAI.GgufRuntime.Contracts.Configuration;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using CoreCache = GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution.GgufCacheType;
using CoreBackend = GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution.GgufRuntimeBackend;
using RuntimeCache = GraniteEdgeAI.GgufRuntime.Contracts.Configuration.GgufCacheType;
using RuntimeBackend = GraniteEdgeAI.GgufRuntime.Contracts.Configuration.GgufRuntimeBackend;

namespace GraniteEdgeAI.Features.GgufRuntime;

internal sealed class GgufCurrentModelChatRouteLauncher(
    GgufOptimizationProductionAuthorityAccessor authority,
    Action<ChatPage, ChatDemoController> showChat)
    : ICurrentModelChatRouteLauncher
{
    private readonly GgufOptimizationProductionAuthorityAccessor _authority =
        authority ?? throw new ArgumentNullException(nameof(authority));
    private readonly Action<ChatPage, ChatDemoController> _showChat =
        showChat ?? throw new ArgumentNullException(nameof(showChat));

    public OptimizationRoute Route => OptimizationRoute.Gguf;

    public async Task<CurrentModelChatLaunchResult> LaunchAsync(
        CurrentModelLaunchContext context,
        ModelSourceLease sourceLease,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(sourceLease);
        if (context.ExactExecutionPayload.Gguf is not { } payload
            || payload.Backend != CoreBackend.Cpu)
        {
            return CurrentModelChatLaunchResult.Failed(
                CurrentModelChatSupportCode.BindingMismatch);
        }

        GgufRuntimeConfiguration configuration = CreateConfiguration(
            $"model-{context.Handoff.ModelSha256[..12]}",
            context.Handoff.ModelSha256,
            payload);
        var request = new GgufChatLaunchRequest(
            _authority.RuntimePackageRoot,
            _authority.TrustedRuntimeManifest.Span,
            sourceLease.SourcePath,
            "Selected model",
            configuration);
        var page = new ChatPage();
        ChatDemoController controller = await ChatDemoController.CreateInitializedProductionAsync(
            page,
            request,
            cancellationToken);
        try
        {
            _showChat(page, controller);
            return CurrentModelChatLaunchResult.Success;
        }
        catch
        {
            await controller.DisposeAsync();
            throw;
        }
    }

    internal static GgufRuntimeConfiguration CreateConfiguration(
        string modelId,
        string modelSha256,
        GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization
            .Execution.GgufExecutionPayload payload) => new(
        modelId,
        modelSha256,
        payload.RuntimeBuildId,
        payload.RuntimeSourceCommit,
        Map(payload.Backend),
        payload.DeviceId,
        payload.ContextSize,
        Map(payload.KeyCacheType),
        Map(payload.ValueCacheType),
        payload.GpuLayerCount,
        payload.FlashAttention,
        payload.ThreadCount,
        payload.BatchSize,
        payload.EvidenceGrade,
        payload.ProfileId,
        payload.MaximumGeneratedTokens);

    private static RuntimeBackend Map(CoreBackend backend) => backend switch
    {
        CoreBackend.Cpu => RuntimeBackend.Cpu,
        CoreBackend.Vulkan => RuntimeBackend.Vulkan,
        CoreBackend.Sycl => RuntimeBackend.Sycl,
        _ => throw new ArgumentOutOfRangeException(nameof(backend)),
    };

    private static RuntimeCache Map(CoreCache cache) => cache switch
    {
        CoreCache.F16 => RuntimeCache.F16,
        CoreCache.Q8Zero => RuntimeCache.Q8Zero,
        CoreCache.Q4Zero => RuntimeCache.Q4Zero,
        CoreCache.Turbo3 => RuntimeCache.Turbo3,
        CoreCache.Turbo4 => RuntimeCache.Turbo4,
        _ => throw new ArgumentOutOfRangeException(nameof(cache)),
    };
}

internal sealed record GgufOptimizationProductionAuthorityAccessor(
    string RuntimePackageRoot,
    ReadOnlyMemory<byte> TrustedRuntimeManifest);
