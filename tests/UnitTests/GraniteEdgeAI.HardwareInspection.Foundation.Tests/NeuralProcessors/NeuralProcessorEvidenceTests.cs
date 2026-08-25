using GraniteEdgeAI.HardwareInspection.Foundation.NeuralProcessors;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Tests.NeuralProcessors;

[TestClass]
public sealed class NeuralProcessorEvidenceTests
{
    private static readonly DateTimeOffset CapturedAtUtc =
        new(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void PresentRequiresOneSafeNameAndNoDiagnostic()
    {
        NeuralProcessorEvidence evidence = NeuralProcessorEvidence.Present(
            "Fixture Cafe\u0301 NPU",
            CapturedAtUtc);

        Assert.AreEqual(NeuralProcessorEvidenceState.Present, evidence.State);
        Assert.AreEqual("Fixture Cafe\u0301 NPU", evidence.Name);
        Assert.HasCount(0, evidence.Diagnostics);
        Assert.ThrowsExactly<ArgumentException>(() =>
            NeuralProcessorEvidence.Present("unsafe\u202Ename", CapturedAtUtc));
    }

    [TestMethod]
    public void NotPresentHasNeitherNameNorDiagnostic()
    {
        NeuralProcessorEvidence evidence = NeuralProcessorEvidence.NotPresent(CapturedAtUtc);

        Assert.AreEqual(NeuralProcessorEvidenceState.NotPresent, evidence.State);
        Assert.IsNull(evidence.Name);
        Assert.HasCount(0, evidence.Diagnostics);
    }

    [TestMethod]
    public void DetectionUnavailableHasNoNameAndOneClosedDiagnostic()
    {
        NeuralProcessorEvidence evidence = NeuralProcessorEvidence.DetectionUnavailable(
            NeuralProcessorDiagnosticCode.EnumerationMechanismNotApproved,
            CapturedAtUtc);

        Assert.AreEqual(NeuralProcessorEvidenceState.DetectionUnavailable, evidence.State);
        Assert.IsNull(evidence.Name);
        CollectionAssert.AreEqual(
            new[] { NeuralProcessorDiagnosticCode.EnumerationMechanismNotApproved },
            evidence.Diagnostics.ToArray());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            NeuralProcessorEvidence.DetectionUnavailable(
                (NeuralProcessorDiagnosticCode)999,
                CapturedAtUtc));
    }

    [TestMethod]
    public void EveryStateRejectsNonUtcCaptureTime()
    {
        DateTimeOffset nonUtc = CapturedAtUtc.ToOffset(TimeSpan.FromHours(1));

        Assert.ThrowsExactly<ArgumentException>(() => NeuralProcessorEvidence.Present("Fixture", nonUtc));
        Assert.ThrowsExactly<ArgumentException>(() => NeuralProcessorEvidence.NotPresent(nonUtc));
        Assert.ThrowsExactly<ArgumentException>(() => NeuralProcessorEvidence.DetectionUnavailable(
            NeuralProcessorDiagnosticCode.EnumerationMechanismNotApproved,
            nonUtc));
    }

    [TestMethod]
    public async Task ProbeBoundaryCanRepresentAllThreeStatesWithoutConflation()
    {
        INeuralProcessorProbe[] probes =
        [
            new FakeProbe(NeuralProcessorEvidence.Present("Fixture", CapturedAtUtc)),
            new FakeProbe(NeuralProcessorEvidence.NotPresent(CapturedAtUtc)),
            new FakeProbe(NeuralProcessorEvidence.DetectionUnavailable(
                NeuralProcessorDiagnosticCode.EnumerationMechanismNotApproved,
                CapturedAtUtc)),
        ];

        NeuralProcessorEvidence[] evidence = [];
        foreach (INeuralProcessorProbe probe in probes)
        {
            evidence = [.. evidence, await probe.CaptureAsync(CancellationToken.None)];
        }

        CollectionAssert.AreEqual(
            new[]
            {
                NeuralProcessorEvidenceState.Present,
                NeuralProcessorEvidenceState.NotPresent,
                NeuralProcessorEvidenceState.DetectionUnavailable,
            },
            evidence.Select(static item => item.State).ToArray());
    }

    private sealed class FakeProbe(NeuralProcessorEvidence evidence) : INeuralProcessorProbe
    {
        public ValueTask<NeuralProcessorEvidence> CaptureAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(evidence);
        }
    }
}
