using System;
using System.Threading;

namespace GraniteEdgeAI.Features.ModelImport.Selection;

internal sealed class ModelSelectionOperation : IDisposable
{
    private readonly CancellationTokenSource cancellation = new();
    private readonly object gate = new();
    private bool retired;
    private bool disposed;

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
            cancellation.Cancel();
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

            if (!retired)
            {
                retired = true;
                cancellation.Cancel();
            }

            cancellation.Dispose();
            disposed = true;
        }
    }
}
