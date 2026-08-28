using System.Windows.Automation;
using GraniteEdgeAI.EndToEndTests.Automation;
using GraniteEdgeAI.EndToEndTests.Infrastructure;

namespace GraniteEdgeAI.EndToEndTests.Journeys;

internal static class PackagedJourneyFixture
{
    internal static void Run(string testName, Action<AutomationSession> journey, params string[] requiredVariables)
    {
        string manifestPath = Require("GRANITE_E2E_CANDIDATE_MANIFEST");
        foreach (string variable in requiredVariables)
        {
            _ = Require(variable);
        }

        if (requiredVariables.Contains("GRANITE_E2E_H1_MANIFEST", StringComparer.Ordinal))
        {
            _ = ProducerEvidenceSet.Load(Require("GRANITE_E2E_H1_MANIFEST"), Require("GRANITE_E2E_M1_MANIFEST"), Require("GRANITE_E2E_Q1_MANIFEST"));
        }

        if (requiredVariables.Contains("GRANITE_E2E_ASSET_MANIFEST", StringComparer.Ordinal))
        {
            _ = AssetManifest.Load(Require("GRANITE_E2E_ASSET_MANIFEST"));
        }

        CandidateManifest candidate = CandidateManifest.Load(manifestPath);
        string evidenceRoot = Environment.GetEnvironmentVariable("GRANITE_E2E_RESULTS_ROOT") ?? Path.Combine(Path.GetTempPath(), "GraniteE1Results");
        string output = Path.Combine(evidenceRoot, Sanitize(testName), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        using OwnedProcessSet processes = new();
        int processId = PackageActivator.Activate(candidate.Aumid);
        processes.Add(processId);
        AutomationSession session = new(processId);
        try
        {
            journey(session);
        }
        catch
        {
            try
            {
                AutomationElement window = session.Window(CancellationToken.None);
                FailureDiagnostics.Capture(window, output);
            }
            catch
            {
                // Preserve the original journey failure if diagnostics are unavailable.
            }

            throw;
        }
    }

    internal static string Require(string variable)
    {
        string? value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(value))
        {
            Assert.Inconclusive($"Blocked by declared guard: environment variable {variable} is not set.");
        }

        return value!;
    }

    private static string Sanitize(string value) => string.Concat(value.Select(character => char.IsLetterOrDigit(character) ? character : '_'));
}
