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

file enum A1BackendAuthorityKind
{
    FreshCompatibility,
    RouteExactOptimization,
    OfficialOpenVinoWorker,
    InitializedGgufChat,
}

file sealed class A1BackendAuthorityRegistry
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
    private static readonly object s_authorityToken = new();
    private static readonly A1BackendAuthorityRegistry s_registry = new();
    private static readonly A1BackendProductionAuthorities s_shared =
        new();

    private A1BackendProductionAuthorities()
    {
        s_registry.Register(A1BackendAuthorityKind.FreshCompatibility);
        s_registry.Register(A1BackendAuthorityKind.RouteExactOptimization);
        s_registry.Register(A1BackendAuthorityKind.OfficialOpenVinoWorker);
        s_registry.Register(A1BackendAuthorityKind.InitializedGgufChat);
        RegistrationCount = s_registry.Count;
    }

    internal static A1BackendProductionAuthorities Shared => s_shared;

    internal int RegistrationCount { get; }

    internal static A1BackendProductionAuthorities CreateAdditionalRouteForValidation() =>
        new();

    internal static void AssertAuthorityToken(object token)
    {
        if (!ReferenceEquals(token, s_authorityToken))
        {
            throw new InvalidOperationException(
                "A1 backend construction requires the process production authority.");
        }
    }

    internal CompatibilityEvaluationOrchestrator CreateCompatibility(
        ICompatibilityFreshResourcesSource source,
        TimeProvider timeProvider) =>
        CompatibilityEvaluationOrchestrator.CreateForAuthority(
            s_authorityToken,
            source,
            timeProvider);

    internal OptimizationBackendCompositionFactory CreateOptimization(
        ModelSourceCustodyRegistry sourceCustody,
        string appRoot,
        GgufOptimizationProductionAuthority? ggufAuthority,
        OpenVinoOptimizationProductionAuthority? openVinoAuthority,
        OpenVinoOptimizationService? openVinoService,
        TimeProvider timeProvider) =>
        OptimizationBackendCompositionFactory.CreateForAuthority(
            s_authorityToken,
            sourceCustody,
            appRoot,
            ggufAuthority,
            openVinoAuthority,
            openVinoService,
            timeProvider);

    internal OptimizationBackendCompositionFactory CreateOptimizationForValidation(
        string appRoot,
        IEnumerable<IOptimizationBackendBuilder> builders,
        TimeProvider timeProvider) =>
        OptimizationBackendCompositionFactory.CreateForAuthority(
            s_authorityToken,
            appRoot,
            builders,
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
            s_authorityToken,
            page,
            request,
            cancellationToken);
}
