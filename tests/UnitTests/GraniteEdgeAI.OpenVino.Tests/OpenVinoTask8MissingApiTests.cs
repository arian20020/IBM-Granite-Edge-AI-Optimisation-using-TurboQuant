namespace GraniteEdgeAI.OpenVino.Tests;

[TestClass]
public sealed class OpenVinoTask8MissingApiTests
{
    [TestMethod]
    public void RouteStateSessionAdapterServiceAndCapabilityApiExistTogether()
    {
        string[] requiredTypes =
        [
            "OpenVinoRouteStateMachine",
            "OpenVinoRouteSession",
            "OpenVinoPromptAdapter",
            "OpenVinoRouteService",
            "OpenVinoRouteCapability"
        ];

        Type[] productionTypes = typeof(OpenVinoTask8MissingApiTests)
            .Assembly
            .GetTypes();
        string[] missing = requiredTypes.Where(required =>
            !productionTypes.Any(type => string.Equals(
                type.Name,
                required,
                StringComparison.Ordinal))).ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), missing,
            "Task 8 route API is incomplete.");
    }
}
