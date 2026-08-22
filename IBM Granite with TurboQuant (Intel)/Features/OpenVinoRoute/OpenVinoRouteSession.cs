using System;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.Prompting;
using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.Features.OpenVinoRoute;

internal sealed record OpenVinoSessionDescriptor(
    Guid InspectionRunId,
    string PackagePath,
    string PackageManifestDigest,
    string ModelSha256,
    long ModelLengthBytes);

/// <summary>Owns one app-visible OpenVINO prompt session.</summary>
public sealed class OpenVinoRouteSession : IPromptRouteSession
{
    private readonly OpenVinoPromptAdapter adapter;

    private OpenVinoRouteSession(OpenVinoPromptAdapter adapter)
    {
        this.adapter = adapter;
    }

    public OpenVinoRouteSnapshot Snapshot => adapter.Snapshot;

    internal SessionStartedEvent? StartupEvidence => adapter.StartupEvidence;

    public PromptRouteCapability Capability =>
        OpenVinoRouteCapability.PromptCapability;

    internal static async Task<OpenVinoRouteSession> StartAsync(
        IOpenVinoPromptChannelFactory channelFactory,
        OpenVinoRouteStateMachine stateMachine,
        OpenVinoSessionDescriptor descriptor,
        Action<PromptEvent> eventSink,
        CancellationToken cancellationToken,
        Action<Guid>? promptTerminalWaitObserver = null,
        OpenVinoRuntimeOptions? runtimeOptions = null)
    {
        OpenVinoPromptAdapter adapter = await OpenVinoPromptAdapter.CreateAsync(
            channelFactory,
            stateMachine,
            descriptor,
            eventSink,
            cancellationToken,
            promptTerminalWaitObserver,
            runtimeOptions).ConfigureAwait(false);
        return new OpenVinoRouteSession(adapter);
    }

    public Task<PromptTurnResult> GenerateAsync(
        string prompt,
        int requestedNewTokens,
        CancellationToken cancellationToken) =>
        adapter.GenerateAsync(prompt, requestedNewTokens, cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) =>
        adapter.StopAsync(cancellationToken);

    public Task StopActiveTurnAsync(
        Guid workerConfirmedTurnId,
        CancellationToken cancellationToken) =>
        adapter.StopActiveTurnAsync(
            workerConfirmedTurnId,
            cancellationToken);

    public Task CancelAsync(CancellationToken cancellationToken) =>
        adapter.CancelAsync(cancellationToken);

    public Task CancelActiveTurnAsync(
        Guid workerConfirmedTurnId,
        CancellationToken cancellationToken) =>
        adapter.CancelActiveTurnAsync(
            workerConfirmedTurnId,
            cancellationToken);

    public Task CloseAsync(CancellationToken cancellationToken) =>
        adapter.CloseAsync(cancellationToken);

    public ValueTask DisposeAsync() => adapter.DisposeAsync();
}
