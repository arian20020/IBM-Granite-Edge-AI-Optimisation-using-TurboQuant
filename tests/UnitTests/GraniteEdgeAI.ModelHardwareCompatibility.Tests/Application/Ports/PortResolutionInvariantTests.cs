using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Ports;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Ports;

/// <summary>
/// The port results are the boundary where facts from other teams enter this
/// feature. These pin the states that boundary refuses to represent at all,
/// rather than merely discouraging.
/// </summary>
[TestClass]
public sealed class PortResolutionInvariantTests
{
    private static InspectedModelFacts ModelFacts() =>
        InspectedModelFacts.Create(
            ByteCount.FromBytes(1024), 32, 4096, 32, 8, 8192, 15, 2);

    private static HardwareFacts MachineFacts() =>
        HardwareFacts.Create(
            ByteCount.FromBytes(1024),
            ByteCount.Zero,
            ByteCount.FromBytes(1024),
            new HashSet<DeviceRouteId> { DeviceRouteId.Cpu },
            new HashSet<CompatibilityBackend> { CompatibilityBackend.Cpu });

    [TestMethod]
    public void AnUnavailableResolution_CannotReportTheNoneReason()
    {
        // Reporting an unknown as the zero value is the exact substitution this
        // design forbids everywhere else; the seams are no exception.
        Assert.ThrowsExactly<ArgumentException>(
            () => ModelFactsResolution.Unavailable(PortUnavailableReason.None));

        Assert.ThrowsExactly<ArgumentException>(
            () => HardwareFactsResolution.Unavailable(PortUnavailableReason.None));

        Assert.ThrowsExactly<ArgumentException>(
            () => FreshMemoryReading.Unavailable(PortUnavailableReason.None));

        Assert.ThrowsExactly<ArgumentException>(
            () => VerificationOutcome.Unavailable(PortUnavailableReason.None));

        Assert.ThrowsExactly<ArgumentException>(
            () => HandoffClaim.Refused(PortUnavailableReason.None));
    }

    [TestMethod]
    public void AnEstablishedResolution_AlwaysCarriesItsValue()
    {
        // "Established with nothing established" would be a null reference waiting
        // for the coordinator to dereference at the safety gate.
        Assert.IsNotNull(ModelFactsResolution.Established(ModelFacts()).Facts);
        Assert.IsNotNull(HardwareFactsResolution.Established(MachineFacts()).Facts);
        Assert.IsNotNull(
            FreshMemoryReading.Established(
                AvailableResources.Create(
                    ByteCount.FromBytes(1024),
                    ByteCount.Zero,
                    ByteCount.FromBytes(1024),
                    DateTimeOffset.UtcNow))
                .Resources);
    }

    [TestMethod]
    public void AnEstablishedResolution_RejectsANullValue()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => ModelFactsResolution.Established(null!));

        Assert.ThrowsExactly<ArgumentNullException>(
            () => HardwareFactsResolution.Established(null!));

        Assert.ThrowsExactly<ArgumentNullException>(
            () => FreshMemoryReading.Established(null!));
    }

    [TestMethod]
    public void AnUnavailableResolution_ReportsNoValue()
    {
        Assert.IsNull(
            ModelFactsResolution.Unavailable(
                PortUnavailableReason.HandoffStale).Facts);

        Assert.IsNull(
            FreshMemoryReading.Unavailable(
                PortUnavailableReason.AdapterNotImplemented).Resources);
    }

    [TestMethod]
    [DataRow("C:/models/granite.gguf")]
    [DataRow("C:\\models\\granite.gguf")]
    [DataRow("../granite.gguf")]
    [DataRow("granite-q4.gguf")]
    [DataRow("\\\\server\\share\\model")]
    public void AClaimedHandoff_RefusesAnIdentityShapedLikeAPathOrFilename(string identity)
    {
        // The gateway adapter is written by another team, so this is where a path
        // would first arrive. Refusing it at the boundary is cheaper than
        // allowlisting it after it has been carried inward.
        Assert.ThrowsExactly<ArgumentException>(
            () => HandoffClaim.Claimed(identity, "hardware-run-1"));

        Assert.ThrowsExactly<ArgumentException>(
            () => HandoffClaim.Claimed("model-run-1", identity));
    }

    [TestMethod]
    public void AClaimedHandoff_RefusesABlankIdentity()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => HandoffClaim.Claimed("   ", "hardware-run-1"));

        Assert.ThrowsExactly<ArgumentException>(
            () => HandoffClaim.Claimed("model-run-1", "   "));
    }

    [TestMethod]
    public void AClaimedHandoff_AcceptsAnOrdinaryRunIdentity()
    {
        HandoffClaim claim = HandoffClaim.Claimed(
            "model-run-1", "0f8fad5b-d9cb-469f-a165-70867728950e");

        Assert.IsTrue(claim.IsClaimed);
        Assert.AreEqual(
            nameof(PortUnavailableReason.None), claim.Reason.ToString());
    }

    [TestMethod]
    public void ARefusedClaim_CarriesNoIdentity()
    {
        HandoffClaim claim = HandoffClaim.Refused(PortUnavailableReason.HandoffUnavailable);

        Assert.IsFalse(claim.IsClaimed);
        Assert.AreEqual(string.Empty, claim.ModelInspectionRunId);
        Assert.AreEqual(string.Empty, claim.ProductHardwareRunId);
    }

    [TestMethod]
    public void ADefaultedRunId_ReportsItselfEmpty()
    {
        // A record struct cannot forbid `default`, and every defaulted instance
        // compares equal to every other — so consumers that decide anything on
        // identity must be able to detect one.
        Assert.IsTrue(default(CompatibilityRunId).IsEmpty);
        Assert.IsFalse(CompatibilityRunId.New().IsEmpty);
    }

    [TestMethod]
    public void ADefaultedRunId_CannotReachAResult()
    {
        // Two results built from defaulted ids would compare equal on identity,
        // which is exactly the confusion stale-run rejection exists to prevent.
        Assert.ThrowsExactly<ArgumentException>(
            () => CompatibilityRunResult.Cancelled(
                default,
                [],
                [PolicyIdentity.Create("estimator", "v1", PolicyProvenance.Provisional)],
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow));
    }
}
