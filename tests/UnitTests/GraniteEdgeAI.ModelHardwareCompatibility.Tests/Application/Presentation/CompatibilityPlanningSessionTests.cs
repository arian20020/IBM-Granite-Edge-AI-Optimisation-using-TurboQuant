using System.Reflection;

using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Planning;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using GraniteEdgeAI.ModelHardwareCompatibility.Tests.Invariants;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Presentation;

[TestClass]
public sealed class CompatibilityPlanningSessionTests
{
    private const ulong GiB = 1024UL * 1024 * 1024;
    private const string Digest =
        "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

    [TestMethod]
    public void Issue_UsesExactRetainedCandidateAndCreatesANewPlanIdentity()
    {
        Fixture fixture = CreateFixture();
        CompatibilityPlanningSession session = CreateSession(fixture);
        var composer = new OpenVinoComposer();
        OptimizationIssuanceAuthority authority =
            OptimizationHardwareAuthorityTestData.Issuance(fixture.Candidate);

        OptimizationExecutionPlan first = session.Issue(
            OptimizationPreferenceSelection.Manual(60), composer, authority,
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));
        OptimizationExecutionPlan second = session.Issue(
            OptimizationPreferenceSelection.Manual(60), composer, authority,
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        Assert.AreEqual(3, first.ContractVersion);
        Assert.AreEqual(OptimizationRoute.OpenVino, first.Route);
        Assert.AreEqual(fixture.Candidate.EvidenceId, first.Candidate.EvidenceId);
        Assert.AreNotEqual(first.OptimizationPlanId, second.OptimizationPlanId);
    }

    [TestMethod]
    public void Issue_RejectsWrongRouteComposerBeforeItCanCompose()
    {
        Fixture fixture = CreateFixture();
        CompatibilityPlanningSession session = CreateSession(fixture);

        Assert.ThrowsExactly<ArgumentException>(() => session.Issue(
            OptimizationPreferenceSelection.Automatic(),
            new WrongRouteComposer(),
            OptimizationHardwareAuthorityTestData.Issuance(fixture.Candidate),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch)));
    }

    [TestMethod]
    public void Issue_RechecksCurrentHardwareFreshness()
    {
        Fixture fixture = CreateFixture();
        CompatibilityPlanningSession session = CreateSession(fixture);

        Assert.ThrowsExactly<ArgumentException>(() => session.Issue(
            OptimizationPreferenceSelection.Automatic(),
            new OpenVinoComposer(),
            OptimizationHardwareAuthorityTestData.Issuance(fixture.Candidate),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch.AddSeconds(31))));
    }

    [TestMethod]
    public void PublicPlanningSurfaceContainsNoPathLikeMember()
    {
        Type[] types =
        [
            typeof(CompatibilityPlanningSession),
            typeof(IOptimizationExecutionPayloadComposer),
            typeof(CompatibilityEvaluation),
            typeof(CurrentCompatibleConfiguration)
        ];
        string[] forbidden = ["Path", "File", "Folder", "Directory", "Uri"];

        foreach (Type type in types)
        {
            IEnumerable<string> names = type.GetProperties(
                    BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name)
                .Concat(type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .SelectMany(method => method.GetParameters())
                    .Select(parameter => parameter.Name ?? string.Empty));
            foreach (string name in names)
            {
                Assert.IsFalse(
                    forbidden.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase)),
                    $"{type.Name}.{name} exposes a path-like public member.");
            }
        }
    }

    private static CompatibilityPlanningSession CreateSession(Fixture fixture) =>
        CompatibilityPlanningSession.Create(
            OptimizationRoute.OpenVino,
            [fixture.Candidate],
            fixture.Snapshot,
            fixture.Workload,
            fixture.Binding,
            modelLayerCount: 32)!;

    private static Fixture CreateFixture()
    {
        OpenVinoRouteConfiguration configuration = OpenVinoRouteConfiguration.Create(
            OpenVinoWeightFormat.Original,
            OpenVinoKvCacheFormat.U8,
            DeviceRouteId.Cpu,
            OpenVinoPerformanceHint.Latency,
            OpenVinoCompiledCachePolicy.Disabled,
            1);
        OptimizationCandidate candidate = OptimizationCandidate.Create(
            configuration,
            OptimizationCandidateMetrics.Create(
                EvidenceGrade.Estimated,
                OptimizationAssessment.Good,
                OptimizationAssessment.Good,
                OptimizationAssessment.Good,
                4096,
                4 * GiB,
                16 * GiB,
                12 * GiB,
                0,
                0,
                requiresPersistentChange: false,
                availableDiskBytes: 500 * GiB),
            "ov-current",
            isExperimental: false);
        OpenVinoBuildIdentity build = OpenVinoBuildIdentity.Create(
            "2026.1.0", "2026.1.0", "2026.1.0", Digest);
        IReadOnlyDictionary<string, string> versions =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["openvino"] = "2026.1.0"
            };
        OptimizationCapabilitySnapshot snapshot =
            OptimizationCapabilitySnapshot.ForOpenVino(
                "ov-cap",
                Digest,
                OpenVinoCapabilityPayload.Create(
                    "2026.1.0",
                    [OpenVinoAdmittedConfiguration.Create(
                        candidate.EvidenceId,
                        DeviceRouteId.Cpu,
                        OpenVinoWeightFormat.Original,
                        OpenVinoKvCacheFormat.U8,
                        OpenVinoPerformanceHint.Latency,
                        OpenVinoCompiledCachePolicy.Disabled,
                        1,
                        512,
                        8192,
                        SupportLevel.DeclaredSupported,
                        requiresEvidence: false)],
                    [OpenVinoExecutionAuthority.Create(
                        candidate.EvidenceId,
                        "ov-current-config",
                        OpenVinoWeightPrecision.Fp16,
                        build,
                        versions,
                        compiledCacheIsDisposable: true)]));
        OptimizationWorkload workload = OptimizationWorkload.Create(
            "chat", 512, OptimizationAssessment.Poor,
            [ContextTokenCount.FromTokens(4096)]);
        OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
            "model-run", "handoff", Digest, 3 * GiB, "hardware-run", Digest);
        OptimizationCandidate admitted = OptimizationAdmissionTestFactory.Admit(
            candidate, snapshot, workload, binding,
            SupportLevel.DeclaredSupported);
        return new Fixture(admitted, snapshot, workload, binding);
    }

    private sealed record Fixture(
        OptimizationCandidate Candidate,
        OptimizationCapabilitySnapshot Snapshot,
        OptimizationWorkload Workload,
        OptimizationJourneyBinding Binding);

    private sealed class OpenVinoComposer : IOptimizationExecutionPayloadComposer
    {
        public OptimizationRoute Route => OptimizationRoute.OpenVino;

        public OptimizationExecutionPayload Compose(OptimizationCandidate candidate) =>
            OptimizationExecutionPayload.ForOpenVino(
                OpenVinoExecutionPayload.Create(
                    "ov-current-config",
                    "CPU",
                    "Standard candidate",
                    candidate.EvidenceId,
                    OpenVinoWeightPrecision.Fp16,
                    OpenVinoWeightPrecision.Fp16,
                    OpenVinoKvCachePrecision.U8,
                    compiledCacheEnabled: false,
                    compiledCacheIsDisposable: true,
                    compiledCacheIsModelArtifact: false,
                    createsCompletePackage: false,
                    OpenVinoBuildIdentity.Create(
                        "2026.1.0", "2026.1.0", "2026.1.0", Digest),
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["openvino"] = "2026.1.0"
                    }));
    }

    private sealed class WrongRouteComposer : IOptimizationExecutionPayloadComposer
    {
        public OptimizationRoute Route => OptimizationRoute.Gguf;
        public OptimizationExecutionPayload Compose(OptimizationCandidate candidate) =>
            throw new AssertFailedException("Wrong-route composer must not run.");
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
