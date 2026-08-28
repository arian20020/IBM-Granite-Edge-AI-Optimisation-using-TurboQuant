using GraniteEdgeAI.EndToEndTests.Infrastructure;

namespace GraniteEdgeAI.EndToEndTests.Tests;

[TestClass]
[TestCategory("Preflight")]
public sealed class PredecessorEvidencePreflightTests
{
    [TestMethod]
    public void Exact_predecessor_chains_are_closed_and_cleanup_verified()
    {
        string repositoryRoot = Require("GRANITE_E2E_REPOSITORY_ROOT");
        string handoffRoot = Require("GRANITE_E2E_HANDOFF_ROOT");
        string nativeRoot = Require("GRANITE_E2E_NATIVE_RECEIPT_ROOT");
        foreach (string worker in new[] { "H1", "M1", "Q1", "F1" })
        {
            string? manifest = worker == "F1" ? null : Require($"GRANITE_E2E_{worker}_MANIFEST");
            _ = PredecessorEvidenceVerifier.Verify(
                worker,
                repositoryRoot,
                Path.Combine(handoffRoot, $"{worker}.json"),
                Path.Combine(nativeRoot, $"{worker}.json"),
                manifest);
        }
    }

    private static string Require(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            Assert.Inconclusive($"Blocked by declared preflight guard: {name} is not set.");
        }
        return value!;
    }
}
