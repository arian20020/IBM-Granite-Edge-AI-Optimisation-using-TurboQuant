using System.Text;
using System.Text.Json;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.NativeIntegrationTests;

/// <summary>
/// Verifies an invalid llama native image is contained in a child process.
/// </summary>
[TestClass]
[TestCategory("NativeIntegration")]
public sealed class InvalidNativeBackendProcessTests
{
    [TestMethod]
    public async Task Run_WithCorruptedLlamaDll_ParentSurvivesAndCapturesFailure()
    {
        using TemporaryProbeSandbox sandbox = TemporaryProbeSandbox.Create(
            PublishedProbeLocation.RequireFromEnvironment());
        string[] llamaDlls = sandbox
            .FindNativeDlls()
            .Where(path => Path.GetFileName(path).StartsWith(
                "llama",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.IsTrue(
            llamaDlls.Length > 0,
            "Published sandbox did not contain a native llama DLL.");

        foreach (string llamaDll in llamaDlls)
        {
            await File.WriteAllTextAsync(
                llamaDll,
                "not a PE file",
                Encoding.UTF8);
        }

        string evidencePath = Path.Combine(
            sandbox.DirectoryPath,
            "evidence",
            "invalid-native-runtime.json");

        ProbeExecutionResult result = await new ProbeProcessRunner().RunAsync(
            sandbox.CreateRequest(
                new[] { "--output", evidencePath },
                TimeSpan.FromSeconds(30)),
            CancellationToken.None);

        Assert.AreEqual(ProcessTerminationKind.Exited, result.TerminationKind);
        Assert.IsNotNull(result.ExitCode);
        Assert.AreNotEqual(0, result.ExitCode.Value);

        if (File.Exists(evidencePath))
        {
            using JsonDocument document =
                EvidenceAssertions.LoadJson(evidencePath);
            string? failureCode = document.RootElement
                .GetProperty("failureCode")
                .GetString();
            string[] allowedCodes =
            {
                "MI-OP-RUNTIME-ARCHITECTURE-MISMATCH",
                "MI-OP-RUNTIME-INITIALISATION-FAILED",
                "MI-OP-RUNTIME-UNAVAILABLE"
            };

            Assert.IsTrue(
                allowedCodes.Contains(failureCode, StringComparer.Ordinal),
                $"Unexpected invalid-native failure code: {failureCode}");
        }
        else
        {
            Assert.IsFalse(
                string.IsNullOrWhiteSpace(result.StandardOutput) &&
                string.IsNullOrWhiteSpace(result.StandardError),
                "Native termination produced neither evidence nor diagnostic output.");
        }
    }
}
