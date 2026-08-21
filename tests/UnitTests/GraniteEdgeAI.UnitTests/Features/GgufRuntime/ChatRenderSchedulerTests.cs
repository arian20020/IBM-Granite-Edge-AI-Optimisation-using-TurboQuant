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
}
