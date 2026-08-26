using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Invariants;

/// <summary>
/// What a plan binds, and what happens when the world moves underneath it.
///
/// A plan is the thing a user confirms. Everything here defends one property:
/// the plan that gets executed is the plan that was agreed to, and any
/// difference is detected rather than absorbed.
/// </summary>
[TestClass]
public sealed class OptimizationPlanBindingTests
{
    private const ulong Gibibyte = 1024UL * 1024 * 1024;

    private static class OptimizationPlanTestData
    {
        internal const string ModelInspectionRunId = "mi-run-1";
        internal const string ModelInspectionHandoffId = "mi-handoff-1";
        internal const string ProductHardwareRunId = "hw-run-1";

        internal const string ModelDigest =
            "1111111111111111111111111111111111111111111111111111111111111111";

        internal const string HardwareDigest =
            "2222222222222222222222222222222222222222222222222222222222222222";

        internal const string CapabilityDigest =
            "3333333333333333333333333333333333333333333333333333333333333333";

        internal static OptimizationJourneyBinding Binding() =>
            OptimizationJourneyBinding.Create(
                ModelInspectionRunId,
                ModelInspectionHandoffId,
                ModelDigest,
                4 * Gibibyte,
                ProductHardwareRunId,
                HardwareDigest);

        internal static OptimizationCandidate Candidate(
            OpenVinoWeightFormat weights = OpenVinoWeightFormat.Int8,
            int context = 4096)
        {
            bool persistent = weights != OpenVinoWeightFormat.Original;

            return OptimizationCandidate.Create(
                OpenVinoRouteConfiguration.Create(
                    weights,
                    OpenVinoKvCacheFormat.U8,
                    DeviceRouteId.Cpu,
                    OpenVinoPerformanceHint.Latency,
                    OpenVinoCompiledCachePolicy.Disabled,
                    1),
                OptimizationCandidateMetrics.Create(
                    EvidenceGrade.Estimated,
                    OptimizationAssessment.Good,
                    OptimizationAssessment.Good,
                    OptimizationAssessment.Good,
                    context,
                    8 * Gibibyte,
                    32 * Gibibyte,
                    24 * Gibibyte,
                    persistent ? 4 * Gibibyte : 0,
                    persistent ? 4 * Gibibyte : 0,
                    persistent,
                    availableDiskBytes: 500 * Gibibyte),
                "ov-int8",
                isExperimental: false);
        }

        internal static OptimizationCapabilitySnapshot Snapshot(
            string digest = CapabilityDigest) => SnapshotFor(Candidate(), digest);

        internal static OptimizationCapabilitySnapshot SnapshotFor(
            OptimizationCandidate candidate,
            string digest = CapabilityDigest)
        {
            OpenVinoRouteConfiguration configuration =
                (OpenVinoRouteConfiguration)candidate.Configuration;
            OpenVinoWeightPrecision target = configuration.Weights switch
            {
                OpenVinoWeightFormat.Fp16 => OpenVinoWeightPrecision.Fp16,
                OpenVinoWeightFormat.Int8 => OpenVinoWeightPrecision.EightBit,
                _ => OpenVinoWeightPrecision.FourBit
            };
            OpenVinoWeightPrecision source =
                configuration.Weights == OpenVinoWeightFormat.Original
                    ? target
                    : OpenVinoWeightPrecision.Fp16;
            OpenVinoBuildIdentity build = OpenVinoBuildIdentity.Create(
                "2026.3.0", "2026.3.0.0", "2026.3.0", HardwareDigest);
            Dictionary<string, string> versions = new(StringComparer.Ordinal)
            {
                ["openvino"] = "2026.3.0"
            };

            return
            OptimizationCapabilitySnapshot.ForOpenVino(
                "ov-cap",
                digest,
                OpenVinoCapabilityPayload.Create(
                    "2026.3.0",
                    [
                        OpenVinoAdmittedConfiguration.Create(
                            candidate.EvidenceId, configuration.Device,
                            configuration.Weights, configuration.KvCache,
                            configuration.PerformanceHint,
                            configuration.CompiledCache, configuration.Streams,
                            512, 32768,
                            SupportLevel.DeclaredSupported, false)
                    ],
                    [
                        OpenVinoExecutionAuthority.Create(
                            candidate.EvidenceId,
                            "openvino.standard.cpu.int8.default.v1",
                            source,
                            build,
                            versions,
                            compiledCacheIsDisposable: true)
                    ]));
        }

        internal static OptimizationWorkload Workload() =>
            OptimizationWorkload.Create(
                "chat", 512, OptimizationAssessment.Poor,
                [ContextTokenCount.FromTokens(4096)]);

        /// <summary>
        /// The execution payload matching whatever candidate is used. V2 requires
        /// one, and requires it to agree - so the fixture derives it from the
        /// candidate rather than restating it.
        /// </summary>
        internal static OptimizationExecutionPayload Payload(
            OptimizationCandidate candidate)
        {
            OpenVinoRouteConfiguration configuration =
                (OpenVinoRouteConfiguration)candidate.Configuration;

            OpenVinoWeightPrecision target = configuration.Weights switch
            {
                OpenVinoWeightFormat.Fp16 => OpenVinoWeightPrecision.Fp16,
                OpenVinoWeightFormat.Int8 => OpenVinoWeightPrecision.EightBit,
                _ => OpenVinoWeightPrecision.FourBit
            };

            // Original converts nothing, so source equals target there.
            OpenVinoWeightPrecision source =
                configuration.Weights == OpenVinoWeightFormat.Original
                    ? target
                    : OpenVinoWeightPrecision.Fp16;

            return OptimizationExecutionPayload.ForOpenVino(
                OpenVinoExecutionPayload.Create(
                    "openvino.standard.cpu.int8.default.v1",
                    "CPU",
                    "Standard candidate",
                    candidate.EvidenceId,
                    source,
                    target,
                    OpenVinoKvCachePrecision.U8,
                    compiledCacheEnabled: false,
                    compiledCacheIsDisposable: true,
                    compiledCacheIsModelArtifact: false,
                    createsCompletePackage: candidate.Metrics.RequiresPersistentChange,
                    OpenVinoBuildIdentity.Create(
                        "2026.3.0", "2026.3.0.0", "2026.3.0", HardwareDigest),
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["openvino"] = "2026.3.0"
                    }));
        }

        internal static OptimizationExecutionPlan Create(
            OptimizationCandidate? candidate = null,
            OptimizationCapabilitySnapshot? snapshot = null)
        {
            OptimizationCandidate unproved = candidate ?? Candidate();
            OptimizationCapabilitySnapshot authority = snapshot ?? SnapshotFor(unproved);
            OptimizationCandidate chosen = OptimizationAdmissionTestFactory.Admit(
                unproved, authority, Workload(), Binding(),
                SupportLevel.DeclaredSupported);

            return OptimizationPlanIssuer.Issue(
                OptimizationPreferenceResolver.Resolve(
                    [chosen],
                    OptimizationPreferenceSelection.Manual(50))!,
                Payload(chosen),
                authority,
                Workload(),
                Binding(),
                modelLayerCount: 32,
                DateTimeOffset.UnixEpoch);
        }

        internal static OptimizationSelection Selection(
            OptimizationCandidate? candidate = null)
        {
            OptimizationCandidate unproved = candidate ?? Candidate();
            OptimizationCapabilitySnapshot snapshot = SnapshotFor(unproved);
            OptimizationCandidate admitted = OptimizationAdmissionTestFactory.Admit(
                unproved, snapshot, Workload(), Binding(),
                SupportLevel.DeclaredSupported);
            return OptimizationPreferenceResolver.Resolve(
                [admitted], OptimizationPreferenceSelection.Automatic())!;
        }
    }

    [TestMethod]
    public void PlanBindsJourneyAndConfiguration()
    {
        OptimizationExecutionPlan plan = OptimizationPlanTestData.Create();

        Assert.AreNotEqual(Guid.Empty, plan.OptimizationPlanId);
        Assert.AreEqual(
            OptimizationPlanTestData.ModelInspectionRunId, plan.ModelInspectionRunId);
        Assert.AreEqual(
            OptimizationPlanTestData.ProductHardwareRunId, plan.ProductHardwareRunId);
        StringAssert.Matches(plan.ConfigurationSha256, new("^[0-9a-f]{64}$"));
    }

    [TestMethod]
    public void EveryPlanGetsItsOwnIdentity()
    {
        // There is no amendment. A changed input produces a new plan the user
        // re-confirms, so two issues must never share an id.
        Assert.AreNotEqual(
            OptimizationPlanTestData.Create().OptimizationPlanId,
            OptimizationPlanTestData.Create().OptimizationPlanId);
    }

    [TestMethod]
    public void PlanCarriesEveryEstablishedSeamName()
    {
        // The seam names are not C1's to rename. An executor and the inspection
        // features have to be talking about the same fields.
        Type type = typeof(OptimizationJourneyBinding);

        foreach (string member in new[]
        {
            "ModelInspectionRunId",
            "ModelInspectionHandoffId",
            "ModelSha256",
            "ModelLengthBytes",
            "ProductHardwareRunId"
        })
        {
            Assert.IsNotNull(type.GetProperty(member), $"The binding lost {member}.");
        }
    }

    [TestMethod]
    public void ConfigurationDigestIsStableForTheSameConfiguration()
    {
        // The executor recomputes this on another machine and compares it
        // ordinally. Anything varying between runs would report drift on a plan
        // that had not changed.
        Assert.AreEqual(
            OptimizationPlanTestData.Create().ConfigurationSha256,
            OptimizationPlanTestData.Create().ConfigurationSha256);
    }

    [TestMethod]
    public void ConfigurationDigestChangesWithTheConfiguration()
    {
        // It covers the complete configuration, not merely the weight format.
        Assert.AreNotEqual(
            OptimizationPlanTestData.Create().ConfigurationSha256,
            OptimizationPlanTestData.Create(
                OptimizationPlanTestData.Candidate(OpenVinoWeightFormat.Int4))
                .ConfigurationSha256);
    }

    [TestMethod]
    public void ConfigurationDigestChangesWithContext()
    {
        // Two candidates differing only in context length are different
        // candidates, and the route descriptor deliberately does not carry
        // context - so this is the check that it is folded in exactly once.
        Assert.AreNotEqual(
            OptimizationPlanTestData.Create().ConfigurationSha256,
            OptimizationPlanTestData.Create(
                OptimizationPlanTestData.Candidate(context: 8192)).ConfigurationSha256);
    }

    [TestMethod]
    public void CapabilityDriftIsDetected()
    {
        OptimizationExecutionPlan plan = OptimizationPlanTestData.Create();

        Assert.IsTrue(plan.MatchesCapability(OptimizationPlanTestData.Snapshot()));
        Assert.IsFalse(plan.MatchesCapability(OptimizationPlanTestData.Snapshot(
            "4444444444444444444444444444444444444444444444444444444444444444")));
    }

    [TestMethod]
    public void SourceDriftIsDetectedByDigestAndLength()
    {
        // Both, because a length alone collides trivially and a digest alone
        // would accept a file truncated and re-padded to match.
        OptimizationExecutionPlan plan = OptimizationPlanTestData.Create();

        Assert.IsTrue(plan.MatchesSource(
            OptimizationPlanTestData.ModelDigest, 4 * Gibibyte));

        Assert.IsFalse(plan.MatchesSource(
            "5555555555555555555555555555555555555555555555555555555555555555",
            4 * Gibibyte));

        Assert.IsFalse(plan.MatchesSource(
            OptimizationPlanTestData.ModelDigest, 5 * Gibibyte));
    }

    [TestMethod]
    public void RouteMismatchBetweenCandidateAndEvidenceIsRefused()
    {
        // The two come from different places. A plan bound to evidence about a
        // different executor could not be verified by either of them.
        Assert.ThrowsExactly<ArgumentException>(() => OptimizationPlanIssuer.Issue(
            OptimizationPlanTestData.Selection(),
            OptimizationPlanTestData.Payload(OptimizationPlanTestData.Candidate()),
            OptimizationCapabilitySnapshot.ForGguf(
                "gguf-cap",
                OptimizationPlanTestData.CapabilityDigest,
                GgufCapabilityPayload.Create(
                    "b4321",
                    [
                        GgufAdmittedConfiguration.Create(
                            "g", CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
                            GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf
                                .GgufWeightFormat.Q4KM,
                            GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf
                                .GgufKvCacheFormat.F16,
                            GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf
                                .GpuOffloadLevel.None,
                            512, 32768, SupportLevel.DeclaredSupported, false)
                    ])),
            OptimizationPlanTestData.Workload(),
            OptimizationPlanTestData.Binding(),
            modelLayerCount: 32,
            DateTimeOffset.UnixEpoch));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("not-a-hash")]
    [DataRow("AAAA111111111111111111111111111111111111111111111111111111111111")]
    public void MalformedBindingDigestIsRefused(string digest)
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => OptimizationJourneyBinding.Create(
                "mi-run-1", "mi-handoff-1", digest, 4 * Gibibyte, "hw-run-1",
                OptimizationPlanTestData.HardwareDigest));
    }

    [TestMethod]
    public void ZeroModelLengthIsRefused()
    {
        // Zero matches an absent file as readily as the real one, so it binds
        // nothing.
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => OptimizationJourneyBinding.Create(
                "mi-run-1", "mi-handoff-1", OptimizationPlanTestData.ModelDigest,
                0, "hw-run-1", OptimizationPlanTestData.HardwareDigest));
    }

    [TestMethod]
    public void NonUtcTimestampIsRefused()
    {
        Assert.ThrowsExactly<ArgumentException>(() => OptimizationPlanIssuer.Issue(
            OptimizationPlanTestData.Selection(),
            OptimizationPlanTestData.Payload(OptimizationPlanTestData.Candidate()),
            OptimizationPlanTestData.Snapshot(),
            OptimizationPlanTestData.Workload(),
            OptimizationPlanTestData.Binding(),
            modelLayerCount: 32,
            new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.FromHours(2))));
    }

    [TestMethod]
    public void EveryTerminalStatusIsReachableAndDefined()
    {
        // A status nothing can produce is a state the destination page would
        // have to handle and never see, and one it cannot produce is a gap.
        OptimizationExecutionPlan plan = OptimizationPlanTestData.Create();

        OptimizationExecutionResult[] results =
        [
            OptimizationExecutionResult.Succeeded(
                plan, "out-1",
                "6666666666666666666666666666666666666666666666666666666666666666",
                4 * Gibibyte, sourceUnchanged: true, DateTimeOffset.UnixEpoch),
            OptimizationExecutionResult.Cancelled(
                plan, sourceUnchanged: true, DateTimeOffset.UnixEpoch),
            OptimizationExecutionResult.Failed(
                plan, OptimizationSupportCode.ValidationFailed,
                sourceUnchanged: true, DateTimeOffset.UnixEpoch),
            OptimizationExecutionResult.ReplanRequired(
                plan, OptimizationSupportCode.CapabilityDrift,
                sourceUnchanged: true, DateTimeOffset.UnixEpoch)
        ];

        foreach (OptimizationExecutionResult result in results)
        {
            Assert.AreNotEqual(OptimizationExecutionStatus.Unspecified, result.Status);
            Assert.IsTrue(Enum.IsDefined(result.Status));
            Assert.AreEqual(plan.OptimizationPlanId, result.OptimizationPlanId);
        }
    }

    [TestMethod]
    public void PersistentAndRuntimeOnlySuccessesAreDifferentStatuses()
    {
        // They promise different things. A destination offering to "save the
        // model" after a runtime-only run would describe a file that does not
        // exist.
        OptimizationExecutionResult persistent = OptimizationExecutionResult.Succeeded(
            OptimizationPlanTestData.Create(), "out-1",
            "6666666666666666666666666666666666666666666666666666666666666666",
            4 * Gibibyte, true, DateTimeOffset.UnixEpoch);

        OptimizationExecutionResult runtimeOnly = OptimizationExecutionResult.Succeeded(
            OptimizationPlanTestData.Create(
                OptimizationPlanTestData.Candidate(OpenVinoWeightFormat.Original)),
            "profile-1",
            "6666666666666666666666666666666666666666666666666666666666666666",
            0, true, DateTimeOffset.UnixEpoch);

        Assert.AreEqual(
            OptimizationExecutionStatus.SucceededPersistent, persistent.Status);
        Assert.IsTrue(persistent.ProducedPersistentArtifact);

        Assert.AreEqual(
            OptimizationExecutionStatus.SucceededRuntimeProfile, runtimeOnly.Status);
        Assert.IsFalse(runtimeOnly.ProducedPersistentArtifact);
    }

    [TestMethod]
    public void OnlySuccessfulResultsMayReachTheDestination()
    {
        OptimizationExecutionPlan plan = OptimizationPlanTestData.Create();

        Assert.IsFalse(
            OptimizationExecutionResult.Cancelled(
                plan, sourceUnchanged: true, DateTimeOffset.UnixEpoch)
                .IsSuccessful);
        Assert.IsFalse(
            OptimizationExecutionResult.Failed(
                plan, OptimizationSupportCode.SmokeTestFailed,
                sourceUnchanged: true, DateTimeOffset.UnixEpoch)
                .IsSuccessful);
        Assert.IsFalse(
            OptimizationExecutionResult.ReplanRequired(
                plan, OptimizationSupportCode.CapabilityDrift,
                sourceUnchanged: true, DateTimeOffset.UnixEpoch)
                .IsSuccessful);
    }

    [TestMethod]
    public void FailedAndCancelledRunsPublishNothing()
    {
        // A partial artifact recorded here is one the destination could offer
        // to save.
        OptimizationExecutionPlan plan = OptimizationPlanTestData.Create();

        foreach (OptimizationExecutionResult result in new[]
        {
            OptimizationExecutionResult.Cancelled(
                plan, sourceUnchanged: true, DateTimeOffset.UnixEpoch),
            OptimizationExecutionResult.Failed(
                plan, OptimizationSupportCode.ConversionFailed,
                sourceUnchanged: true, DateTimeOffset.UnixEpoch),
            OptimizationExecutionResult.ReplanRequired(
                plan, OptimizationSupportCode.SourceIdentityMismatch,
                sourceUnchanged: true, DateTimeOffset.UnixEpoch)
        })
        {
            Assert.IsNull(result.OutputIdentity);
            Assert.IsNull(result.OutputManifestSha256);
            Assert.AreEqual(0UL, result.OutputSizeBytes);
        }
    }

    [TestMethod]
    public void NonSuccessResultsPreserveTheObservedSourceIntegrity()
    {
        OptimizationExecutionPlan plan = OptimizationPlanTestData.Create();
        OptimizationExecutionResult[] results =
        [
            OptimizationExecutionResult.Cancelled(
                plan, sourceUnchanged: false, DateTimeOffset.UnixEpoch),
            OptimizationExecutionResult.Failed(
                plan, OptimizationSupportCode.ValidationFailed,
                sourceUnchanged: false, DateTimeOffset.UnixEpoch),
            OptimizationExecutionResult.ReplanRequired(
                plan, OptimizationSupportCode.SourceIdentityMismatch,
                sourceUnchanged: false, DateTimeOffset.UnixEpoch)
        ];

        Assert.IsTrue(results.All(result => !result.SourceUnchanged));
    }

    [TestMethod]
    public void SuccessWithoutASourceAttestationIsRefused()
    {
        // The original surviving is the promise. A result that cannot evidence
        // it has not succeeded, whatever it produced.
        Assert.ThrowsExactly<ArgumentException>(
            () => OptimizationExecutionResult.Succeeded(
                OptimizationPlanTestData.Create(), "out-1",
                "6666666666666666666666666666666666666666666666666666666666666666",
                4 * Gibibyte, sourceUnchanged: false, DateTimeOffset.UnixEpoch));
    }

    [TestMethod]
    public void ReplanRequiredRefusesAnEnvironmentalCause()
    {
        // Replan means a planning input changed. An environmental problem that
        // left the plan valid is a Failed result, and confusing them sends the
        // user to redo the wrong thing.
        Assert.ThrowsExactly<ArgumentException>(
            () => OptimizationExecutionResult.ReplanRequired(
                OptimizationPlanTestData.Create(),
                OptimizationSupportCode.InsufficientDiskSpace,
                sourceUnchanged: false,
                DateTimeOffset.UnixEpoch));
    }

    [TestMethod]
    public void FailureMustNameItself()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => OptimizationExecutionResult.Failed(
                OptimizationPlanTestData.Create(),
                OptimizationSupportCode.None,
                sourceUnchanged: false,
                DateTimeOffset.UnixEpoch));
    }

    [TestMethod]
    public void SupportCodesCarryNoFreeText()
    {
        // Bounded and privacy-safe by construction. A support record built from
        // these can be shown to a user and shipped in a log.
        Assert.IsTrue(typeof(OptimizationSupportCode).IsEnum);

        Assert.AreEqual(
            typeof(OptimizationSupportCode),
            typeof(OptimizationExecutionResult)
                .GetProperty(nameof(OptimizationExecutionResult.SupportCode))!
                .PropertyType);
    }

    [TestMethod]
    public void PlanStatesWhetherItWritesAModel()
    {
        // The one fact the confirmation surface must not get wrong.
        Assert.IsTrue(OptimizationPlanTestData.Create().ProducesPersistentArtifact);

        Assert.IsFalse(OptimizationPlanTestData.Create(
            OptimizationPlanTestData.Candidate(OpenVinoWeightFormat.Original))
            .ProducesPersistentArtifact);
    }

    [TestMethod]
    public void PlanDoesNotSerializeTheLegacyModeVocabulary()
    {
        // New plans never carry Quality, Balanced or Efficiency as a mode. The
        // product replaced that vocabulary, and a plan reintroducing it would
        // put retired words in front of a user.
        string canonical = string.Join(
            " ",
            typeof(OptimizationExecutionPlan)
                .GetProperties()
                .Select(property => property.PropertyType.Name));

        Assert.IsFalse(
            canonical.Contains("CompatibilityMode", StringComparison.Ordinal),
            "A plan exposes the retired four-mode vocabulary.");
    }
}
