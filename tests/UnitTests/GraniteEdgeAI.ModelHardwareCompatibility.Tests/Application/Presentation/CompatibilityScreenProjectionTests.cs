using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Ports;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Presentation;

[TestClass]
public sealed class CompatibilityScreenProjectionTests
{
    private const ulong Gibibyte = 1024UL * 1024 * 1024;

    private sealed class Gateway : ICompatibilityInputGateway
    {
        public HandoffClaim Claim() => HandoffClaim.Claimed("model-run-1", "hardware-run-1");

        public void Rollback()
        {
        }

        public bool Commit(CompatibilityRunId runId) => true;
    }

    private sealed class Facts(InspectedModelFacts? facts) : IInspectedModelFactsSource
    {
        public ModelFactsResolution Resolve(string modelInspectionRunId) => facts is null
            ? ModelFactsResolution.Unavailable(PortUnavailableReason.HandoffUnavailable)
            : ModelFactsResolution.Established(facts);
    }

    private sealed class Machine(HardwareFacts facts) : IHardwareFactsSource
    {
        public HardwareFactsResolution Resolve(string productHardwareRunId) =>
            HardwareFactsResolution.Established(facts);
    }

    private sealed class MemoryProbe(ulong gibibytes) : IFreshSystemMemoryProbe
    {
        public FreshMemoryReading Probe() =>
            FreshMemoryReading.Established(
                AvailableResources.Create(
                    ByteCount.FromBytes(gibibytes * Gibibyte),
                    ByteCount.FromBytes(8 * Gibibyte),
                    ByteCount.FromBytes(500 * Gibibyte),
                    DateTimeOffset.UtcNow));
    }

    private static InspectedModelFacts ModelFacts() =>
        InspectedModelFacts.Create(
            ByteCount.FromBytes(3 * Gibibyte), 32, 4096, 32, 8, 8192, 15, 2);

    private static CompatibilityRunResult RunWith(ulong availableGibibytes)
    {
        SupportMatrix matrix = SupportMatrix.ProvisionalV1();

        return CompatibilityRunCoordinator.Execute(
            new CompatibilityRunRequest(CompatibilityContextRequest.ApplicationDefault()),
            CompatibilityRunDependencies.Create(
                new Gateway(),
                new Facts(ModelFacts()),
                new Machine(HardwareFacts.Create(
                    ByteCount.FromBytes(64 * Gibibyte),
                    ByteCount.FromBytes(8 * Gibibyte),
                    ByteCount.FromBytes(500 * Gibibyte),
                    new HashSet<DeviceRouteId> { DeviceRouteId.Cpu },
                    new HashSet<CompatibilityBackend> { CompatibilityBackend.Cpu })),
                new MemoryProbe(availableGibibytes),
                matrix,
                EstimatorPolicy.ProvisionalV1(),
                SafetyPolicy.ProvisionalV1(),
                new HashSet<string>(),
                TrustedSourceAvailability.None(),
                GgufRouteConfiguration.Create(
                    GgufWeightFormat.Imported,
                    GgufKvCacheFormat.F16,
                    CompatibilityBackend.Cpu,
                    DeviceRouteId.Cpu,
                    GpuOffloadLevel.None),
                ContextTokenCount.FromTokens(4096),
                TimeProvider.System),
            CancellationToken.None);
    }

    private static CompatibilityRunResult NotEstablishedResult() =>
        CompatibilityRunResult.NotEstablished(
            CompatibilityRunId.New(),
            [CompatibilityFinding.Create(
                CompatibilityFindingCode.ModelFactsUnavailable, FindingSeverity.Blocking)],
            [PolicyIdentity.Create("estimator", "v1", PolicyProvenance.Provisional)],
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    [TestMethod]
    public void AGenerousMachine_ShowsEstimatedCompatible()
    {
        Assert.AreEqual(
            nameof(CompatibilityScreenState.EstimatedCompatible),
            CompatibilityScreenModel.From(RunWith(64)).State.ToString());
    }

    [TestMethod]
    public void AMachineThatCannotRunAnything_ShowsNoEstimatedSafeConfiguration()
    {
        // Every candidate was sized and compared. This is a conclusion.
        Assert.AreEqual(
            nameof(CompatibilityScreenState.NoEstimatedSafeConfiguration),
            CompatibilityScreenModel.From(RunWith(4)).State.ToString());
    }

    [TestMethod]
    public void AnUnestablishedRun_ShowsNotEstablishedRatherThanNothingFits()
    {
        // The distinction that matters most: "we could not tell you" is not the
        // same claim as "we checked and the answer is no".
        Assert.AreEqual(
            nameof(CompatibilityScreenState.NotEstablished),
            CompatibilityScreenModel.From(NotEstablishedResult()).State.ToString());
    }

    [TestMethod]
    public void AFailedRun_ShowsNotEstablished()
    {
        CompatibilityRunResult failed = CompatibilityRunResult.Failed(
            CompatibilityRunId.New(),
            [CompatibilityFinding.Create(
                CompatibilityFindingCode.HandoffClaimFailed, FindingSeverity.Blocking)],
            [PolicyIdentity.Create("estimator", "v1", PolicyProvenance.Provisional)],
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        Assert.AreEqual(
            nameof(CompatibilityScreenState.NotEstablished),
            CompatibilityScreenModel.From(failed).State.ToString());
    }

    [TestMethod]
    public void ACancelledRun_ShowsCancelled()
    {
        CompatibilityRunResult cancelled = CompatibilityRunResult.Cancelled(
            CompatibilityRunId.New(),
            [],
            [PolicyIdentity.Create("estimator", "v1", PolicyProvenance.Provisional)],
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        Assert.AreEqual(
            nameof(CompatibilityScreenState.Cancelled),
            CompatibilityScreenModel.From(cancelled).State.ToString());
    }

    [TestMethod]
    public void ContinueIsEnabledOnlyWhereSomethingWasConcluded()
    {
        Assert.IsTrue(CompatibilityScreenModel.From(RunWith(64)).ContinueEnabled);
        Assert.IsFalse(CompatibilityScreenModel.From(RunWith(4)).ContinueEnabled);
        Assert.IsFalse(CompatibilityScreenModel.From(NotEstablishedResult()).ContinueEnabled);
    }

    [TestMethod]
    public void AnUnestablishedScreen_CarriesWhatWasMissing()
    {
        // Screen 06 must list the exact missing evidence with recovery actions,
        // which it can only do if the engine hands the codes over.
        CompatibilityScreenModel model = CompatibilityScreenModel.From(NotEstablishedResult());

        Assert.IsTrue(model.Findings.Any(finding =>
            finding.Code == CompatibilityFindingCode.ModelFactsUnavailable));
    }

    [TestMethod]
    public void EveryConcludedScreen_CarriesAllFourModes()
    {
        // An unavailable mode is disabled with its reason, never hidden.
        Assert.AreEqual(4, CompatibilityScreenModel.From(RunWith(64)).ModeSelections.Count);
        Assert.AreEqual(4, CompatibilityScreenModel.From(RunWith(4)).ModeSelections.Count);
    }

    [TestMethod]
    public void AnUnestablishedScreen_CarriesNoModes()
    {
        // Nothing was assessed, so there is nothing to disable with a reason.
        Assert.AreEqual(
            0, CompatibilityScreenModel.From(NotEstablishedResult()).ModeSelections.Count);
    }

    [TestMethod]
    public void TheScreenModel_CarriesNoWordingOfItsOwn()
    {
        // Presentation owns every string the user reads. A string here would be
        // a message the engine wrote, which section 14 forbids reaching a result
        // and which would also be untranslatable.
        Assert.AreEqual(
            0,
            typeof(CompatibilityScreenModel)
                .GetProperties()
                .Count(property => property.PropertyType == typeof(string)));
    }

    [TestMethod]
    public void EveryTerminalOutcome_MapsToAScreen()
    {
        // A new outcome added without a rule would fall to Unspecified, and the
        // page would have nothing to show.
        foreach (CompatibilityRunOutcome outcome in Enum.GetValues<CompatibilityRunOutcome>())
        {
            CompatibilityRunResult result = outcome switch
            {
                CompatibilityRunOutcome.Completed => RunWith(64),
                CompatibilityRunOutcome.Cancelled => CompatibilityRunResult.Cancelled(
                    CompatibilityRunId.New(),
                    [],
                    [PolicyIdentity.Create("e", "v1", PolicyProvenance.Provisional)],
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow),
                _ => NotEstablishedResult()
            };

            Assert.AreNotEqual(
                CompatibilityScreenState.Unspecified,
                CompatibilityScreenModel.From(result).State,
                $"{outcome} maps to no screen.");
        }
    }
}
