using GraniteEdgeAI.GgufRuntime.Contracts.Events;
using GraniteEdgeAI.GgufRuntime.FakeCli;
using GraniteEdgeAI.GgufRuntime.Worker;
using GraniteEdgeAI.GgufRuntime.WorkerClient;

namespace GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests;

[TestClass]
public sealed class GgufOutputBoundTests
{
    [TestMethod]
    public async Task StandardErrorFloodCannotBlockOrBecomeChatText()
    {
        string modelFile = Path.GetTempFileName();
        try
        {
            GgufRuntimeClient client = GgufRuntimeClient.CreateForTestFixture(
                GgufTwoTurnConversationTests.ResolveExecutable(typeof(GgufWorkerMarker)),
                GgufTwoTurnConversationTests.ResolveExecutable(typeof(FakeCliMarker)),
                modelFile,
                "stderr-flood");
            await using GgufRuntimeSession session = await client.StartAsync(
                GgufTwoTurnConversationTests.TestConfiguration(),
                [],
                CancellationToken.None);
            var text = new List<string>();

            await foreach (GgufRuntimeEvent runtimeEvent in
                session.GenerateAsync("bounded", CancellationToken.None))
            {
                if (runtimeEvent is TextDeltaEvent delta)
                {
                    text.Add(delta.Text);
                }
            }

            Assert.AreEqual("fake-response-01:bounded", string.Concat(text));
            Assert.IsFalse(text.Any(value => value.Contains("diagnostic-", StringComparison.Ordinal)));
            await session.CloseAsync(CancellationToken.None);
            Assert.AreEqual(0u, session.ActiveProcessCount);
        }
        finally
        {
            File.Delete(modelFile);
        }
    }
}
