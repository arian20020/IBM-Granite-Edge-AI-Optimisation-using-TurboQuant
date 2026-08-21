using System;

namespace GraniteEdgeAI.Features.GgufRuntime;

internal sealed class ChatRenderScheduler : IDisposable
{
    private readonly object sync = new();
    private readonly Func<Action, bool> enqueue;
    private readonly Action render;
    private bool isPending;
    private bool isDisposed;

    internal ChatRenderScheduler(Func<Action, bool> enqueue, Action render)
    {
        this.enqueue = enqueue ?? throw new ArgumentNullException(nameof(enqueue));
        this.render = render ?? throw new ArgumentNullException(nameof(render));
    }

    internal void Request()
    {
        lock (sync)
        {
            if (isDisposed || isPending)
            {
                return;
            }

            isPending = true;
            try
            {
                if (!enqueue(Drain))
                {
                    isPending = false;
                }
            }
            catch
            {
                isPending = false;
                throw;
            }
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            isDisposed = true;
            isPending = false;
        }
    }

    private void Drain()
    {
        lock (sync)
        {
            isPending = false;
            if (isDisposed)
            {
                return;
            }

            render();
        }
    }
}
