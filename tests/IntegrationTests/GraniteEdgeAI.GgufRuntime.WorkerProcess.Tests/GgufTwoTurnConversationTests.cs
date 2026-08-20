using GraniteEdgeAI.GgufRuntime.Contracts;
using GraniteEdgeAI.GgufRuntime.Contracts.Commands;
using GraniteEdgeAI.GgufRuntime.Contracts.Configuration;
using GraniteEdgeAI.GgufRuntime.Contracts.Events;
using GraniteEdgeAI.GgufRuntime.FakeCli;
using GraniteEdgeAI.GgufRuntime.Worker;
using GraniteEdgeAI.GgufRuntime.WorkerClient;

namespace GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests;

[TestClass]
public sealed class GgufTwoTurnConversationTests
{
    [TestMethod]
    public async Task StreamsTwoCorrelatedTurnsAndClosesWithoutResidue()
    {
        string modelFile = Path.GetTempFileName();
        try
        {
            GgufRuntimeClient client = GgufRuntimeClient.CreateForTestFixture(
                ResolveExecutable(typeof(GgufWorkerMarker)),
                ResolveExecutable(typeof(FakeCliMarker)),
                modelFile,
                "two-turn");
            await using GgufRuntimeSession session = await client.StartAsync(
                TestConfiguration(),
                [],
                CancellationToken.None);

            IReadOnlyList<GgufRuntimeEvent> first = await CollectAsync(
                session.GenerateAsync("first", CancellationToken.None));
            IReadOnlyList<GgufRuntimeEvent> second = await CollectAsync(
                session.GenerateAsync("second", CancellationToken.None));

            Assert.AreEqual(
                "fake-response-01:first",
                string.Concat(first.OfType<TextDeltaEvent>().Select(item => item.Text)));
            Assert.AreEqual(
                "fake-response-02:second",
                string.Concat(second.OfType<TextDeltaEvent>().Select(item => item.Text)));
            Assert.AreEqual(1, first.OfType<ResponseStartedEvent>().Count());
            Assert.AreEqual(1, first.OfType<ResponseCompletedEvent>().Count());
            Assert.AreNotEqual(first[0].RequestId, second[0].RequestId);

            await session.CloseAsync(CancellationToken.None);
            Assert.AreEqual(0u, session.ActiveProcessCount);
        }
        finally
        {
            File.Delete(modelFile);
        }
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

    internal static string ResolveExecutable(Type markerType) =>
        Path.ChangeExtension(markerType.Assembly.Location, ".exe");

    internal static GgufRuntimeConfiguration TestConfiguration() => new(
        "test-model",
        new string('a', 64),
        "test-runtime",
        new string('b', 40),
        GgufRuntimeBackend.Cpu,
        "cpu",
        2048,
        GgufCacheType.F16,
        GgufCacheType.F16,
        0,
        false,
        2,
        32,
        "test-only",
        "cpu-test");
}
