using GraniteEdgeAI.EndToEndTests.Automation;

namespace GraniteEdgeAI.EndToEndTests.Journeys;

internal static class R2JourneyGuard
{
    internal static readonly string[] ProducerEvidence =
    [
        "GRANITE_E2E_H1_MANIFEST",
        "GRANITE_E2E_M1_MANIFEST",
        "GRANITE_E2E_Q1_MANIFEST",
        "GRANITE_E2E_ASSET_MANIFEST",
    ];

    internal static void Unsupported(string testName, string surface, string reason, params string[] variables) =>
        PackagedJourneyFixture.Run(testName, session =>
        {
            Assert.IsNotNull(session.ByName(surface, CancellationToken.None));
            Assert.Inconclusive($"Blocked: {reason}");
        }, variables);

    internal static void Surface(string testName, string surface, Action<AutomationSession>? assertion = null, params string[] variables) =>
        PackagedJourneyFixture.Run(testName, session =>
        {
            Assert.IsNotNull(session.ByName(surface, CancellationToken.None));
            assertion?.Invoke(session);
        }, variables);
}
