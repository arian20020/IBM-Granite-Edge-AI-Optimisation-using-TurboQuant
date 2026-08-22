using System.Text.Json;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.Support;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.Security;

/// <summary>
/// Verifies no TCP listener or established connection is observed for the exact
/// feasibility child process during a successful controlled-model probe.
/// </summary>
[TestClass]
[TestCategory("RealModelIntegration")]
public sealed class NetworkObservationTests
{
    [TestMethod]
    public async Task Run_WithControlledGranite_OpensNoObservedTcpEndpoint()
    {
        using RealModelTestContext context =
            await RealModelTestContext.CreateAsync();
        using TemporaryProbeSandbox sandbox = context.CreateProbeSandbox();
        string evidencePath = context.CreateEvidencePath("network-observation");
        string observationPath = context.CreateEvidencePath(
            "network-observation",
            "tcp-observations.json");
        var observation = new SocketObservation(
            TimeSpan.FromMilliseconds(100));

        ProbeProcessRequest request = sandbox.CreateRequest(
            new[]
            {
                "--model", context.Model.ModelPath,
                "--output", evidencePath
            },
            TimeSpan.FromMinutes(2)) with
        {
            WhileRunningObserver = observation.ObserveAsync
        };

        ProbeExecutionResult process = await new ProbeProcessRunner().RunAsync(
            request,
            CancellationToken.None);

        IReadOnlyList<TcpSocketObservation> observedEndpoints =
            observation.GetSnapshot();

        await File.WriteAllTextAsync(
            observationPath,
            JsonSerializer.Serialize(
                new
                {
                    ChildProcessId = process.ProcessId,
                    observation.SampleCount,
                    ObservedEndpoints = observedEndpoints
                },
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                }));

        Assert.AreEqual(ProcessTerminationKind.Exited, process.TerminationKind);
        Assert.AreEqual(0, process.ExitCode);
        Assert.IsTrue(
            observation.SampleCount > 0,
            "No netstat sample completed while the child process was running.");
        Assert.AreEqual(
            0,
            observedEndpoints.Count,
            "The feasibility process opened an observed TCP listener or established connection: " +
            string.Join(
                ", ",
                observedEndpoints.Select(
                    endpoint =>
                        $"{endpoint.State} {endpoint.LocalEndpoint} -> {endpoint.RemoteEndpoint}")));

        using JsonDocument document = EvidenceAssertions.LoadJson(evidencePath);
        EvidenceAssertions.AssertSuccessfulGraniteVocabOnly(
            document.RootElement,
            context.Model.Manifest);
        EvidenceAssertions.AssertNoGgufFiles(context.EvidenceRoot);
    }
}
