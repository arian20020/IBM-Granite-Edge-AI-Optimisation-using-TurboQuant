using GraniteEdgeAI.Features.GgufRuntime;

namespace GraniteEdgeAI.UnitTests.Features.GgufRuntime;

[TestClass]
public sealed class ChatRenderSchedulerTests
{
    [TestMethod]
    public void RequestsBeforeDrainShareOneQueuedRender()
    {
        var callbacks = new Queue<Action>();
        int enqueueCount = 0;
        int renderCount = 0;
        using var scheduler = new ChatRenderScheduler(
            callback =>
            {
                enqueueCount++;
                callbacks.Enqueue(callback);
                return true;
            },
            () => renderCount++);

        scheduler.Request();
        scheduler.Request();
        scheduler.Request();

        Assert.AreEqual(1, enqueueCount);
        Assert.AreEqual(0, renderCount);
        callbacks.Dequeue()();
        Assert.AreEqual(1, renderCount);

        scheduler.Request();

        Assert.AreEqual(2, enqueueCount);
        callbacks.Dequeue()();
        Assert.AreEqual(2, renderCount);
    }

    [TestMethod]
    public void RejectedEnqueueCanBeRetried()
    {
        var callbacks = new Queue<Action>();
        int enqueueCount = 0;
        int renderCount = 0;
        using var scheduler = new ChatRenderScheduler(
            callback =>
            {
                enqueueCount++;
                if (enqueueCount == 1)
                {
                    return false;
                }

                callbacks.Enqueue(callback);
                return true;
            },
            () => renderCount++);

        scheduler.Request();
        scheduler.Request();

        Assert.AreEqual(2, enqueueCount);
        Assert.AreEqual(1, callbacks.Count);
        callbacks.Dequeue()();
        Assert.AreEqual(1, renderCount);
    }

    [TestMethod]
    public async Task RequestArrivingDuringRejectedEnqueueIsNotLost()
    {
        using var firstEnqueueStarted = new ManualResetEventSlim();
        using var releaseFirstEnqueue = new ManualResetEventSlim();
        var callbacks = new Queue<Action>();
        int enqueueCount = 0;
        using var scheduler = new ChatRenderScheduler(
            callback =>
            {
                int attempt = Interlocked.Increment(ref enqueueCount);
                if (attempt == 1)
                {
                    firstEnqueueStarted.Set();
                    releaseFirstEnqueue.Wait();
                    return false;
                }

                callbacks.Enqueue(callback);
                return true;
            },
            () => { });

        Task firstRequest = Task.Run(scheduler.Request);
        Assert.IsTrue(firstEnqueueStarted.Wait(TimeSpan.FromSeconds(5)));
        Task newestRequest = Task.Run(scheduler.Request);
        await Task.Delay(250);
        releaseFirstEnqueue.Set();
        await Task.WhenAll(firstRequest, newestRequest).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual(2, enqueueCount);
        Assert.AreEqual(1, callbacks.Count);
    }

    [TestMethod]
    public void DisposeMakesCapturedCallbackHarmless()
    {
        Action? queued = null;
        int renderCount = 0;
        var scheduler = new ChatRenderScheduler(
            callback =>
            {
                queued = callback;
                return true;
            },
            () => renderCount++);

        scheduler.Request();
        scheduler.Dispose();
        Assert.IsNotNull(queued);
        queued!();

        Assert.AreEqual(0, renderCount);
    }

    [TestMethod]
    public async Task DisposeDoesNotReturnWhileRenderIsStillRunning()
    {
        Action? queued = null;
        using var renderStarted = new ManualResetEventSlim();
        using var releaseRender = new ManualResetEventSlim();
        var scheduler = new ChatRenderScheduler(
            callback =>
            {
                queued = callback;
                return true;
            },
            () =>
            {
                renderStarted.Set();
                releaseRender.Wait();
            });

        scheduler.Request();
        Assert.IsNotNull(queued);
        Task drain = Task.Run(queued!);
        Assert.IsTrue(renderStarted.Wait(TimeSpan.FromSeconds(5)));
        Task dispose = Task.Run(scheduler.Dispose);

        Assert.IsFalse(dispose.Wait(TimeSpan.FromMilliseconds(250)));
        releaseRender.Set();
        await Task.WhenAll(drain, dispose).WaitAsync(TimeSpan.FromSeconds(5));
    }
}
