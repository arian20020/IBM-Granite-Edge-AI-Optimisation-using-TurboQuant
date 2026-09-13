using System;
using System.Threading;
using System.Diagnostics;

namespace GraniteEdgeAI.Features.ModelImport.Selection;

internal sealed class ModelSelectionOperation : IDisposable
{
    private readonly CancellationTokenSource cancellation = new();
    private readonly object gate = new();
    private bool retired;
    private bool completed;
    private bool disposed;
    private bool cancellationDisposed;
    private Task cancellationQuiescence = Task.CompletedTask;

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
        try { cancellationQuiescence = ObserveCancellationAsync(cancellation.CancelAsync()); }
        catch (Exception exception) { Trace.TraceWarning("Model-selection cancellation observed {0}.", exception.GetType().Name); }
    }

    private static async Task ObserveCancellationAsync(Task cancellation)
    {
        try { await cancellation.ConfigureAwait(false); }
        catch (Exception exception) { Trace.TraceWarning("Model-selection cancellation observed {0}.", exception.GetType().Name); }
    }

    private void DisposeCancellationSourceWhenQuiescent()
    {
        if (completed && disposed && !cancellationDisposed)
        {
            cancellationDisposed = true;
            _ = DisposeAfterCancellationAsync();
        }
    }

    private async Task DisposeAfterCancellationAsync()
    {
        await cancellationQuiescence.ConfigureAwait(false);
        cancellation.Dispose();
    }
}
