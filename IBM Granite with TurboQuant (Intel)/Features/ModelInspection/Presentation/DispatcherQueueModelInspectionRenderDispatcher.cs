using Microsoft.UI.Dispatching;
using System;

namespace GraniteEdgeAI.Features.ModelInspection.Presentation;

internal sealed class DispatcherQueueModelInspectionRenderDispatcher :
    IModelInspectionRenderDispatcher
{
    private readonly DispatcherQueue _dispatcherQueue;

    internal DispatcherQueueModelInspectionRenderDispatcher(
        DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue ??
            throw new ArgumentNullException(nameof(dispatcherQueue));
    }

    public bool TryEnqueue(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return _dispatcherQueue.TryEnqueue(() => callback());
    }
}
