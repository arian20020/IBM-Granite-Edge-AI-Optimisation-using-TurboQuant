using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using System;
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

    public ValueTask WaitForPresentationAsync()
    {
        var completion = new TaskCompletionSource<bool>();
        bool enqueued;
        try
        {
            enqueued = dispatcher.TryEnqueue(() =>
                completion.TrySetResult(true));
        }
        catch (Exception error)
        {
            return ValueTask.FromException(error);
        }

        return enqueued
            ? new ValueTask(completion.Task)
            : ValueTask.FromException(new InvalidOperationException(
                "The secure inspection startup presentation could not be scheduled."));
    }
}
