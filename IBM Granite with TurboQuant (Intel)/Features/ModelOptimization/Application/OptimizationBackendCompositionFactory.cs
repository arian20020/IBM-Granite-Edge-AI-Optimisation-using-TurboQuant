using System;
using System.Collections.Generic;
using System.IO;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.Features.ModelInspection.SourceCustody;
using GraniteEdgeAI.Features.ModelOptimization.Execution.Gguf;
using GraniteEdgeAI.Features.ModelOptimization.Execution.OpenVino;
using GraniteEdgeAI.Features.ModelOptimization.Storage;
using GraniteEdgeAI.Features.OpenVinoRoute.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.Features.ApplicationComposition;

namespace GraniteEdgeAI.Features.ModelOptimization.Journey;

internal sealed record OptimizationBackendParts(
    IOptimizationExecutor Executor,
    IOptimizationRevalidator Revalidator,
    IOptimizationAttemptContextFactory ContextFactory);

internal interface IOptimizationBackendBuilder
{
    OptimizationRoute Route { get; }

    OptimizationBackendParts Build(
        OptimizationJourneyEntryContext entry,
        OptimizationOutputRegistry outputs);
}

internal sealed record OptimizationBackendComposition(
    OptimizationJourneyCoordinator Coordinator,
    IOptimizationAttemptContextFactory ContextFactory,
    OptimizationOutputRegistry Outputs,
    IOptimizationExecutor Executor);

internal sealed class OptimizationBackendCompositionFactory
{
    private readonly string _appRoot;
    private readonly IReadOnlyDictionary<OptimizationRoute, IOptimizationBackendBuilder>
        _builders;
    private readonly TimeProvider _timeProvider;

    private OptimizationBackendCompositionFactory(
        ModelSourceCustodyRegistry sourceCustody,
        string appRoot,
        GgufOptimizationProductionAuthority? ggufAuthority,
        OpenVinoOptimizationProductionAuthority? openVinoAuthority,
        OpenVinoOptimizationService? openVinoService,
        TimeProvider? timeProvider = null)
        : this(
            appRoot,
            AvailableBuilders(
                sourceCustody,
                appRoot,
                ggufAuthority,
                openVinoAuthority,
                openVinoService),
            timeProvider ?? TimeProvider.System)
    {
    }

    private OptimizationBackendCompositionFactory(
        string appRoot,
        IEnumerable<IOptimizationBackendBuilder> builders,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(builders);
        _timeProvider = timeProvider
            ?? throw new ArgumentNullException(nameof(timeProvider));
        _appRoot = StoragePathGuard.RequireRoot(appRoot, create: true);

        Dictionary<OptimizationRoute, IOptimizationBackendBuilder> mapped = [];
        foreach (IOptimizationBackendBuilder builder in builders)
        {
            ArgumentNullException.ThrowIfNull(builder);
            if (!Enum.IsDefined(builder.Route) ||
                !mapped.TryAdd(builder.Route, builder))
            {
                throw new ArgumentException(
                    "Each optimization route must have one backend authority.",
                    nameof(builders));
            }
        }
        _builders = mapped;
    }

    internal static OptimizationBackendCompositionFactory CreateForAuthority(
        object authorityToken,
        ModelSourceCustodyRegistry sourceCustody,
        string appRoot,
        GgufOptimizationProductionAuthority? ggufAuthority,
        OpenVinoOptimizationProductionAuthority? openVinoAuthority,
        OpenVinoOptimizationService? openVinoService,
        TimeProvider? timeProvider = null)
    {
        A1BackendProductionAuthorities.AssertAuthorityToken(authorityToken);
        return new(
            sourceCustody,
            appRoot,
            ggufAuthority,
            openVinoAuthority,
            openVinoService,
            timeProvider);
    }

    internal static OptimizationBackendCompositionFactory CreateForAuthority(
        object authorityToken,
        string appRoot,
        IEnumerable<IOptimizationBackendBuilder> builders,
        TimeProvider timeProvider)
    {
        A1BackendProductionAuthorities.AssertAuthorityToken(authorityToken);
        return new(appRoot, builders, timeProvider);
    }

    internal bool TryCreate(
        OptimizationJourneyEntryContext entry,
        out OptimizationBackendComposition? composition)
    {
        ArgumentNullException.ThrowIfNull(entry);
        composition = null;
        OptimizationRoute route = entry.OptimizationHandoff.Plan.Route;
        if (!_builders.TryGetValue(route, out IOptimizationBackendBuilder? builder))
        {
            return false;
        }

        var outputs = new OptimizationOutputRegistry(
            Path.Combine(_appRoot, "OutputStaging"),
            Path.Combine(_appRoot, "Outputs"));
        OptimizationBackendParts parts = builder.Build(entry, outputs)
            ?? throw new InvalidOperationException(
                "The selected backend returned no composition.");
        ArgumentNullException.ThrowIfNull(parts.Executor);
        ArgumentNullException.ThrowIfNull(parts.Revalidator);
        ArgumentNullException.ThrowIfNull(parts.ContextFactory);
        if (parts.Executor.Route != route)
        {
            throw new InvalidOperationException(
                "The selected backend does not match the optimization route.");
        }

        var coordinator = new OptimizationJourneyCoordinator(
            entry,
            new OptimizationExecutorRouter([parts.Executor]),
            parts.Revalidator,
            parts.ContextFactory,
            _timeProvider);
        composition = new OptimizationBackendComposition(
            coordinator,
            parts.ContextFactory,
            outputs,
            parts.Executor);
        return true;
    }

    private static IReadOnlyList<IOptimizationBackendBuilder> AvailableBuilders(
        ModelSourceCustodyRegistry sourceCustody,
        string appRoot,
        GgufOptimizationProductionAuthority? ggufAuthority,
        OpenVinoOptimizationProductionAuthority? openVinoAuthority,
        OpenVinoOptimizationService? openVinoService)
    {
        ArgumentNullException.ThrowIfNull(sourceCustody);
        ArgumentException.ThrowIfNullOrWhiteSpace(appRoot);
        if ((openVinoAuthority is null) != (openVinoService is null))
        {
            throw new ArgumentException(
                "OpenVINO authority and service must be supplied together.");
        }

        List<IOptimizationBackendBuilder> builders = [];
        if (ggufAuthority is not null)
        {
            builders.Add(new GgufBackendBuilder(
                sourceCustody,
                appRoot,
                ggufAuthority));
        }
        if (openVinoAuthority is not null && openVinoService is not null)
        {
            builders.Add(new OpenVinoBackendBuilder(
                sourceCustody,
                appRoot,
                openVinoAuthority,
                openVinoService));
        }
        return builders;
    }

    private sealed class GgufBackendBuilder(
        ModelSourceCustodyRegistry sourceCustody,
        string appRoot,
        GgufOptimizationProductionAuthority authority)
        : IOptimizationBackendBuilder
    {
        public OptimizationRoute Route => OptimizationRoute.Gguf;

        public OptimizationBackendParts Build(
            OptimizationJourneyEntryContext entry,
            OptimizationOutputRegistry outputs)
        {
            IGgufQuantizationRunner runner = authority.QuantizerPackageRoot is
                { } quantizerRoot
                    ? new VerifiedGgufQuantizationRunner(
                        quantizerRoot,
                        authority.QuantizerManifestSha256,
                        TimeSpan.FromHours(2))
                    : UnavailableGgufQuantizationRunner.Instance;
            var contextFactory = new GgufOptimizationAttemptContextFactory(
                sourceCustody,
                Path.Combine(appRoot, "SourceStaging"))
            {
                Plan = entry.OptimizationHandoff.Plan
            };
            var validator = new GgufOptimizationOutputValidator(
                authority.RuntimePackageRoot,
                authority.TrustedRuntimeManifest);
            return new OptimizationBackendParts(
                new GgufOptimizationExecutor(outputs, runner, validator),
                new GgufOptimizationRevalidator(sourceCustody, authority),
                contextFactory);
        }
    }

    private sealed class OpenVinoBackendBuilder(
        ModelSourceCustodyRegistry sourceCustody,
        string appRoot,
        OpenVinoOptimizationProductionAuthority authority,
        OpenVinoOptimizationService service)
        : IOptimizationBackendBuilder
    {
        public OptimizationRoute Route => OptimizationRoute.OpenVino;

        public OptimizationBackendParts Build(
            OptimizationJourneyEntryContext entry,
            OptimizationOutputRegistry outputs)
        {
            var contextFactory = new OpenVinoOptimizationAttemptContextFactory(
                sourceCustody,
                Path.Combine(appRoot, "SourceStaging"))
            {
                Plan = entry.OptimizationHandoff.Plan
            };
            return new OptimizationBackendParts(
                new OpenVinoOptimizationExecutor(
                    sourceCustody,
                    authority,
                    service,
                    Path.Combine(appRoot, "OpenVinoOutputs")),
                new OpenVinoOptimizationRevalidator(sourceCustody, authority),
                contextFactory);
        }
    }
}
