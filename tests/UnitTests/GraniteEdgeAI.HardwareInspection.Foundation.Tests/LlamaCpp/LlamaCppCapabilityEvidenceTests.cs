using GraniteEdgeAI.HardwareInspection.Foundation.LlamaCpp;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Tests.LlamaCpp;

[TestClass]
public sealed class LlamaCppCapabilityEvidenceTests
{
    private static readonly DateTimeOffset CapturedAtUtc =
        new(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void AvailableEvidenceRetainsPinnedIdentityAndCopiesCapabilityFacts()
    {
        LlamaCppBackend[] backends = [LlamaCppBackend.Cpu];
        LlamaCppVisibleDevice[] devices = [new(0, "CPU")];

        LlamaCppCapabilityEvidence evidence = LlamaCppCapabilityEvidence.Available(
            LlamaCppRuntimeIdentity.PinnedCpu,
            CapturedAtUtc,
            backends,
            devices);
        backends[0] = (LlamaCppBackend)99;
        devices[0] = new LlamaCppVisibleDevice(0, "Changed");

        Assert.AreEqual(LlamaCppCapabilityEvidenceState.Available, evidence.State);
        Assert.AreEqual("LLamaSharp", evidence.RuntimeIdentity!.ManagedPackage);
        Assert.AreEqual("0.27.0", evidence.RuntimeIdentity.ManagedVersion);
        Assert.AreEqual("LLamaSharp.Backend.Cpu", evidence.RuntimeIdentity.BackendPackage);
        Assert.AreEqual("0.27.0", evidence.RuntimeIdentity.BackendVersion);
        Assert.AreEqual(
            "7cbbc45e421d55794d5050d126e0b96511007007",
            evidence.RuntimeIdentity.LlamaSharpCommit);
        Assert.AreEqual(
            "3f7c29d318e317b63f54c558bc69803963d7d88c",
            evidence.RuntimeIdentity.MappedLlamaCppCommit);
        Assert.AreEqual("win-x64", evidence.RuntimeIdentity.RuntimeIdentifier);
        Assert.AreEqual(LlamaCppBackend.Cpu, evidence.Backends.Single());
        Assert.AreEqual(new LlamaCppVisibleDevice(0, "CPU"), evidence.VisibleDevices.Single());
        Assert.AreEqual(CapturedAtUtc, evidence.CapturedAtUtc);
        Assert.IsNull(evidence.Diagnostic);
    }

    [TestMethod]
    public void AvailableEvidenceRequiresExactlyCpuAndContiguousBoundedDevices()
    {
        Assert.Throws<ArgumentException>(() => LlamaCppCapabilityEvidence.Available(
            LlamaCppRuntimeIdentity.PinnedCpu,
            CapturedAtUtc,
            [],
            [new LlamaCppVisibleDevice(0, "CPU")]));
        Assert.Throws<ArgumentException>(() => LlamaCppCapabilityEvidence.Available(
            LlamaCppRuntimeIdentity.PinnedCpu,
            CapturedAtUtc,
            [LlamaCppBackend.Cpu, LlamaCppBackend.Cpu],
            [new LlamaCppVisibleDevice(0, "CPU")]));
        Assert.Throws<ArgumentOutOfRangeException>(() => LlamaCppCapabilityEvidence.Available(
            LlamaCppRuntimeIdentity.PinnedCpu,
            CapturedAtUtc,
            [(LlamaCppBackend)99],
            [new LlamaCppVisibleDevice(0, "CPU")]));
        Assert.Throws<ArgumentException>(() => LlamaCppCapabilityEvidence.Available(
            LlamaCppRuntimeIdentity.PinnedCpu,
            CapturedAtUtc,
            [LlamaCppBackend.Cpu],
            []));
        Assert.Throws<ArgumentException>(() => LlamaCppCapabilityEvidence.Available(
            LlamaCppRuntimeIdentity.PinnedCpu,
            CapturedAtUtc,
            [LlamaCppBackend.Cpu],
            [new LlamaCppVisibleDevice(0, "CPU"), new LlamaCppVisibleDevice(2, "CPU 2")]));

        LlamaCppVisibleDevice[] tooMany = Enumerable.Range(0, 16)
            .Select(ordinal => new LlamaCppVisibleDevice(ordinal, $"CPU {ordinal}"))
            .Append(new LlamaCppVisibleDevice(15, "Extra CPU"))
            .ToArray();
        Assert.Throws<ArgumentException>(() => LlamaCppCapabilityEvidence.Available(
            LlamaCppRuntimeIdentity.PinnedCpu,
            CapturedAtUtc,
            [LlamaCppBackend.Cpu],
            tooMany));
    }

    [TestMethod]
    public void VisibleDeviceRejectsInvalidOrdinalAndUnsafeOrOversizedText()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LlamaCppVisibleDevice(-1, "CPU"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LlamaCppVisibleDevice(16, "CPU"));
        Assert.Throws<ArgumentException>(() => new LlamaCppVisibleDevice(0, " leading"));
        Assert.Throws<ArgumentException>(() => new LlamaCppVisibleDevice(0, "format\u202ename"));
        Assert.Throws<ArgumentException>(() => new LlamaCppVisibleDevice(0, new string('x', 129)));
    }

    [TestMethod]
    public void UnavailableEvidenceContainsOnlyUtcTimeAndOneClosedDiagnostic()
    {
        LlamaCppCapabilityEvidence evidence = LlamaCppCapabilityEvidence.Unavailable(
            CapturedAtUtc,
            LlamaCppCapabilityDiagnosticCode.NativeCapabilityUnavailable);

        Assert.AreEqual(LlamaCppCapabilityEvidenceState.Unavailable, evidence.State);
        Assert.IsNull(evidence.RuntimeIdentity);
        Assert.IsEmpty(evidence.Backends);
        Assert.IsEmpty(evidence.VisibleDevices);
        Assert.AreEqual(
            LlamaCppCapabilityDiagnosticCode.NativeCapabilityUnavailable,
            evidence.Diagnostic);
        Assert.AreEqual(CapturedAtUtc, evidence.CapturedAtUtc);
    }

    [TestMethod]
    public void EvidenceRejectsNonUtcTimeAndUndefinedDiagnostic()
    {
        DateTimeOffset nonUtc = CapturedAtUtc.ToOffset(TimeSpan.FromHours(1));

        Assert.Throws<ArgumentException>(() => LlamaCppCapabilityEvidence.Available(
            LlamaCppRuntimeIdentity.PinnedCpu,
            nonUtc,
            [LlamaCppBackend.Cpu],
            [new LlamaCppVisibleDevice(0, "CPU")]));
        Assert.Throws<ArgumentOutOfRangeException>(() => LlamaCppCapabilityEvidence.Unavailable(
            CapturedAtUtc,
            (LlamaCppCapabilityDiagnosticCode)99));
    }
}
