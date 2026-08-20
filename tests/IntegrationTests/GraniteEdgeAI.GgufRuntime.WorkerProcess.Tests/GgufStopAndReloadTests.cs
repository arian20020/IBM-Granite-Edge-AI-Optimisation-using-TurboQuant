using GraniteEdgeAI.GgufRuntime.Contracts.Events;
using GraniteEdgeAI.GgufRuntime.FakeCli;
using GraniteEdgeAI.GgufRuntime.Worker;
using GraniteEdgeAI.GgufRuntime.WorkerClient;

namespace GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests;

[TestClass]
public sealed class GgufStopAndReloadTests
{
    [TestMethod]
    public async Task UnresponsiveStopRetainsPartialTextAndReloadsForNextTurn()
    {
        string modelFile = Path.GetTempFileName();
        try
        {
            GgufRuntimeClient client = GgufRuntimeClient.CreateForTestFixture(
                GgufTwoTurnConversationTests.ResolveExecutable(typeof(GgufWorkerMarker)),
                GgufTwoTurnConversationTests.ResolveExecutable(typeof(FakeCliMarker)),
                modelFile,
                "ignore-stop");
            await using GgufRuntimeSession session = await client.StartAsync(
                GgufTwoTurnConversationTests.TestConfiguration(),
                [],
                CancellationToken.None);
            var events = new List<GgufRuntimeEvent>();
            var firstDelta = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

            Task generation = Task.Run(async () =>
            {
                await foreach (GgufRuntimeEvent runtimeEvent in
                    session.GenerateAsync("slow", CancellationToken.None))
                {
                    events.Add(runtimeEvent);
                    if (runtimeEvent is TextDeltaEvent)
                    {
                        firstDelta.TrySetResult();
                    }
                }
            });
            await AwaitFirstDeltaAsync(firstDelta.Task, generation);
            await session.StopAsync(CancellationToken.None);
            await generation.WaitAsync(TimeSpan.FromSeconds(10));

            ResponseStoppedEvent stopped = events.OfType<ResponseStoppedEvent>().Single();
            Assert.AreEqual(GgufStopDisposition.StoppedNeedsReload, stopped.Disposition);
            Assert.IsTrue(events.OfType<TextDeltaEvent>().Any());

            IReadOnlyList<GgufRuntimeEvent> next = await CollectAsync(
                session.GenerateAsync("after-stop", CancellationToken.None));
            Assert.IsTrue(next.OfType<ResponseCompletedEvent>().Any());
            await session.CloseAsync(CancellationToken.None);
            Assert.AreEqual(0u, session.ActiveProcessCount);
        }
        finally
        {
            File.Delete(modelFile);
        }
    }

    private static async Task AwaitFirstDeltaAsync(Task firstDelta, Task generation)
    {
        Task winner = await Task.WhenAny(
            firstDelta,
            generation,
            Task.Delay(TimeSpan.FromSeconds(10)));
        if (ReferenceEquals(winner, generation))
        {
            await generation;
        }

        Assert.AreSame(firstDelta, winner, "The expected streamed delta did not arrive.");
    }

    private static async Task<IReadOnlyList<GgufRuntimeEvent>> CollectAsync(
        IAsyncEnumerable<GgufRuntimeEvent> events)
    {
        var result = new List<GgufRuntimeEvent>();
        await foreach (GgufRuntimeEvent runtimeEvent in events)
        {
            result.Add(runtimeEvent);
        }

        return result;
    }
}
