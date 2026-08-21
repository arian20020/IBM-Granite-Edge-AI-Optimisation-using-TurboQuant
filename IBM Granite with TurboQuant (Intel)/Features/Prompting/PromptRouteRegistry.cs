using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.Prompting;

public sealed class PromptRouteRegistry
{
    private readonly IReadOnlyDictionary<PromptRouteKind, IPromptRouteAdapter>
        adapters;

    public PromptRouteRegistry(IEnumerable<IPromptRouteAdapter> adapters)
    {
        ArgumentNullException.ThrowIfNull(adapters);
        Dictionary<PromptRouteKind, IPromptRouteAdapter> registered = [];
        foreach (IPromptRouteAdapter adapter in adapters)
        {
            ArgumentNullException.ThrowIfNull(adapter);
            Validate(adapter.Capability);
            if (!registered.TryAdd(adapter.Capability.Kind, adapter))
            {
                throw new ArgumentException(
                    "Only one adapter may own a prompt route kind.",
                    nameof(adapters));
            }
        }

        this.adapters = registered;
    }

    public IReadOnlyCollection<PromptRouteCapability> Capabilities =>
        adapters.Values.Select(adapter => adapter.Capability).ToArray();

    public IPromptRouteAdapter GetRequired(PromptRouteKind kind) =>
        adapters.TryGetValue(kind, out IPromptRouteAdapter? adapter)
            ? adapter
            : throw new KeyNotFoundException(
                $"No prompt adapter is registered for route '{kind}'.");

    public Task<PromptRouteSessionActivation> ActivateAsync(
        IPromptRouteActivation activation,
        Action<PromptEvent> eventSink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(activation);
        ArgumentNullException.ThrowIfNull(eventSink);
        return GetRequired(activation.Kind).ActivateAsync(
            activation,
            eventSink,
            cancellationToken);
    }

    private static void Validate(PromptRouteCapability capability)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentException.ThrowIfNullOrWhiteSpace(capability.RouteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(capability.ConfigurationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(capability.BackendLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(capability.Device);
        ArgumentException.ThrowIfNullOrWhiteSpace(capability.Maturity);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            capability.MaximumContextTokens,
            1);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            capability.DefaultRequestedNewTokens,
            1);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            capability.MaximumRequestedNewTokens,
            capability.DefaultRequestedNewTokens);
    }
}
