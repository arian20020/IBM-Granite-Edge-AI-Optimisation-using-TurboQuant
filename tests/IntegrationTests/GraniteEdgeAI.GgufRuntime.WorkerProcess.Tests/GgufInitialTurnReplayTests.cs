using GraniteEdgeAI.GgufRuntime.Contracts.Commands;
using GraniteEdgeAI.GgufRuntime.Contracts.Events;
using GraniteEdgeAI.GgufRuntime.FakeCli;
using GraniteEdgeAI.GgufRuntime.Worker;
using GraniteEdgeAI.GgufRuntime.WorkerClient;

namespace GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests;

[TestClass]
public sealed class GgufInitialTurnReplayTests
{
    [TestMethod]
    public async Task StartSendsOrderedUserAndAssistantTurnsBeforeTheNextPrompt()
    {
        string modelFile = Path.GetTempFileName();
        try
        {
            GgufRuntimeClient client = GgufRuntimeClient.CreateForTestFixture(
                GgufTwoTurnConversationTests.ResolveExecutable(typeof(GgufWorkerMarker)),
                GgufTwoTurnConversationTests.ResolveExecutable(typeof(FakeCliMarker)),
                modelFile,
                "two-turn");
            GgufConversationTurn[] initialTurns =
            [
                new(GgufConversationRole.User, "first\nquestion"),
                new(GgufConversationRole.Assistant, "first answer"),
            ];
            await using GgufRuntimeSession session = await client.StartAsync(
                GgufTwoTurnConversationTests.TestConfiguration(),
                initialTurns,
                CancellationToken.None);

            var events = new List<GgufRuntimeEvent>();
            await foreach (GgufRuntimeEvent runtimeEvent in session.GenerateAsync(
                               "follow up",
                               CancellationToken.None))
            {
                events.Add(runtimeEvent);
            }

            Assert.AreEqual(
                "fake-response-01:history=UA:follow up",
                string.Concat(events.OfType<TextDeltaEvent>().Select(item => item.Text)));
        }
        finally
        {
            File.Delete(modelFile);
        }
    }
}
