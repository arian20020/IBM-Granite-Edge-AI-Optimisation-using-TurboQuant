using System;
using System.Threading;

namespace GraniteEdgeAI.Features.ModelImport.Selection;

internal sealed class ModelSelectionOperation : IDisposable
{
    private readonly CancellationTokenSource cancellation = new();
    private readonly object gate = new();
    private bool retired;
    private bool completed;
    private bool disposed;
    private bool cancellationDisposed;

    internal ModelSelectionOperationId Id { get; } = ModelSelectionOperationId.CreateNew();

    internal CancellationToken Token => cancellation.Token;

    internal void Retire()
    {
        lock (gate)
        {
            if (retired || disposed)
            {
                return;
            }

            retired = true;
            RequestCancellation();
        }
    }

    internal void Complete()
    {
        lock (gate)
        {
            completed = true;
            DisposeCancellationSourceWhenQuiescent();
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (!retired)
            {
                retired = true;
                RequestCancellation();
            }

            DisposeCancellationSourceWhenQuiescent();
        }
    }

    private void RequestCancellation()
    {
        try { cancellation.Cancel(); }
        catch (AggregateException) { }
    }

    private void DisposeCancellationSourceWhenQuiescent()
    {
        if (completed && disposed && !cancellationDisposed)
        {
            cancellationDisposed = true;
            cancellation.Dispose();
        }
    }
}
