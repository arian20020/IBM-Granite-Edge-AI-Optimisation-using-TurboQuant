using GraniteEdgeAI.Features.HardwareInspection.Domain;
using GraniteEdgeAI.Features.HardwareInspection.Resolution;
using GraniteEdgeAI.HardwareInspection.Foundation.NeuralProcessors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.Resolution;

[TestClass]
public sealed class NeuralProcessorEvidenceResolverTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_MapsPresentExactlyAndPreservesName()
    {
        ComponentResolution<NeuralProcessorFacts> result =
            NeuralProcessorEvidenceResolver.Resolve(
                NeuralProcessorEvidence.Present("Intel AI Boost", HardwareResolutionTestData.Now),
                HardwareResolutionTestData.Now);

        Assert.IsFalse(result.HasCriticalFailure);
        Assert.AreEqual(NpuDetectionState.Present, result.Value!.State);
        Assert.AreEqual("Intel AI Boost", result.Value.Name);
        AssertResolved(result);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_MapsNotPresentExactlyWithoutName()
    {
        ComponentResolution<NeuralProcessorFacts> result =
            NeuralProcessorEvidenceResolver.Resolve(
                NeuralProcessorEvidence.NotPresent(HardwareResolutionTestData.Now),
                HardwareResolutionTestData.Now);

        Assert.AreEqual(NpuDetectionState.NotPresent, result.Value!.State);
        Assert.IsNull(result.Value.Name);
        AssertResolved(result);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_MapsDetectionUnavailableWithoutManufacturingAbsence()
    {
        ComponentResolution<NeuralProcessorFacts> result =
            NeuralProcessorEvidenceResolver.Resolve(
                HardwareResolutionTestData.NeuralProcessor(),
                HardwareResolutionTestData.Now);

        Assert.IsFalse(result.HasCriticalFailure);
        Assert.AreEqual(NpuDetectionState.DetectionUnavailable, result.Value!.State);
        Assert.IsNull(result.Value.Name);
        Assert.AreEqual(EvidenceResolutionState.Unavailable, result.Entries.Single().Resolution);
        CollectionAssert.AreEqual(
            new[] { HardwareResolutionDiagnosticCode.NeuralProcessorUnavailable },
            result.Diagnostics.ToArray());
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_StaleOrFutureObservationMapsToDetectionUnavailable()
    {
        DateTimeOffset[] rejectedTimes =
        [
            HardwareResolutionTestData.Now - HardwareResolutionPolicy.StaticEvidenceMaximumAge -
                TimeSpan.FromTicks(1),
            HardwareResolutionTestData.Now + HardwareResolutionPolicy.FutureClockSkew +
                TimeSpan.FromTicks(1),
        ];

        foreach (DateTimeOffset capturedAt in rejectedTimes)
        {
            ComponentResolution<NeuralProcessorFacts> result =
                NeuralProcessorEvidenceResolver.Resolve(
                    NeuralProcessorEvidence.Present("Do Not Use", capturedAt),
                    HardwareResolutionTestData.Now);

            Assert.IsFalse(result.HasCriticalFailure);
            Assert.AreEqual(NpuDetectionState.DetectionUnavailable, result.Value!.State);
            Assert.IsNull(result.Value.Name);
            Assert.AreEqual(EvidenceResolutionState.Unavailable, result.Entries.Single().Resolution);
            CollectionAssert.Contains(
                result.Diagnostics.ToArray(),
                capturedAt < HardwareResolutionTestData.Now
                    ? HardwareResolutionDiagnosticCode.EvidenceStale
                    : HardwareResolutionDiagnosticCode.ClockFuture);
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_AcceptsOnlyNpuEvidenceAndResolverTime()
    {
        var method = typeof(NeuralProcessorEvidenceResolver).GetMethod(
            "Resolve",
            System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic);

        Assert.IsNotNull(method);
        CollectionAssert.AreEqual(
            new[] { typeof(NeuralProcessorEvidence), typeof(DateTimeOffset) },
            method.GetParameters().Select(static parameter => parameter.ParameterType).ToArray());
    }

    private static void AssertResolved(ComponentResolution<NeuralProcessorFacts> result)
    {
        HardwareEvidenceEntry entry = result.Entries.Single();
        Assert.AreEqual("npu.state", entry.CanonicalField);
        Assert.AreEqual(EvidenceSourceKind.NeuralProcessorProbe, entry.Source);
        Assert.AreEqual(EvidenceResolutionState.ResolvedPrimary, entry.Resolution);
        Assert.AreEqual(EvidenceConfidence.High, entry.Confidence);
        Assert.IsNull(entry.SafeDiagnosticCode);
        Assert.AreEqual(0, result.Diagnostics.Count);
    }
}
