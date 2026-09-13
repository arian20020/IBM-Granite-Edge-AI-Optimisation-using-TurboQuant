using System.Reflection;
using GraniteEdgeAI.HardwareInspection.Foundation.NeuralProcessors;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Tests.NeuralProcessors;

[TestClass]
public sealed class UnavailableNeuralProcessorProbeTests
{
    private static readonly DateTimeOffset CapturedAtUtc =
        new(2026, 8, 23, 12, 30, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task DefaultProbeReturnsExactTruthfulUnavailableEvidenceWithUtcTime()
    {
        UnavailableNeuralProcessorProbe probe = new(
            new FixedTimeProvider(CapturedAtUtc.ToOffset(TimeSpan.FromHours(1))));

        NeuralProcessorEvidence evidence = await probe.CaptureAsync(CancellationToken.None);

        Assert.AreEqual(NeuralProcessorEvidenceState.DetectionUnavailable, evidence.State);
        Assert.IsNull(evidence.Name);
        Assert.AreEqual(CapturedAtUtc, evidence.CapturedAtUtc);
        CollectionAssert.AreEqual(
            new[] { NeuralProcessorDiagnosticCode.EnumerationMechanismNotApproved },
            evidence.Diagnostics.ToArray());
    }

    [TestMethod]
    public async Task DefaultProbePropagatesPreCancellationWithCallerToken()
    {
        UnavailableNeuralProcessorProbe probe = new(new FixedTimeProvider(CapturedAtUtc));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        OperationCanceledException error = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await probe.CaptureAsync(cancellation.Token));

        Assert.AreEqual(cancellation.Token, error.CancellationToken);
    }

    [TestMethod]
    public void DefaultProbeRetainsOnlyTimeProviderDependency()
    {
        FieldInfo[] fields = typeof(UnavailableNeuralProcessorProbe).GetFields(
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.HasCount(1, fields);
        Assert.AreEqual(typeof(TimeProvider), fields[0].FieldType);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
