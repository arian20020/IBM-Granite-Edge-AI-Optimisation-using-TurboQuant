using GraniteEdgeAI.Features.HardwareInspection.Domain;
using GraniteEdgeAI.Features.HardwareInspection.Resolution;
using GraniteEdgeAI.HardwareInspection.Foundation.LlamaCpp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.Resolution;

[TestClass]
public sealed class RuntimeEvidenceResolverTests
{
    private const string ExpectedIdentity =
        "LLamaSharp/0.27.0;LLamaSharp.Backend.Cpu/0.27.0;" +
        "llama.cpp/3f7c29d318e317b63f54c558bc69803963d7d88c;win-x64";

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_MapsPinnedIdentityCpuBackendAndOrderedBufferLabelsExactly()
    {
        LlamaCppCapabilityEvidence evidence = LlamaCppCapabilityEvidence.Available(
            LlamaCppRuntimeIdentity.PinnedCpu,
            HardwareResolutionTestData.Now,
            [LlamaCppBackend.Cpu],
            [
                new LlamaCppVisibleDevice(0, "CPU"),
                new LlamaCppVisibleDevice(1, "Mapped Buffer"),
            ]);

        ComponentResolution<LocalRuntimeCapabilities> result =
            RuntimeEvidenceResolver.Resolve(evidence, HardwareResolutionTestData.Now);

        Assert.IsFalse(result.HasCriticalFailure);
        Assert.AreEqual(ExpectedIdentity, result.Value!.BuildIdentity);
        CollectionAssert.AreEqual(
            new[] { LocalRuntimeBackend.Cpu },
            result.Value.SupportedBackends.ToArray());
        CollectionAssert.AreEqual(
            new[] { "CPU", "Mapped Buffer" },
            result.Value.VisibleDevices.ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                "runtime.buildIdentity",
                "runtime.backends",
                "runtime.visibleDevices",
            },
            result.Entries.Select(static entry => entry.CanonicalField).ToArray());
        Assert.IsTrue(result.Entries.All(static entry =>
            entry.Source == EvidenceSourceKind.LlamaCpp &&
            entry.Resolution == EvidenceResolutionState.ResolvedPrimary));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_AcceptsExactStaticAndFutureClockBoundaries()
    {
        foreach (DateTimeOffset capturedAt in new[]
                 {
                     HardwareResolutionTestData.Now - HardwareResolutionPolicy.StaticEvidenceMaximumAge,
                     HardwareResolutionTestData.Now + HardwareResolutionPolicy.FutureClockSkew,
                 })
        {
            ComponentResolution<LocalRuntimeCapabilities> result =
                RuntimeEvidenceResolver.Resolve(
                    HardwareResolutionTestData.LlamaCpp(capturedAt),
                    HardwareResolutionTestData.Now);
            Assert.IsFalse(result.HasCriticalFailure);
            Assert.IsNotNull(result.Value);
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_RejectsStaleFutureAndUnavailableEvidenceWithoutInventingRuntime()
    {
        (LlamaCppCapabilityEvidence Evidence, HardwareResolutionDiagnosticCode Diagnostic)[] cases =
        [
            (HardwareResolutionTestData.LlamaCpp(
                HardwareResolutionTestData.Now - HardwareResolutionPolicy.StaticEvidenceMaximumAge -
                    TimeSpan.FromTicks(1)), HardwareResolutionDiagnosticCode.EvidenceStale),
            (HardwareResolutionTestData.LlamaCpp(
                HardwareResolutionTestData.Now + HardwareResolutionPolicy.FutureClockSkew +
                    TimeSpan.FromTicks(1)), HardwareResolutionDiagnosticCode.ClockFuture),
            (LlamaCppCapabilityEvidence.Unavailable(
                HardwareResolutionTestData.Now,
                LlamaCppCapabilityDiagnosticCode.NativeCapabilityUnavailable),
                HardwareResolutionDiagnosticCode.LlamaCppUnavailable),
        ];

        foreach ((LlamaCppCapabilityEvidence evidence, HardwareResolutionDiagnosticCode diagnostic) in cases)
        {
            ComponentResolution<LocalRuntimeCapabilities> result =
                RuntimeEvidenceResolver.Resolve(evidence, HardwareResolutionTestData.Now);

            Assert.IsTrue(result.HasCriticalFailure);
            Assert.IsNull(result.Value);
            Assert.IsTrue(result.Entries.All(static entry =>
                entry.Resolution == EvidenceResolutionState.Unavailable));
            CollectionAssert.AreEqual(new[] { diagnostic }, result.Diagnostics.ToArray());
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void RuntimeFactsContainNoModelExecutionClaim()
    {
        string[] propertyNames = typeof(LocalRuntimeCapabilities)
            .GetProperties()
            .Select(static property => property.Name)
            .ToArray();

        CollectionAssert.AreEquivalent(
            new[] { "BuildIdentity", "SupportedBackends", "VisibleDevices" },
            propertyNames);
        Assert.IsFalse(propertyNames.Any(static name =>
            name.Contains("Model", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Execution", StringComparison.OrdinalIgnoreCase)));
    }
}
