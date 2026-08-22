using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelInspection.Presentation;

internal sealed class DispatcherModelInspectionStartupPresentationBarrier :
    IModelInspectionStartupPresentationBarrier
{
    private readonly IModelInspectionRenderDispatcher dispatcher;

    internal DispatcherModelInspectionStartupPresentationBarrier(
        IModelInspectionRenderDispatcher dispatcher)
    {
        this.dispatcher = dispatcher ??
            throw new ArgumentNullException(nameof(dispatcher));
    }

    public async ValueTask WaitForPresentationAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var completion = new TaskCompletionSource<bool>();
        using CancellationTokenRegistration cancellationRegistration =
            cancellationToken.Register(() =>
                completion.TrySetCanceled(cancellationToken));
        bool enqueued;
        try
        {
            enqueued = dispatcher.TryEnqueue(() =>
                completion.TrySetResult(true));
        }
        catch
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }

        if (!enqueued)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException(
                "The secure inspection startup presentation could not be scheduled.");
        }

        await completion.Task;
    }
}
