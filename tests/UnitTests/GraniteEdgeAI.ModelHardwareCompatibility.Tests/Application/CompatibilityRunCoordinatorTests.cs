using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Ports;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application;

[TestClass]
public sealed class CompatibilityRunCoordinatorTests
{
    private const ulong Gibibyte = 1024UL * 1024 * 1024;

    private sealed class StubGateway(bool claims) : ICompatibilityInputGateway
    {
        internal int RollbackCount { get; private set; }

        public HandoffClaim Claim() => claims
            ? HandoffClaim.Claimed("model-run-1", "hardware-run-1")
            : HandoffClaim.Refused(PortUnavailableReason.HandoffUnavailable);

        public void Rollback() => RollbackCount++;

        public bool Commit(CompatibilityRunId runId) => true;
    }

    private sealed class StubModelFacts(InspectedModelFacts? facts) : IInspectedModelFactsSource
    {
        public ModelFactsResolution Resolve(string modelInspectionRunId) => facts is null
            ? ModelFactsResolution.Unavailable(PortUnavailableReason.HandoffUnavailable)
            : ModelFactsResolution.Established(facts);
    }

    private sealed class StubHardwareFacts(HardwareFacts? facts) : IHardwareFactsSource
    {
        public HardwareFactsResolution Resolve(string productHardwareRunId) => facts is null
            ? HardwareFactsResolution.Unavailable(PortUnavailableReason.HandoffUnavailable)
            : HardwareFactsResolution.Established(facts);
    }

    private sealed class CountingProbe(ulong? availableGibibytes) : IFreshSystemMemoryProbe
    {
        internal int ProbeCount { get; private set; }

        public FreshMemoryReading Probe()
        {
            ProbeCount++;

            return availableGibibytes is not { } gibibytes
                ? FreshMemoryReading.Unavailable(PortUnavailableReason.AdapterNotImplemented)
                : FreshMemoryReading.Established(
                    AvailableResources.Create(
                        ByteCount.FromBytes(gibibytes * Gibibyte),
                        ByteCount.FromBytes(8 * Gibibyte),
                        ByteCount.FromBytes(500 * Gibibyte),
                        DateTimeOffset.UtcNow));
        }
    }

    private static InspectedModelFacts ModelFacts(int? declaredContextLimit = 8192) =>
        InspectedModelFacts.Create(
            ByteCount.FromBytes(3 * Gibibyte),
            layerCount: 32,
            embeddingSize: 4096,
            attentionHeadCount: 32,
            keyValueHeadCount: 8,
            declaredContextLimit,
            fileType: 15,
            quantisationVersion: 2);

    private static HardwareFacts MachineFacts() =>
        HardwareFacts.Create(
            ByteCount.FromBytes(64 * Gibibyte),
            ByteCount.FromBytes(8 * Gibibyte),
            ByteCount.FromBytes(500 * Gibibyte),
            new HashSet<DeviceRouteId> { DeviceRouteId.Cpu },
            new HashSet<CompatibilityBackend> { CompatibilityBackend.Cpu });

    private static GgufRouteConfiguration Baseline() =>
        GgufRouteConfiguration.Create(
            GgufWeightFormat.Imported,
            GgufKvCacheFormat.F16,
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            GpuOffloadLevel.None);

    private static CompatibilityRunDependencies Dependencies(
        ICompatibilityInputGateway? gateway = null,
        InspectedModelFacts? modelFacts = null,
        HardwareFacts? machineFacts = null,
        IFreshSystemMemoryProbe? probe = null,
        SupportMatrix? matrix = null,
        bool omitModelFacts = false,
        bool omitMachineFacts = false) =>
        CompatibilityRunDependencies.Create(
            gateway ?? new StubGateway(true),
            new StubModelFacts(omitModelFacts ? null : modelFacts ?? ModelFacts()),
            new StubHardwareFacts(omitMachineFacts ? null : machineFacts ?? MachineFacts()),
            probe ?? new CountingProbe(64),
            matrix ?? SupportMatrix.ProvisionalV1(),
            EstimatorPolicy.ProvisionalV1(),
            SafetyPolicy.ProvisionalV1(),
            new HashSet<string>(),
            TrustedSourceAvailability.None(),
            Baseline(),
            ContextTokenCount.FromTokens(4096),
            TimeProvider.System);

    private static CompatibilityRunResult Run(
        CompatibilityRunDependencies? dependencies = null,
        CancellationToken cancellationToken = default) =>
        CompatibilityRunCoordinator.Execute(
            new CompatibilityRunRequest(CompatibilityContextRequest.ApplicationDefault()),
            dependencies ?? Dependencies(),
            cancellationToken);

    [TestMethod]
    public void Execute_CompletesOnAWorkingMachine()
    {
        CompatibilityRunResult result = Run();

        Assert.AreEqual(nameof(CompatibilityRunOutcome.Completed), result.Outcome.ToString());
        Assert.IsNotNull(result.Assessment);
        Assert.AreEqual(4, result.Assessment!.ModeSelections.Count);
    }

    [TestMethod]
    public void Execute_ProbesFreshMemoryExactlyOncePerRun()
    {
        // Every candidate must be judged against the same observation, or the
        // comparison between two candidates is a comparison of two machines.
        CountingProbe probe = new(64);

        Run(Dependencies(probe: probe));

        Assert.AreEqual(1, probe.ProbeCount);
    }

    [TestMethod]
    public void Execute_FailsAndRollsBackWhenTheClaimIsRefused()
    {
        StubGateway gateway = new(claims: false);

        CompatibilityRunResult result = Run(Dependencies(gateway: gateway));

        Assert.AreEqual(nameof(CompatibilityRunOutcome.Failed), result.Outcome.ToString());
        Assert.IsNull(result.Assessment);
        Assert.AreEqual(1, gateway.RollbackCount);
        Assert.IsTrue(result.Findings.Any(finding =>
            finding.Code == CompatibilityFindingCode.HandoffClaimFailed));
    }

    [TestMethod]
    public void Execute_RollsBackWhenAResolutionFailsAfterASuccessfulClaim()
    {
        // A claimed handoff never released would block every later run.
        StubGateway gateway = new(claims: true);

        Run(Dependencies(gateway: gateway, omitModelFacts: true));

        Assert.AreEqual(1, gateway.RollbackCount);
    }

    [TestMethod]
    public void Execute_RollsBackOnTheSuccessPathToo()
    {
        // Nothing commits during a run; the transfer commits only when the user
        // confirms a choice on a later screen.
        StubGateway gateway = new(claims: true);

        Run(Dependencies(gateway: gateway));

        Assert.AreEqual(1, gateway.RollbackCount);
    }

    [TestMethod]
    public void Execute_IsNotEstablishedWhenModelFactsAreUnavailable()
    {
        CompatibilityRunResult result = Run(Dependencies(omitModelFacts: true));

        Assert.AreEqual(
            nameof(CompatibilityRunOutcome.NotEstablished), result.Outcome.ToString());
        Assert.IsTrue(result.Findings.Any(finding =>
            finding.Code == CompatibilityFindingCode.ModelFactsUnavailable));
    }

    [TestMethod]
    public void Execute_IsNotEstablishedWhenHardwareFactsAreUnavailable()
    {
        CompatibilityRunResult result = Run(Dependencies(omitMachineFacts: true));

        Assert.AreEqual(
            nameof(CompatibilityRunOutcome.NotEstablished), result.Outcome.ToString());
        Assert.IsTrue(result.Findings.Any(finding =>
            finding.Code == CompatibilityFindingCode.HardwareFactsUnavailable));
    }

    [TestMethod]
    public void Execute_IsNotEstablishedWhenTheProbeCannotRead()
    {
        // The shipping state until an adapter exists. A historical figure is never
        // substituted for a reading taken now.
        CompatibilityRunResult result = Run(Dependencies(probe: new CountingProbe(null)));

        Assert.AreEqual(
            nameof(CompatibilityRunOutcome.NotEstablished), result.Outcome.ToString());
        Assert.IsTrue(result.Findings.Any(finding =>
            finding.Code == CompatibilityFindingCode.FreshMemoryUnavailable));
    }

    [TestMethod]
    public void Execute_IsNotEstablishedWhenTheMatrixIsAbsent()
    {
        CompatibilityRunResult result = Run(Dependencies(matrix: SupportMatrix.Absent()));

        Assert.AreEqual(
            nameof(CompatibilityRunOutcome.NotEstablished), result.Outcome.ToString());
        Assert.IsTrue(result.Findings.Any(finding =>
            finding.Code == CompatibilityFindingCode.SupportMatrixUnavailable));
    }

    [TestMethod]
    public void Execute_IsNotEstablishedWhenTheModelDeclaresNoContextLimit()
    {
        // The generator refuses to substitute a limit for an unknown one, and the
        // run must surface that rather than silently producing nothing.
        CompatibilityRunResult result = Run(
            Dependencies(modelFacts: ModelFacts(declaredContextLimit: null)));

        Assert.AreEqual(
            nameof(CompatibilityRunOutcome.NotEstablished), result.Outcome.ToString());
        Assert.IsNull(result.Assessment);
    }

    [TestMethod]
    public void Execute_WithEveryPortUnavailableIsNotEstablished()
    {
        // The production configuration today: no adapter exists for any seam.
        CompatibilityRunResult result = CompatibilityRunCoordinator.Execute(
            new CompatibilityRunRequest(CompatibilityContextRequest.ApplicationDefault()),
            CompatibilityRunDependencies.Create(
                UnavailablePorts.Gateway(),
                UnavailablePorts.ModelFacts(),
                UnavailablePorts.HardwareFacts(),
                UnavailablePorts.MemoryProbe(),
                SupportMatrix.ProvisionalV1(),
                EstimatorPolicy.ProvisionalV1(),
                SafetyPolicy.ProvisionalV1(),
                new HashSet<string>(),
                TrustedSourceAvailability.None(),
                Baseline(),
                ContextTokenCount.FromTokens(4096),
                TimeProvider.System),
            CancellationToken.None);

        Assert.AreNotEqual(
            nameof(CompatibilityRunOutcome.Completed), result.Outcome.ToString());
        Assert.IsNull(result.Assessment);
        Assert.IsTrue(result.Findings.Count > 0);
    }

    [TestMethod]
    public void Execute_CancelsWithoutAnAssessment()
    {
        using CancellationTokenSource source = new();
        source.Cancel();

        CompatibilityRunResult result = Run(cancellationToken: source.Token);

        Assert.AreEqual(nameof(CompatibilityRunOutcome.Cancelled), result.Outcome.ToString());
        Assert.IsNull(result.Assessment);
    }

    [TestMethod]
    public void Execute_RollsBackWhenCancelled()
    {
        using CancellationTokenSource source = new();
        source.Cancel();
        StubGateway gateway = new(claims: true);

        Run(Dependencies(gateway: gateway), source.Token);

        Assert.AreEqual(1, gateway.RollbackCount);
    }

    [TestMethod]
    public void Execute_NamesEveryPolicyItUsed()
    {
        CompatibilityRunResult result = Run();

        Assert.AreEqual(3, result.PolicyIdentities.Count);
        Assert.IsTrue(result.PolicyIdentities.Any(p => p.Version == "estimator-policy-v1"));
        Assert.IsTrue(result.PolicyIdentities.Any(p => p.Version == "fit-safety-policy-v1"));
        Assert.IsTrue(result.PolicyIdentities.Any(p => p.Version == "support-matrix-v1"));
    }

    [TestMethod]
    public void Execute_RecordsTheUncalibratedFindingWhileAnyPolicyIsProvisional()
    {
        Assert.IsTrue(Run().Findings.Any(finding =>
            finding.Code == CompatibilityFindingCode.UncalibratedEstimate));
    }

    [TestMethod]
    public void Execute_GivesEveryRunItsOwnIdentity()
    {
        Assert.AreNotEqual(Run().RunId.Value, Run().RunId.Value);
    }

    [TestMethod]
    public void Execute_CompletesWithinItsOwnTimeWindow()
    {
        CompatibilityRunResult result = Run();

        Assert.IsTrue(result.CompletedAtUtc >= result.StartedAtUtc);
    }

    [TestMethod]
    public void Execute_EvaluatesEveryGeneratedCandidateAgainstTheSameObservation()
    {
        // One probe, many candidates: the assessment must contain more evaluated
        // candidates than there were probe calls, or they were not shared.
        CountingProbe probe = new(64);

        CompatibilityRunResult result = Run(Dependencies(probe: probe));

        Assert.IsNotNull(result.Assessment);
        Assert.IsTrue(result.Assessment!.EvaluatedCandidates.Count > 1);
        Assert.AreEqual(1, probe.ProbeCount);
    }

    [TestMethod]
    public void Execute_OffersUseCurrentModelOnAGenerousMachine()
    {
        CompatibilityRunResult result = Run();

        Assert.IsNotNull(result.Assessment);
        Assert.IsTrue(result.Assessment!.UseCurrentModelAvailable);
    }

    [TestMethod]
    public void Execute_RecordsNoSafeConfigurationOnAMachineTooSmallToRunAnything()
    {
        CompatibilityRunResult result = Run(Dependencies(probe: new CountingProbe(4)));

        Assert.AreEqual(nameof(CompatibilityRunOutcome.Completed), result.Outcome.ToString());
        Assert.IsTrue(result.Findings.Any(finding =>
            finding.Code == CompatibilityFindingCode.NoSafeConfigurationFound));
    }
}
