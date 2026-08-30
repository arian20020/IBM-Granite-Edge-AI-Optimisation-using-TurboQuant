using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.GgufRuntime;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;
using GraniteEdgeAI.Features.ModelInspection.SourceCustody;
using GraniteEdgeAI.Features.ModelOptimization.Journey;
using GraniteEdgeAI.Features.OpenVinoRoute.Optimization;
using GraniteEdgeAI.OpenVino.WorkerClient;

namespace GraniteEdgeAI.Features.ApplicationComposition;

internal enum A1BackendAuthorityKind
{
    FreshCompatibility,
    RouteExactOptimization,
    OfficialOpenVinoWorker,
    InitializedGgufChat,
}

internal sealed class A1BackendAuthorityRegistry
{
    private readonly HashSet<A1BackendAuthorityKind> _registrations = [];

    internal int Count => _registrations.Count;

    internal void Register(A1BackendAuthorityKind kind)
    {
        if (!_registrations.Add(kind))
        {
            throw new InvalidOperationException(
                "Each A1 production backend authority can be registered only once.");
        }
    }
}

/// <summary>
/// The one semantic production entry point for A1-owned backend composition.
/// Registration happens once; live callers may create route-scoped instances
/// only through this authority.
/// </summary>
internal sealed class A1BackendProductionAuthorities
{
    private static readonly A1BackendProductionAuthorities s_shared =
        new(new A1BackendAuthorityRegistry());

    internal A1BackendProductionAuthorities(A1BackendAuthorityRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(A1BackendAuthorityKind.FreshCompatibility);
        registry.Register(A1BackendAuthorityKind.RouteExactOptimization);
        registry.Register(A1BackendAuthorityKind.OfficialOpenVinoWorker);
        registry.Register(A1BackendAuthorityKind.InitializedGgufChat);
        RegistrationCount = registry.Count;
    }

    internal static A1BackendProductionAuthorities Shared => s_shared;

    internal int RegistrationCount { get; }

    internal CompatibilityEvaluationOrchestrator CreateCompatibility(
        ICompatibilityFreshResourcesSource source,
        TimeProvider timeProvider) => new(source, timeProvider);

    internal OptimizationBackendCompositionFactory CreateOptimization(
        ModelSourceCustodyRegistry sourceCustody,
        string appRoot,
        GgufOptimizationProductionAuthority? ggufAuthority,
        OpenVinoOptimizationProductionAuthority? openVinoAuthority,
        OpenVinoOptimizationService? openVinoService,
        TimeProvider timeProvider) => new(
            sourceCustody,
            appRoot,
            ggufAuthority,
            openVinoAuthority,
            openVinoService,
            timeProvider);

    internal OpenVinoWorkerInstallation CreateOfficialWorkerInstallation(
        string workerRoot,
        string expectedManifestDigest) =>
        OpenVinoOfficialWorkerAuthority.CreateInstallation(
            workerRoot,
            expectedManifestDigest);

    internal Task<ChatDemoController> CreateInitializedChatAsync(
        ChatPage page,
        GgufChatLaunchRequest request,
        CancellationToken cancellationToken) =>
        ChatDemoController.CreateInitializedProductionAsync(
            page,
            request,
            cancellationToken);
}
