using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using System.Reflection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Invariants;

[TestClass]
public sealed class OptimizationExecutionContractV3Tests
{
    private const ulong Gibibyte = 1024UL * 1024 * 1024;
    private const string Digest64 =
        "1111111111111111111111111111111111111111111111111111111111111111";
    private const string OtherDigest64 =
        "2222222222222222222222222222222222222222222222222222222222222222";
    private const string Commit40 = "0123456789abcdef0123456789abcdef01234567";

    [TestMethod]
    public void UndefinedSupportCannotAcquireAProofOrIssueAfterAdmissionMutation()
    {
        OpenVinoAdmittedConfiguration admitted = OpenVinoAdmittedConfiguration.Create(
            "ov-standard", DeviceRouteId.Cpu, OpenVinoWeightFormat.Int8,
            OpenVinoKvCacheFormat.U8, OpenVinoPerformanceHint.Latency,
            OpenVinoCompiledCachePolicy.Disabled, 1, 512, 32768,
            SupportLevel.DeclaredSupported, false);
        OpenVinoExecutionAuthority authority = OpenVinoExecutionAuthority.Create(
            admitted.EvidenceId, "openvino.standard.cpu.int8.u8.v3",
            OpenVinoWeightPrecision.Fp16, Build(), Versions(),
            compiledCacheIsDisposable: true);
        OptimizationCapabilitySnapshot snapshot =
            OptimizationCapabilitySnapshot.ForOpenVino(
                "ov-standard", Digest64,
                OpenVinoCapabilityPayload.Create(
                    Build().RuntimeBuild, [admitted], [authority]));
        OptimizationWorkload workload = OptimizationWorkload.Create(
            "chat", 512, OptimizationAssessment.Poor,
            [ContextTokenCount.FromTokens(4096)]);
        OptimizationJourneyBinding binding = Binding();
        CrossRouteGenerationResult generated = CrossRouteCandidateGenerator.Generate(
            snapshot,
            InspectedModelFacts.Create(
                ByteCount.FromBytes(3 * Gibibyte), 32, 4096, 32, 8, 8192, 15, 2),
            workload, binding, ByteCount.FromBytes(32 * Gibibyte),
            ByteCount.FromBytes(500 * Gibibyte), EstimatorPolicy.ProvisionalV1(),
            new HashSet<string>(),
            OptimizationHardwareAuthorityTestData.AllEstablished());
        OptimizationSelection valid = OptimizationPreferenceResolver.Resolve(
            generated.Candidates, OptimizationPreferenceSelection.Automatic())!;
        OptimizationExecutionPayload payload = OptimizationExecutionPayload.ForOpenVino(
            OpenVinoExecutionPayload.Create(
                authority.ConfigurationId, "CPU", "Standard candidate",
                admitted.EvidenceId, OpenVinoWeightPrecision.Fp16,
                OpenVinoWeightPrecision.EightBit, OpenVinoKvCachePrecision.U8,
                false, true, false, true, Build(), Versions()));
        _ = OptimizationPlanIssuer.Issue(
            valid, payload, snapshot, workload, binding, 32,
            DateTimeOffset.UnixEpoch);

        const SupportLevel invalid = (SupportLevel)3;
        typeof(OpenVinoAdmittedConfiguration).GetField(
            "<Level>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(admitted, invalid);

        Assert.ThrowsExactly<ArgumentException>(() =>
        {
            OptimizationAdmissionProof forged = OptimizationAdmissionProof.Create(
                snapshot, workload, binding, valid.Candidate, invalid,
                requiresEvidence: false, new HashSet<string>());
            OptimizationCandidate candidate = OptimizationCandidate.AttachAdmissionProof(
                valid.Candidate, forged);
            OptimizationSelection selection = OptimizationPreferenceResolver.Resolve(
                [candidate], OptimizationPreferenceSelection.Automatic())!;
            _ = OptimizationPlanIssuer.Issue(
                selection, payload, snapshot, workload, binding, 32,
                DateTimeOffset.UnixEpoch);
        });

        OptimizationAdmissionProof proof = valid.Candidate.AdmissionProof!;
        typeof(OptimizationAdmissionProof).GetField(
            "<SupportLevel>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(proof, invalid);
        string currentAuthorityDigest = (string)typeof(OptimizationAdmissionProof)
            .GetMethod(
                "DigestRouteExecutionAuthority",
                BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [snapshot, admitted.EvidenceId])!;
        typeof(OptimizationAdmissionProof).GetField(
            "<RouteExecutionAuthoritySha256>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(proof, currentAuthorityDigest);

        Assert.IsNull(OptimizationPreferenceResolver.Resolve(
            [valid.Candidate], OptimizationPreferenceSelection.Automatic()));

        Assert.ThrowsExactly<ArgumentException>(() => OptimizationPlanIssuer.Issue(
            valid, payload, snapshot, workload, binding, 32,
            DateTimeOffset.UnixEpoch));
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(int.MaxValue)]
    public void GgufPayloadRejectsUndefinedPersistentTargetWeightFormat(int raw)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            GgufExecutionPayload.Create(
                "runtime", Commit40, GgufRuntimeBackend.Cpu, "CPU", 4096,
                GgufCacheType.F16, GgufCacheType.F16, 0, false, 4, 128,
                "Estimated", "profile", 256, (GgufWeightFormat)raw,
                GgufQuantiser(Digest64)));
    }

    [TestMethod]
    [DataRow(OpenVinoKvCacheFormat.TurboQuantTbq4, OpenVinoKvCachePrecision.Tbq4)]
    [DataRow(OpenVinoKvCacheFormat.TurboQuantTbq3, OpenVinoKvCachePrecision.Tbq3)]
    public void OptedInTurboQuantCandidateIssuesAnExactV3Plan(
        OpenVinoKvCacheFormat cache,
        OpenVinoKvCachePrecision precision)
    {
        (OptimizationSelection selection, OptimizationCapabilitySnapshot snapshot,
            OptimizationWorkload workload) = SelectedTurboCandidate(cache);
        TurboQuantBuildIdentity turboBuild = TurboBuild();

        OptimizationExecutionPlan plan = OptimizationPlanIssuer.Issue(
            selection,
            OptimizationExecutionPayload.ForOpenVino(Payload(
                OpenVinoKvCacheAlgorithm.TurboQuant,
                precision,
                turboBuild)),
            snapshot,
            workload,
            Binding(),
            modelLayerCount: 32,
            DateTimeOffset.UnixEpoch);

        Assert.AreEqual(3, plan.ContractVersion);
        Assert.IsFalse(plan.IsExecutableBy(2));
        Assert.AreEqual(OpenVinoKvCacheAlgorithm.TurboQuant,
            plan.ExecutionPayload.OpenVino!.KvCacheAlgorithm);
        Assert.AreEqual(precision, plan.ExecutionPayload.OpenVino.KvCachePrecision);
        Assert.AreSame(turboBuild, plan.ExecutionPayload.OpenVino.TurboQuantBuild);
    }

    [TestMethod]
    public void OpenVinoPlanRejectsRuntimeMaturityPackageAndEvidenceSubstitution()
    {
        (OptimizationSelection selection, OptimizationCapabilitySnapshot snapshot,
            OptimizationWorkload workload) = SelectedTurboCandidate(
                OpenVinoKvCacheFormat.TurboQuantTbq4);

        foreach (OpenVinoExecutionPayload changed in new[]
        {
            Payload(OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq4, TurboBuild(), runtimeBuild: "changed"),
            Payload(OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq4, TurboBuild(), maturity: "Standard candidate"),
            Payload(OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq4, TurboBuild(), createsCompletePackage: false),
            Payload(OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq4, TurboBuild(), evidenceId: "other-evidence")
        })
        {
            Assert.ThrowsExactly<ArgumentException>(() => OptimizationPlanIssuer.Issue(
                selection, OptimizationExecutionPayload.ForOpenVino(changed),
                snapshot, workload, Binding(), 32, DateTimeOffset.UnixEpoch));
        }
    }

    [TestMethod]
    public void OpenVinoPlanRejectsUnboundConfigurationSourceBuildAndOptimizerSubstitution()
    {
        (OptimizationSelection selection, OptimizationCapabilitySnapshot snapshot,
            OptimizationWorkload workload) = SelectedTurboCandidate(
                OpenVinoKvCacheFormat.TurboQuantTbq4);
        OptimizationExecutionPayload Valid(OpenVinoExecutionPayload payload) =>
            OptimizationExecutionPayload.ForOpenVino(payload);

        _ = OptimizationPlanIssuer.Issue(
            selection,
            Valid(Payload(
                OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq4,
                TurboBuild())),
            snapshot, workload, Binding(), 32, DateTimeOffset.UnixEpoch);

        foreach (OpenVinoExecutionPayload substituted in new[]
        {
            Payload(OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq4, TurboBuild(),
                configurationId: "other-configuration"),
            Payload(OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq4, TurboBuild(),
                sourceWeightPrecision: OpenVinoWeightPrecision.EightBit),
            Payload(OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq4, TurboBuild(),
                buildIdentity: OpenVinoBuildIdentity.Create(
                    "other-runtime", "2026.3.0.0", "2026.3.0", Digest64)),
            Payload(OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq4, TurboBuild(),
                buildIdentity: OpenVinoBuildIdentity.Create(
                    "2026.3.0", "other-genai", "2026.3.0", Digest64)),
            Payload(OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq4, TurboBuild(),
                buildIdentity: OpenVinoBuildIdentity.Create(
                    "2026.3.0", "2026.3.0.0", "other-tokenizers", Digest64)),
            Payload(OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq4, TurboBuild(),
                buildIdentity: OpenVinoBuildIdentity.Create(
                    "2026.3.0", "2026.3.0.0", "2026.3.0", OtherDigest64)),
            Payload(OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq4, TurboBuild(),
                optimizerVersions: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["nncf"] = "other-nncf",
                    ["openvino"] = "2026.3.0"
                }),
            Payload(OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq4, TurboBuild(),
                optimizerVersions: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["nncf"] = "3.3.0",
                    ["openvino"] = "2026.3.0",
                    ["added"] = "1"
                }),
            Payload(OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq4, TurboBuild(),
                optimizerVersions: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["openvino"] = "2026.3.0"
                }),
            Payload(OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq4,
                TurboQuantBuildIdentity.Create(
                    "89abcdef0123456789abcdef0123456789abcdef",
                    Commit40, Digest64, OtherDigest64)),
            Payload(OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq4,
                TurboQuantBuildIdentity.Create(
                    Commit40,
                    "89abcdef0123456789abcdef0123456789abcdef",
                    Digest64, OtherDigest64)),
            Payload(OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq4,
                TurboQuantBuildIdentity.Create(
                    Commit40, Commit40, OtherDigest64, OtherDigest64)),
            Payload(OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq4,
                TurboQuantBuildIdentity.Create(
                    Commit40, Commit40, Digest64, Digest64)),
            Payload(OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq4, TurboBuild(),
                compiledCacheIsDisposable: false)
        })
        {
            Assert.ThrowsExactly<ArgumentException>(() => OptimizationPlanIssuer.Issue(
                selection, Valid(substituted), snapshot, workload, Binding(), 32,
                DateTimeOffset.UnixEpoch));

            OpenVinoAdmittedConfiguration admitted = snapshot.OpenVino!.Admitted.Single();
            OptimizationCapabilitySnapshot replay =
                OptimizationCapabilitySnapshot.ForOpenVino(
                    snapshot.SnapshotId,
                    snapshot.CapabilitySnapshotSha256,
                    OpenVinoCapabilityPayload.Create(
                        substituted.BuildIdentity.RuntimeBuild,
                        [admitted],
                        [OpenVinoExecutionAuthority.Create(
                            substituted.EvidenceId,
                            substituted.ConfigurationId,
                            substituted.SourceWeightPrecision,
                            substituted.BuildIdentity,
                            substituted.OptimizerVersions,
                            substituted.CompiledCacheIsDisposable,
                            substituted.TurboQuantBuild)]));
            Assert.ThrowsExactly<ArgumentException>(() => OptimizationPlanIssuer.Issue(
                selection, Valid(substituted), replay, workload, Binding(), 32,
                DateTimeOffset.UnixEpoch));
        }
    }

    [TestMethod]
    public void IssuedOpenVinoOptimizerMapCannotBeMutatedThroughADowncast()
    {
        (OptimizationSelection selection, _, _) = SelectedTurboCandidate(
            OpenVinoKvCacheFormat.TurboQuantTbq4);
        OptimizationExecutionPayload payload = OptimizationExecutionPayload.ForOpenVino(
            Payload(OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq4, TurboBuild()));
        string before = OptimizationCanonicalizer.ConfigurationSha256(
            selection.Candidate, payload, 3);

        Assert.ThrowsExactly<NotSupportedException>(() =>
            ((IDictionary<string, string>)payload.OpenVino!.OptimizerVersions)
                ["injected"] = "version");

        Assert.AreEqual(before, OptimizationCanonicalizer.ConfigurationSha256(
            selection.Candidate, payload, 3));
    }

    [TestMethod]
    public void OpenVinoPayloadRejectsOptimizerMapWhoseCountDisagreesWithEnumeration()
    {
        EmptyEnumeratingDictionary inconsistent = new();
        Assert.ThrowsExactly<ArgumentException>(() => Payload(
            OpenVinoKvCacheAlgorithm.TurboQuant,
            OpenVinoKvCachePrecision.Tbq4,
            TurboBuild(),
            optimizerVersions: inconsistent));
        Assert.ThrowsExactly<ArgumentException>(() =>
            OpenVinoExecutionAuthority.Create(
                "evidence", "configuration", OpenVinoWeightPrecision.Fp16,
                Build(), inconsistent, compiledCacheIsDisposable: true));
        Assert.ThrowsExactly<ArgumentException>(() => GgufRuntimeAuthority.Create(
            "runtime", Commit40, new EmptyEnumeratingProfileList()));
    }

    [TestMethod]
    public void EveryExecutionPayloadMemberNamesItsExactAuthoritySource()
    {
        IReadOnlyDictionary<string, string> gguf =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [nameof(GgufExecutionPayload.RuntimeBuildId)] = "runtime authority",
                [nameof(GgufExecutionPayload.RuntimeSourceCommit)] = "runtime authority",
                [nameof(GgufExecutionPayload.Backend)] = "admitted route configuration",
                [nameof(GgufExecutionPayload.DeviceId)] = "admitted device mapping",
                [nameof(GgufExecutionPayload.ContextSize)] = "candidate context",
                [nameof(GgufExecutionPayload.KeyCacheType)] = "admitted cache configuration",
                [nameof(GgufExecutionPayload.ValueCacheType)] = "admitted cache configuration",
                [nameof(GgufExecutionPayload.GpuLayerCount)] = "offload policy and model layers",
                [nameof(GgufExecutionPayload.FlashAttention)] = "profile authority",
                [nameof(GgufExecutionPayload.ThreadCount)] = "profile authority",
                [nameof(GgufExecutionPayload.BatchSize)] = "profile authority",
                [nameof(GgufExecutionPayload.EvidenceGrade)] = "profile authority",
                [nameof(GgufExecutionPayload.ProfileId)] = "profile authority",
                [nameof(GgufExecutionPayload.MaximumGeneratedTokens)] = "profile authority",
                [nameof(GgufExecutionPayload.PersistentTargetWeightFormat)] = "candidate configuration",
                [nameof(GgufExecutionPayload.Quantiser)] = "capability quantiser authority",
                [nameof(GgufExecutionPayload.ConversionSource)] = "source journey binding",
                [nameof(GgufExecutionPayload.RequantisationPolicy)] = "capability policy authority",
                [nameof(GgufExecutionPayload.RequiresPersistentConversion)] = "derived invariant"
            };
        IReadOnlyDictionary<string, string> openVino =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [nameof(OpenVinoExecutionPayload.ConfigurationId)] = "execution authority",
                [nameof(OpenVinoExecutionPayload.Device)] = "admitted device mapping",
                [nameof(OpenVinoExecutionPayload.Maturity)] = "admitted support level",
                [nameof(OpenVinoExecutionPayload.EvidenceId)] = "admitted evidence",
                [nameof(OpenVinoExecutionPayload.SourceWeightPrecision)] = "execution authority",
                [nameof(OpenVinoExecutionPayload.TargetWeightPrecision)] = "candidate configuration",
                [nameof(OpenVinoExecutionPayload.KvCacheAlgorithm)] = "candidate cache family",
                [nameof(OpenVinoExecutionPayload.KvCachePrecision)] = "candidate cache precision",
                [nameof(OpenVinoExecutionPayload.CompiledCacheEnabled)] = "candidate configuration",
                [nameof(OpenVinoExecutionPayload.CompiledCacheIsDisposable)] = "execution authority",
                [nameof(OpenVinoExecutionPayload.CompiledCacheIsModelArtifact)] = "constructor invariant",
                [nameof(OpenVinoExecutionPayload.CreatesCompletePackage)] = "candidate persistence",
                [nameof(OpenVinoExecutionPayload.BuildIdentity)] = "execution authority",
                [nameof(OpenVinoExecutionPayload.OptimizerVersions)] = "execution authority",
                [nameof(OpenVinoExecutionPayload.TurboQuantBuild)] = "execution authority",
                [nameof(OpenVinoExecutionPayload.RequiresPersistentConversion)] = "derived invariant"
            };

        CollectionAssert.AreEquivalent(
            typeof(GgufExecutionPayload).GetProperties(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(property => property.Name).ToArray(),
            gguf.Keys.ToArray());
        CollectionAssert.AreEquivalent(
            typeof(OpenVinoExecutionPayload).GetProperties(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(property => property.Name).ToArray(),
            openVino.Keys.ToArray());
        Assert.IsTrue(gguf.Values.Concat(openVino.Values).All(value => value.Length > 0));
    }

    [TestMethod]
    [DataRow(OpenVinoKvCachePrecision.Tbq4)]
    [DataRow(OpenVinoKvCachePrecision.Tbq3)]
    public void TurboQuantPrecisionRequiresPinnedBuildIdentity(
        OpenVinoKvCachePrecision precision)
    {
        Assert.ThrowsExactly<ArgumentException>(() => Payload(
            OpenVinoKvCacheAlgorithm.TurboQuant,
            precision,
            turboBuild: null));
    }

    [TestMethod]
    public void ReleasedCacheForbidsTurboQuantBuildIdentity()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Payload(
            OpenVinoKvCacheAlgorithm.Released,
            OpenVinoKvCachePrecision.U4,
            TurboBuild()));
    }

    [TestMethod]
    public void CacheAlgorithmAndPrecisionMustDescribeTheSameFamily()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Payload(
            OpenVinoKvCacheAlgorithm.Released,
            OpenVinoKvCachePrecision.Tbq4,
            TurboBuild()));

        Assert.ThrowsExactly<ArgumentException>(() => Payload(
            OpenVinoKvCacheAlgorithm.TurboQuant,
            OpenVinoKvCachePrecision.U4,
            TurboBuild()));
    }

    [TestMethod]
    public void IssuerRejectsAnyTurboQuantCacheDisagreement()
    {
        (OptimizationSelection selection, OptimizationCapabilitySnapshot snapshot,
            OptimizationWorkload workload) =
            SelectedTurboCandidate(OpenVinoKvCacheFormat.TurboQuantTbq4);

        Assert.ThrowsExactly<ArgumentException>(() => OptimizationPlanIssuer.Issue(
            selection,
            OptimizationExecutionPayload.ForOpenVino(Payload(
                OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq3,
                TurboBuild())),
            snapshot,
            workload,
            Binding(),
            32,
            DateTimeOffset.UnixEpoch));
    }

    [TestMethod]
    public void V3CanonicalOrderBindsAlgorithmPrecisionAndTurboQuantIdentity()
    {
        (OptimizationSelection selection, _, _) =
            SelectedTurboCandidate(OpenVinoKvCacheFormat.TurboQuantTbq4);
        OptimizationExecutionPayload payload = OptimizationExecutionPayload.ForOpenVino(
            Payload(
                OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq4,
                TurboBuild()));

        string canonical = OptimizationCanonicalizer.Canonicalize(
            selection.Candidate, payload, contractVersion: 3);

        int algorithm = canonical.IndexOf("ov.kvCacheAlgorithm", StringComparison.Ordinal);
        int precision = canonical.IndexOf("ov.kvCachePrecision", StringComparison.Ordinal);
        int identity = canonical.IndexOf("ov.turboQuant.sourceCommit", StringComparison.Ordinal);

        Assert.IsTrue(algorithm >= 0);
        Assert.IsTrue(algorithm < precision);
        Assert.IsTrue(precision < identity);
    }

    [TestMethod]
    public void TurboQuantTbq4V3CanonicalVectorRemainsFrozen()
    {
        OptimizationCandidate candidate = SelectedTurboCandidate(
            OpenVinoKvCacheFormat.TurboQuantTbq4).Selection.Candidate;
        OptimizationExecutionPayload payload = OptimizationExecutionPayload.ForOpenVino(
            Payload(
                OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq4,
                TurboBuild()));

        Assert.AreEqual(
            "v=1:3|route=1:2|config=79:openvino|w=Int4|kv=TurboQuantTbq4|dev=Cpu|hint=Latency|cache=Disabled|streams=1|ctx=4:4096|persistent=1:1|evidence=17:ov-turbo-evidence|experimental=1:1|availableDiskBytes=12:536870912000|admission.snapshotId=6:ov-cap|admission.capabilitySha256=64:1111111111111111111111111111111111111111111111111111111111111111|admission.workloadId=4:chat|admission.workloadSha256=64:256a9ed432045ddd99b0ed1c14865d1a811c03578515413fcea438b63e75ea5b|admission.journeySha256=64:79b21af287341b1ed1546722969e12b4785ce5051bc9ae21ec243b2906a8afd4|admission.routeExecutionAuthoritySha256=64:371407f0173992351669b9a1f1addb851297bbb8a65cdf8f43b91f87114353f9|admission.configuration=79:openvino|w=Int4|kv=TurboQuantTbq4|dev=Cpu|hint=Latency|cache=Disabled|streams=1|admission.evidenceId=17:ov-turbo-evidence|admission.supportLevel=1:2|admission.requiresEvidence=1:1|admission.optInEvidenceId=17:ov-turbo-evidence|admission.experimental=1:1|admission.conversionProvenance=1:0|admission.notice=1:0|admission.evidenceGrade=1:1|admission.quality=1:2|admission.performance=1:2|admission.stability=1:2|admission.contextTokens=4:4096|admission.predictedPeakBytes=10:1762111488|admission.safeBudgetBytes=11:34359738368|admission.headroomBytes=11:32597626880|admission.dedicatedEstablished=1:0|admission.dedicatedRequiredBytes=4:none|admission.dedicatedSafeBudgetBytes=4:none|admission.dedicatedHeadroomBytes=4:none|admission.workingStoragePhasePeakBytes=10:1042368922|admission.outputDiskBytes=10:1042368922|admission.diskObligationBytes=10:1042368922|admission.availableDiskBytes=12:536870912000|admission.persistent=1:1|ov.configurationId=39:openvino.experimental.cpu.int4.turbo.v3|ov.device=3:CPU|ov.maturity=22:Experimental candidate|ov.evidenceId=17:ov-turbo-evidence|ov.sourceWeightPrecision=1:0|ov.targetWeightPrecision=1:2|ov.kvCacheAlgorithm=1:2|ov.kvCachePrecision=1:5|ov.compiledCacheEnabled=1:0|ov.compiledCacheIsDisposable=1:1|ov.compiledCacheIsModelArtifact=1:0|ov.createsCompletePackage=1:1|ov.build.runtimeBuild=8:2026.3.0|ov.build.genAiBuild=10:2026.3.0.0|ov.build.tokenizersBuild=8:2026.3.0|ov.build.workerManifestDigest=64:1111111111111111111111111111111111111111111111111111111111111111|ov.optimizer.nncf=5:3.3.0|ov.optimizer.openvino=8:2026.3.0|ov.turboQuant.sourceCommit=40:0123456789abcdef0123456789abcdef01234567|ov.turboQuant.implementationCommit=40:0123456789abcdef0123456789abcdef01234567|ov.turboQuant.patchSeriesDigest=64:1111111111111111111111111111111111111111111111111111111111111111|ov.turboQuant.runtimeManifestDigest=64:2222222222222222222222222222222222222222222222222222222222222222",
            OptimizationCanonicalizer.Canonicalize(candidate, payload, contractVersion: 3));
        Assert.AreEqual(
            "6284cfbc5f5f63b25b8fc53a3e9566ec9bd1c94bd35b87dd81485fa5f9a66426",
            OptimizationCanonicalizer.ConfigurationSha256(
                candidate, payload, contractVersion: 3));
    }

    [TestMethod]
    public void V3DigestChangesWithTurboQuantPrecisionAndBuildIdentity()
    {
        OptimizationCandidate tbq4 = SelectedTurboCandidate(
            OpenVinoKvCacheFormat.TurboQuantTbq4).Selection.Candidate;
        OptimizationCandidate tbq3 = SelectedTurboCandidate(
            OpenVinoKvCacheFormat.TurboQuantTbq3).Selection.Candidate;

        string baseline = OptimizationCanonicalizer.ConfigurationSha256(
            tbq4,
            OptimizationExecutionPayload.ForOpenVino(Payload(
                OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq4,
                TurboBuild())),
            contractVersion: 3);

        string precisionChanged = OptimizationCanonicalizer.ConfigurationSha256(
            tbq3,
            OptimizationExecutionPayload.ForOpenVino(Payload(
                OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq3,
                TurboBuild())),
            contractVersion: 3);

        string identityChanged = OptimizationCanonicalizer.ConfigurationSha256(
            tbq4,
            OptimizationExecutionPayload.ForOpenVino(Payload(
                OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq4,
                TurboQuantBuildIdentity.Create(
                    "89abcdef0123456789abcdef0123456789abcdef",
                    Commit40,
                    Digest64,
                    OtherDigest64))),
            contractVersion: 3);

        Assert.AreNotEqual(baseline, precisionChanged);
        Assert.AreNotEqual(baseline, identityChanged);
    }

    [TestMethod]
    public void V3DigestBindsTheExactAvailableDiskAdmissionProof()
    {
        OptimizationCandidate baseline = SelectedTurboCandidate(
            OpenVinoKvCacheFormat.TurboQuantTbq4).Selection.Candidate;
        OptimizationCandidate changed = SelectedTurboCandidate(
            OpenVinoKvCacheFormat.TurboQuantTbq4,
            availableDiskBytes: 499 * Gibibyte).Selection.Candidate;
        OptimizationExecutionPayload payload = OptimizationExecutionPayload.ForOpenVino(
            Payload(
                OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq4,
                TurboBuild()));

        Assert.AreNotEqual(
            OptimizationCanonicalizer.ConfigurationSha256(baseline, payload, 3),
            OptimizationCanonicalizer.ConfigurationSha256(changed, payload, 3));
    }

    [TestMethod]
    public void V3CanonicalBindsEveryAdmissionProofFieldInFixedOrder()
    {
        OptimizationCandidate candidate = SelectedTurboCandidate(
            OpenVinoKvCacheFormat.TurboQuantTbq4).Selection.Candidate;
        string canonical = OptimizationCanonicalizer.Canonicalize(
            candidate,
            OptimizationExecutionPayload.ForOpenVino(Payload(
                OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq4,
                TurboBuild())),
            contractVersion: 3);
        string[] labels =
        [
            "admission.snapshotId", "admission.capabilitySha256",
            "admission.workloadId", "admission.workloadSha256",
            "admission.journeySha256", "admission.routeExecutionAuthoritySha256",
            "admission.configuration", "admission.evidenceId",
            "admission.supportLevel", "admission.requiresEvidence",
            "admission.optInEvidenceId", "admission.experimental",
            "admission.conversionProvenance", "admission.notice",
            "admission.evidenceGrade", "admission.quality",
            "admission.performance", "admission.stability",
            "admission.contextTokens", "admission.predictedPeakBytes",
            "admission.safeBudgetBytes", "admission.headroomBytes",
            "admission.dedicatedEstablished",
            "admission.dedicatedRequiredBytes",
            "admission.dedicatedSafeBudgetBytes",
            "admission.dedicatedHeadroomBytes",
            "admission.workingStoragePhasePeakBytes", "admission.outputDiskBytes",
            "admission.diskObligationBytes", "admission.availableDiskBytes",
            "admission.persistent"
        ];

        int previous = -1;
        foreach (string label in labels)
        {
            int current = canonical.IndexOf(label + "=", StringComparison.Ordinal);
            Assert.IsTrue(current > previous, $"{label} was absent or out of order.");
            previous = current;
        }
    }

    [TestMethod]
    public void MutatingEachAdmissionProofFieldOneAtATimeRejectsReplayAndChangesDigest()
    {
        (OptimizationSelection selection, OptimizationCapabilitySnapshot snapshot,
            OptimizationWorkload workload) = SelectedTurboCandidate(
                OpenVinoKvCacheFormat.TurboQuantTbq4);
        OptimizationCandidate candidate = selection.Candidate;
        OptimizationExecutionPayload payload = OptimizationExecutionPayload.ForOpenVino(
            Payload(OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq4, TurboBuild()));
        _ = OptimizationPlanIssuer.Issue(
            selection, payload, snapshot, workload, Binding(), 32,
            DateTimeOffset.UnixEpoch);
        string baseline = OptimizationCanonicalizer.ConfigurationSha256(
            candidate, payload, 3);
        FieldInfo[] fields = typeof(OptimizationAdmissionProof)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.Name.Contains("BackingField", StringComparison.Ordinal))
            .ToArray();

        foreach (FieldInfo field in fields)
        {
            object? original = field.GetValue(candidate.AdmissionProof!);
            object changed = ChangedValue(field.FieldType, original);
            try
            {
                field.SetValue(candidate.AdmissionProof, changed);
                Assert.AreNotEqual(
                    baseline,
                    OptimizationCanonicalizer.ConfigurationSha256(candidate, payload, 3),
                    $"{field.Name} did not participate in the v3 digest.");
                Assert.ThrowsExactly<ArgumentException>(() =>
                    OptimizationPlanIssuer.Issue(
                        selection, payload, snapshot, workload, Binding(), 32,
                        DateTimeOffset.UnixEpoch),
                    $"{field.Name} was accepted by replay validation.");
            }
            finally
            {
                field.SetValue(candidate.AdmissionProof, original);
            }
        }

        Assert.AreEqual(30, fields.Length);
    }

    private static object ChangedValue(Type type, object? original)
    {
        if (type == typeof(string))
        {
            return (string?)original + "-changed";
        }
        if (type == typeof(bool))
        {
            return !(bool)original!;
        }
        if (type == typeof(int))
        {
            return checked((int)original! + 1);
        }
        if (type == typeof(ulong))
        {
            return checked((ulong)original! + 1);
        }
        if (type == typeof(ulong?))
        {
            return original is null ? 1UL : checked((ulong)original + 1);
        }
        if (type.IsEnum)
        {
            return Enum.GetValues(type).Cast<object>()
                .First(value => !value.Equals(original));
        }

        throw new AssertFailedException($"No one-field mutation exists for {type.Name}.");
    }

    [TestMethod]
    public void GgufTurboQuantPlanRejectsRuntimeOutsidePinnedCapabilityIdentity()
    {
        GgufAdmittedConfiguration admitted = GgufAdmittedConfiguration.Create(
            "turbo3", CompatibilityBackend.IntelVulkan,
            DeviceRouteId.IntelIntegratedGpu, GgufWeightFormat.Imported,
            GgufKvCacheFormat.TurboQuant3Bit, GpuOffloadLevel.Full,
            512, 32768, SupportLevel.Experimental, requiresEvidence: true);
        GgufTurboQuantImplementationIdentity identity =
            GgufTurboQuantImplementationIdentity.Create(
                "turbo3",
                "519f0c594a8e31467d2e2f2cf17054c9e7e11536",
                CompatibilityBackend.IntelVulkan,
                DeviceRouteId.IntelIntegratedGpu);
        OptimizationCandidate candidate = OptimizationCandidate.Create(
            GgufRouteConfiguration.Create(
                admitted.Weights, admitted.KvCache, admitted.Backend,
                admitted.Device, admitted.Offload),
            OptimizationCandidateMetrics.Create(
                EvidenceGrade.Estimated,
                OptimizationAssessment.Acceptable,
                OptimizationAssessment.Acceptable,
                OptimizationAssessment.Acceptable,
                4096,
                8 * Gibibyte,
                32 * Gibibyte,
                24 * Gibibyte,
                0,
                0,
                requiresPersistentChange: false,
                availableDiskBytes: 500 * Gibibyte),
            admitted.EvidenceId,
            isExperimental: true);
        OptimizationCapabilitySnapshot snapshot = OptimizationCapabilitySnapshot.ForGguf(
            "gguf-turbo",
            Digest64,
            GgufCapabilityPayload.Create(
                "turbo3", [admitted], turboQuantImplementation: identity,
                runtimeAuthority: GgufRuntimeAuthority.Create(
                    "turbo3", identity.SourceCommit,
                    [GgufExecutionProfileAuthority.Create(
                        admitted.EvidenceId, EvidenceGrade.Estimated,
                        identity.RuntimeName, false, 8, 512, 512)])));
        OptimizationWorkload workload = OptimizationWorkload.Create(
            "chat", 512, OptimizationAssessment.Poor,
            [ContextTokenCount.FromTokens(4096)]);
        candidate = OptimizationAdmissionTestFactory.Admit(
            candidate, snapshot, workload, Binding(),
            SupportLevel.Experimental, requiresEvidence: true);
        OptimizationExecutionPayload mismatched = OptimizationExecutionPayload.ForGguf(
            GgufExecutionPayload.Create(
                "turbo3",
                "5bc5ed3bdc25003aa9f07422753a7b8d4f9190fc",
                GgufRuntimeBackend.Vulkan,
                "GPU.0",
                4096,
                GgufCacheType.Turbo3,
                GgufCacheType.Turbo3,
                32,
                false,
                8,
                512,
                "Estimated",
                "turbo3",
                512,
                GgufWeightFormat.Imported));

        Assert.ThrowsExactly<ArgumentException>(() => OptimizationPlanIssuer.Issue(
            OptimizationPreferenceResolver.Resolve(
                [candidate], OptimizationPreferenceSelection.Automatic())!,
            mismatched,
            snapshot,
            workload,
            Binding(),
            modelLayerCount: 32,
            DateTimeOffset.UnixEpoch));

        OptimizationExecutionPayload exact = OptimizationExecutionPayload.ForGguf(
            GgufExecutionPayload.Create(
                identity.RuntimeName,
                identity.SourceCommit,
                GgufRuntimeBackend.Vulkan,
                "GPU.0",
                4096,
                GgufCacheType.Turbo3,
                GgufCacheType.Turbo3,
                32,
                false,
                8,
                512,
                "Estimated",
                identity.RuntimeName,
                512,
                GgufWeightFormat.Imported));
        OptimizationExecutionPlan plan = OptimizationPlanIssuer.Issue(
            OptimizationPreferenceResolver.Resolve(
                [candidate], OptimizationPreferenceSelection.Automatic())!,
            exact,
            snapshot,
            workload,
            Binding(),
            modelLayerCount: 32,
            DateTimeOffset.UnixEpoch);
        string canonical = OptimizationCanonicalizer.Canonicalize(
            plan.Candidate, plan.ExecutionPayload, contractVersion: 3);

        StringAssert.Contains(canonical, "gguf.runtimeBuildId=6:turbo3");
        StringAssert.Contains(
            canonical,
            "gguf.runtimeSourceCommit=40:519f0c594a8e31467d2e2f2cf17054c9e7e11536");
    }

    [TestMethod]
    public void GgufPlanAcceptsAuthenticatedAlreadyAtTargetWeightNormalization()
    {
        GgufAdmittedConfiguration admitted = GgufAdmittedConfiguration.Create(
            "gguf-q4", CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
            GgufWeightFormat.Q4KM, GgufKvCacheFormat.F16,
            GpuOffloadLevel.None, 512, 32768,
            SupportLevel.DeclaredSupported, requiresEvidence: false);
        OptimizationCandidate candidate = RuntimeOnlyGgufCandidate(
            admitted, GgufWeightFormat.Imported);
        OptimizationCapabilitySnapshot snapshot = OptimizationCapabilitySnapshot.ForGguf(
            "gguf-standard", Digest64,
            GgufCapabilityPayload.Create(
                "standard", [admitted],
                runtimeAuthority: GgufRuntimeAuthority.Create(
                    "standard", Commit40,
                    [GgufExecutionProfileAuthority.Create(
                        admitted.EvidenceId, EvidenceGrade.Estimated, "profile",
                        flashAttention: false, threadCount: 4, batchSize: 128,
                        maximumGeneratedTokens: 256)])));
        OptimizationWorkload workload = OptimizationWorkload.Create(
            "chat", 512, OptimizationAssessment.Poor,
            [ContextTokenCount.FromTokens(4096)]);

        Assert.IsNull(OptimizationPreferenceResolver.Resolve(
            [candidate], OptimizationPreferenceSelection.Automatic()));

        CrossRouteGenerationResult generated = CrossRouteCandidateGenerator.Generate(
            snapshot,
            InspectedModelFacts.Create(
                ByteCount.FromBytes(3 * Gibibyte), 32, 4096, 32, 8,
                8192, fileType: 15, quantisationVersion: 2),
            workload,
            Binding(),
            ByteCount.FromBytes(32 * Gibibyte),
            ByteCount.FromBytes(500 * Gibibyte),
            EstimatorPolicy.ProvisionalV1(),
            new HashSet<string> { "gguf-q2" },
            OptimizationHardwareAuthorityTestData.AllEstablished());
        OptimizationSelection selected = OptimizationPreferenceResolver.Resolve(
            generated.Candidates, OptimizationPreferenceSelection.Automatic())
            ?? throw new AssertFailedException("Already-at-target candidate was absent.");
        OptimizationExecutionPlan plan = OptimizationPlanIssuer.Issue(
            selected,
            RuntimeOnlyGgufPayload("standard", Commit40, GgufCacheType.F16),
            snapshot, workload, Binding(), 32, DateTimeOffset.UnixEpoch);

        Assert.AreEqual(GgufWeightFormat.Imported,
            ((GgufRouteConfiguration)plan.Candidate.Configuration).Weights);
        string canonical = OptimizationCanonicalizer.Canonicalize(
            plan.Candidate, plan.ExecutionPayload, contractVersion: 3);
        StringAssert.Contains(canonical, "gguf.normalizedFileType=2:15");
        StringAssert.Contains(canonical, "gguf.normalizedQuantisationVersion=1:2");
        StringAssert.Contains(canonical, "admission.snapshotId=13:gguf-standard");
        StringAssert.Contains(canonical, "admission.diskObligationBytes=");
        string normalizedDigest = OptimizationCanonicalizer.ConfigurationSha256(
            plan.Candidate, plan.ExecutionPayload, 3);
        FieldInfo[] normalizationFields = typeof(GgufWeightNormalizationProof)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.Name.Contains("BackingField", StringComparison.Ordinal))
            .ToArray();
        foreach (FieldInfo field in normalizationFields)
        {
            object? original = field.GetValue(plan.Candidate.WeightNormalizationProof!);
            try
            {
                field.SetValue(
                    plan.Candidate.WeightNormalizationProof,
                    ChangedValue(field.FieldType, original));
                Assert.AreNotEqual(
                    normalizedDigest,
                    OptimizationCanonicalizer.ConfigurationSha256(
                        plan.Candidate, plan.ExecutionPayload, 3),
                    $"{field.Name} did not participate in the v3 digest.");
            }
            finally
            {
                field.SetValue(plan.Candidate.WeightNormalizationProof, original);
            }
        }
        Assert.AreEqual(4, normalizationFields.Length);
        Assert.ThrowsExactly<ArgumentException>(() =>
            OptimizationCanonicalizer.CanonicalizeV2(
                plan.Candidate, plan.ExecutionPayload));

        OptimizationCapabilitySnapshot changedSnapshot =
            OptimizationCapabilitySnapshot.ForGguf(
                "gguf-changed", OtherDigest64, snapshot.Gguf!);
        Assert.ThrowsExactly<ArgumentException>(() => OptimizationPlanIssuer.Issue(
            selected,
            plan.ExecutionPayload,
            changedSnapshot,
            workload,
            Binding(), 32, DateTimeOffset.UnixEpoch));
        OptimizationWorkload changedWorkload = OptimizationWorkload.Create(
            "different-chat", 512, OptimizationAssessment.Poor,
            [ContextTokenCount.FromTokens(4096)]);
        Assert.ThrowsExactly<ArgumentException>(() => OptimizationPlanIssuer.Issue(
            selected,
            plan.ExecutionPayload,
            snapshot,
            changedWorkload,
            Binding(), 32, DateTimeOffset.UnixEpoch));
    }

    [TestMethod]
    public void GgufPlanAcceptsStandardFallbackFromMixedTurboQuantSnapshot()
    {
        GgufAdmittedConfiguration standard = GgufAdmittedConfiguration.Create(
            "gguf-standard", CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
            GgufWeightFormat.Imported, GgufKvCacheFormat.F16,
            GpuOffloadLevel.None, 512, 32768,
            SupportLevel.DeclaredSupported, requiresEvidence: false);
        GgufAdmittedConfiguration turbo = GgufAdmittedConfiguration.Create(
            "turbo3", CompatibilityBackend.IntelVulkan,
            DeviceRouteId.IntelIntegratedGpu, GgufWeightFormat.Imported,
            GgufKvCacheFormat.TurboQuant3Bit, GpuOffloadLevel.Full,
            512, 32768, SupportLevel.Experimental, requiresEvidence: true);
        GgufTurboQuantImplementationIdentity identity =
            GgufTurboQuantImplementationIdentity.Create(
                "turbo3",
                "519f0c594a8e31467d2e2f2cf17054c9e7e11536",
                CompatibilityBackend.IntelVulkan,
                DeviceRouteId.IntelIntegratedGpu);
        OptimizationCapabilitySnapshot snapshot = OptimizationCapabilitySnapshot.ForGguf(
            "gguf-mixed", Digest64,
            GgufCapabilityPayload.Create(
                identity.RuntimeName, [standard, turbo],
                turboQuantImplementation: identity,
                runtimeAuthority: GgufAuthority(
                    identity.RuntimeName, identity.SourceCommit, standard, turbo)));
        OptimizationCandidate candidate = RuntimeOnlyGgufCandidate(
            standard, GgufWeightFormat.Imported);
        OptimizationWorkload workload = OptimizationWorkload.Create(
            "chat", 512, OptimizationAssessment.Poor,
            [ContextTokenCount.FromTokens(4096)]);
        candidate = OptimizationAdmissionTestFactory.Admit(
            candidate, snapshot, workload, Binding(),
            SupportLevel.DeclaredSupported);

        OptimizationExecutionPlan plan = OptimizationPlanIssuer.Issue(
            OptimizationPreferenceResolver.Resolve(
                [candidate], OptimizationPreferenceSelection.Automatic())!,
            RuntimeOnlyGgufPayload(
                identity.RuntimeName, identity.SourceCommit, GgufCacheType.F16),
            snapshot,
            workload,
            Binding(), 32, DateTimeOffset.UnixEpoch);

        Assert.AreEqual(GgufKvCacheFormat.F16,
            ((GgufRouteConfiguration)plan.Candidate.Configuration).KvCache);
        Assert.ThrowsExactly<ArgumentException>(() => OptimizationPlanIssuer.Issue(
            OptimizationPreferenceResolver.Resolve(
                [candidate], OptimizationPreferenceSelection.Automatic())!,
            RuntimeOnlyGgufPayload(
                identity.RuntimeName, Commit40, GgufCacheType.F16),
            snapshot,
            workload,
            Binding(), 32, DateTimeOffset.UnixEpoch));
    }

    [TestMethod]
    public void StandardGgufPlanRejectsRuntimeSourceEvidenceAndProfileSubstitution()
    {
        GgufAdmittedConfiguration admitted = GgufAdmittedConfiguration.Create(
            "gguf-standard", CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
            GgufWeightFormat.Imported, GgufKvCacheFormat.F16,
            GpuOffloadLevel.None, 512, 32768,
            SupportLevel.DeclaredSupported, requiresEvidence: false);
        OptimizationCapabilitySnapshot snapshot = OptimizationCapabilitySnapshot.ForGguf(
            "gguf-standard", Digest64,
            GgufCapabilityPayload.Create(
                "standard", [admitted],
                runtimeAuthority: GgufRuntimeAuthority.Create(
                    "standard", Commit40,
                    [GgufExecutionProfileAuthority.Create(
                        admitted.EvidenceId, EvidenceGrade.Estimated, "profile",
                        false, 4, 128, 256)])));
        OptimizationWorkload workload = OptimizationWorkload.Create(
            "chat", 512, OptimizationAssessment.Poor,
            [ContextTokenCount.FromTokens(4096)]);
        OptimizationCandidate candidate = OptimizationAdmissionTestFactory.Admit(
            RuntimeOnlyGgufCandidate(admitted, GgufWeightFormat.Imported),
            snapshot, workload, Binding(), SupportLevel.DeclaredSupported);
        OptimizationSelection selection = OptimizationPreferenceResolver.Resolve(
            [candidate], OptimizationPreferenceSelection.Automatic())!;

        OptimizationExecutionPayload Payload(
            string build = "standard",
            string source = Commit40,
            string evidence = "Estimated",
            string profile = "profile",
            bool flashAttention = false,
            int threadCount = 4,
            int batchSize = 128,
            int maximumGeneratedTokens = 256) =>
            OptimizationExecutionPayload.ForGguf(GgufExecutionPayload.Create(
                build, source, GgufRuntimeBackend.Cpu, "CPU", 4096,
                GgufCacheType.F16, GgufCacheType.F16, 0, flashAttention,
                threadCount, batchSize, evidence, profile,
                maximumGeneratedTokens, GgufWeightFormat.Imported));

        _ = OptimizationPlanIssuer.Issue(
            selection, Payload(), snapshot, workload, Binding(), 32,
            DateTimeOffset.UnixEpoch);

        foreach (OptimizationExecutionPayload substituted in new[]
        {
            Payload(build: "other-build"),
            Payload(source: "89abcdef0123456789abcdef0123456789abcdef"),
            Payload(evidence: "Measured"),
            Payload(profile: "other-profile"),
            Payload(flashAttention: true),
            Payload(threadCount: 8),
            Payload(batchSize: 256),
            Payload(maximumGeneratedTokens: 512)
        })
        {
            Assert.ThrowsExactly<ArgumentException>(() => OptimizationPlanIssuer.Issue(
                selection, substituted, snapshot, workload, Binding(), 32,
                DateTimeOffset.UnixEpoch));

            GgufExecutionPayload changed = substituted.Gguf!;
            OptimizationCapabilitySnapshot replay =
                OptimizationCapabilitySnapshot.ForGguf(
                    snapshot.SnapshotId,
                    snapshot.CapabilitySnapshotSha256,
                    GgufCapabilityPayload.Create(
                        changed.RuntimeBuildId,
                        [admitted],
                        runtimeAuthority: GgufRuntimeAuthority.Create(
                            changed.RuntimeBuildId,
                            changed.RuntimeSourceCommit,
                            [GgufExecutionProfileAuthority.Create(
                                admitted.EvidenceId,
                                Enum.Parse<EvidenceGrade>(changed.EvidenceGrade),
                                changed.ProfileId,
                                changed.FlashAttention,
                                changed.ThreadCount,
                                changed.BatchSize,
                                changed.MaximumGeneratedTokens)])));
            Assert.ThrowsExactly<ArgumentException>(() => OptimizationPlanIssuer.Issue(
                selection, substituted, replay, workload, Binding(), 32,
                DateTimeOffset.UnixEpoch));
        }
    }

    [TestMethod]
    public void AdmissionProofRejectsRouteAuthoritySubstitutionBehindReusedSnapshotDigest()
    {
        GgufAdmittedConfiguration admitted = GgufAdmittedConfiguration.Create(
            "gguf-standard", CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
            GgufWeightFormat.Imported, GgufKvCacheFormat.F16,
            GpuOffloadLevel.None, 512, 32768,
            SupportLevel.DeclaredSupported, requiresEvidence: false);
        OptimizationCapabilitySnapshot original = OptimizationCapabilitySnapshot.ForGguf(
            "gguf-standard", Digest64,
            GgufCapabilityPayload.Create(
                "standard", [admitted],
                runtimeAuthority: GgufRuntimeAuthority.Create(
                    "standard", Commit40,
                    [GgufExecutionProfileAuthority.Create(
                        admitted.EvidenceId, EvidenceGrade.Estimated, "profile",
                        false, 4, 128, 256)])));
        OptimizationWorkload workload = OptimizationWorkload.Create(
            "chat", 512, OptimizationAssessment.Poor,
            [ContextTokenCount.FromTokens(4096)]);
        OptimizationCandidate candidate = OptimizationAdmissionTestFactory.Admit(
            RuntimeOnlyGgufCandidate(admitted, GgufWeightFormat.Imported),
            original, workload, Binding(), SupportLevel.DeclaredSupported);
        OptimizationSelection selection = OptimizationPreferenceResolver.Resolve(
            [candidate], OptimizationPreferenceSelection.Automatic())!;

        const string changedCommit =
            "89abcdef0123456789abcdef0123456789abcdef";
        OptimizationCapabilitySnapshot substituted =
            OptimizationCapabilitySnapshot.ForGguf(
                original.SnapshotId, original.CapabilitySnapshotSha256,
                GgufCapabilityPayload.Create(
                    "changed-build", [admitted],
                    runtimeAuthority: GgufRuntimeAuthority.Create(
                        "changed-build", changedCommit,
                        [GgufExecutionProfileAuthority.Create(
                            admitted.EvidenceId, EvidenceGrade.Estimated,
                            "changed-profile", false, 4, 128, 256)])));
        OptimizationExecutionPayload matchingSubstitution =
            OptimizationExecutionPayload.ForGguf(GgufExecutionPayload.Create(
                "changed-build", changedCommit, GgufRuntimeBackend.Cpu, "CPU",
                4096, GgufCacheType.F16, GgufCacheType.F16, 0, false, 4, 128,
                "Estimated", "changed-profile", 256,
                GgufWeightFormat.Imported));

        Assert.ThrowsExactly<ArgumentException>(() => OptimizationPlanIssuer.Issue(
            selection, matchingSubstitution, substituted, workload, Binding(), 32,
            DateTimeOffset.UnixEpoch));
    }

    [TestMethod]
    public void ControlledGgufRequantisationRequiresExactQuantiserAndSourceBinding()
    {
        OptimizationJourneyBinding acknowledgedBinding = GgufBinding(Digest64);
        GgufQuantiserIdentity quantiser = GgufQuantiser(Digest64);
        (OptimizationSelection selection, OptimizationCapabilitySnapshot snapshot,
            OptimizationWorkload workload) = SelectedGgufCandidate(
                acknowledgedBinding, quantiser);

        OptimizationExecutionPlan plan = OptimizationPlanIssuer.Issue(
            selection,
            GgufPayload(quantiser, snapshot),
            snapshot,
            workload,
            acknowledgedBinding,
            32,
            DateTimeOffset.UnixEpoch);

        Assert.IsTrue(plan.ProducesPersistentArtifact);
        Assert.AreEqual(quantiser, plan.ExecutionPayload.Gguf!.Quantiser);

        Assert.ThrowsExactly<ArgumentException>(() => OptimizationPlanIssuer.Issue(
            selection,
            GgufPayload(GgufQuantiser(OtherDigest64), snapshot),
            snapshot,
            workload,
            acknowledgedBinding,
            32,
            DateTimeOffset.UnixEpoch));

        Assert.ThrowsExactly<ArgumentException>(() => OptimizationPlanIssuer.Issue(
            selection,
            GgufPayload(quantiser, snapshot),
            snapshot,
            workload,
            GgufBinding(OtherDigest64),
            32,
            DateTimeOffset.UnixEpoch));

        Assert.ThrowsExactly<ArgumentException>(() => OptimizationPlanIssuer.Issue(
            selection,
            GgufPayload(quantiser, snapshot, conversionSource: null,
                overrideConversionSource: true),
            snapshot,
            workload,
            acknowledgedBinding,
            32,
            DateTimeOffset.UnixEpoch));

        GgufConversionSourceBinding substitutedSource =
            GgufConversionSourceBinding.Create(
                WeightQuantisation.Q4_K_M,
                GgufBindingWithSource(OtherDigest64, 3 * Gibibyte));
        Assert.ThrowsExactly<ArgumentException>(() => OptimizationPlanIssuer.Issue(
            selection,
            GgufPayload(quantiser, snapshot, substitutedSource,
                overrideConversionSource: true),
            snapshot,
            workload,
            acknowledgedBinding,
            32,
            DateTimeOffset.UnixEpoch));

        OptimizationCandidate weakerWarning = OptimizationCandidate.Create(
            selection.Candidate.Configuration,
            selection.Candidate.Metrics,
            selection.Candidate.EvidenceId,
            selection.Candidate.IsExperimental,
            OptimizationConversionProvenance.HigherPrecisionSource);
        Assert.AreEqual(OptimizationCandidateNotice.LowQuality, weakerWarning.Notice);
        Assert.IsNull(OptimizationPreferenceResolver.Resolve(
            [weakerWarning], OptimizationPreferenceSelection.Manual(10)));

        Assert.ThrowsExactly<ArgumentException>(() => OptimizationPlanIssuer.Issue(
            selection,
            GgufPayload(quantiser, snapshot),
            snapshot,
            workload,
            GgufBindingWithSource(OtherDigest64, 3 * Gibibyte),
            32,
            DateTimeOffset.UnixEpoch));

        Assert.ThrowsExactly<ArgumentException>(() => OptimizationPlanIssuer.Issue(
            selection,
            GgufPayload(quantiser, snapshot),
            snapshot,
            workload,
            GgufBindingWithSource(Digest64, 3 * Gibibyte + 1),
            32,
            DateTimeOffset.UnixEpoch));
    }

    [TestMethod]
    public void V3DigestBindsTheTypedLowQualityWarning()
    {
        OptimizationJourneyBinding binding = GgufBinding(Digest64);
        GgufQuantiserIdentity quantiser = GgufQuantiser(Digest64);
        (OptimizationSelection selection, OptimizationCapabilitySnapshot snapshot,
            OptimizationWorkload workload) = SelectedGgufCandidate(binding, quantiser);
        OptimizationCandidate warned = selection.Candidate;
        OptimizationCandidate unwarned = OptimizationCandidate.Create(
            warned.Configuration,
            warned.Metrics,
            warned.EvidenceId,
            warned.IsExperimental,
            OptimizationConversionProvenance.HigherPrecisionSource);
        unwarned = OptimizationAdmissionTestFactory.Admit(
            unwarned, snapshot, workload, binding,
            SupportLevel.DeclaredSupported, requiresEvidence: true);
        OptimizationExecutionPayload payload = GgufPayload(quantiser, snapshot: null);

        Assert.AreNotEqual(
            OptimizationCanonicalizer.ConfigurationSha256(warned, payload, 3),
            OptimizationCanonicalizer.ConfigurationSha256(unwarned, payload, 3));
    }

    [TestMethod]
    public void V3DigestBindsConversionSourceAndExactRequantisationTarget()
    {
        OptimizationJourneyBinding binding = GgufBinding(Digest64);
        GgufQuantiserIdentity quantiser = GgufQuantiser(Digest64);
        (OptimizationSelection selection, OptimizationCapabilitySnapshot snapshot, _) =
            SelectedGgufCandidate(binding, quantiser);
        GgufExecutionPayload baselineGguf = GgufPayload(quantiser, snapshot).Gguf!;
        OptimizationExecutionPayload baseline = OptimizationExecutionPayload.ForGguf(
            baselineGguf);

        GgufConversionSourceBinding changedSource = GgufConversionSourceBinding.Create(
            WeightQuantisation.Q4_K_M,
            GgufBindingWithSource(OtherDigest64, 3 * Gibibyte));
        GgufAdmittedConfiguration changedTarget = GgufAdmittedConfiguration.Create(
            "gguf-q2", CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
            GgufWeightFormat.Q3KM, GgufKvCacheFormat.F16,
            GpuOffloadLevel.None, 512, 32768,
            SupportLevel.DeclaredSupported, requiresEvidence: true);

        OptimizationExecutionPayload sourceChanged = WithGgufAuthority(
            baselineGguf,
            changedSource,
            GgufRequantisationPolicy.Create(
                true, true, true, changedTarget, quantiser, changedSource));
        OptimizationExecutionPayload policyTargetChanged = WithGgufAuthority(
            baselineGguf,
            snapshot.Gguf!.ConversionSource!,
            GgufRequantisationPolicy.Create(
                true, true, true, changedTarget, quantiser,
                snapshot.Gguf.ConversionSource!));

        string digest = OptimizationCanonicalizer.ConfigurationSha256(
            selection.Candidate, baseline, 3);
        Assert.AreNotEqual(digest, OptimizationCanonicalizer.ConfigurationSha256(
            selection.Candidate, sourceChanged, 3));
        Assert.AreNotEqual(digest, OptimizationCanonicalizer.ConfigurationSha256(
            selection.Candidate, policyTargetChanged, 3));
    }

    [TestMethod]
    public void TrustedSourceContextUsesTheExactlyBoundHigherPrecisionSource()
    {
        OptimizationJourneyBinding imported = GgufBinding(Digest64);
        OptimizationJourneyBinding higherPrecisionJourney =
            OptimizationJourneyBinding.Create(
                "mi-run-source", "mi-handoff-source", OtherDigest64,
                6 * Gibibyte, "hw-run-1", Digest64);
        GgufConversionSourceBinding source = GgufConversionSourceBinding.Create(
            WeightQuantisation.F16, higherPrecisionJourney);
        GgufQuantiserIdentity quantiser = GgufQuantiser(Digest64);
        GgufAdmittedConfiguration admitted = GgufAdmittedConfiguration.Create(
            "gguf-q4", CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
            GgufWeightFormat.Q4KM, GgufKvCacheFormat.F16,
            GpuOffloadLevel.None, 512, 32768,
            SupportLevel.DeclaredSupported, requiresEvidence: false);
        OptimizationCandidate candidate = OptimizationCandidate.Create(
            GgufRouteConfiguration.Create(
                admitted.Weights, admitted.KvCache, admitted.Backend,
                admitted.Device, admitted.Offload),
            OptimizationCandidateMetrics.Create(
                EvidenceGrade.Estimated, OptimizationAssessment.Good,
                OptimizationAssessment.Good, OptimizationAssessment.Good,
                4096, 8 * Gibibyte, 32 * Gibibyte, 24 * Gibibyte,
                4 * Gibibyte,
                4 * Gibibyte, requiresPersistentChange: true,
                availableDiskBytes: 500 * Gibibyte),
            admitted.EvidenceId, false,
            OptimizationConversionProvenance.HigherPrecisionSource);
        OptimizationCapabilitySnapshot snapshot = OptimizationCapabilitySnapshot.ForGguf(
            "gguf-cap", Digest64,
            GgufCapabilityPayload.Create(
                "b4321", [admitted], hasHigherPrecisionSource: true,
                conversionSource: source, admittedQuantiser: quantiser,
                runtimeAuthority: GgufAuthority(
                    "b4321", Commit40, admitted)));
        OptimizationWorkload workload = OptimizationWorkload.Create(
            "chat", 512, OptimizationAssessment.Poor,
            [ContextTokenCount.FromTokens(4096)]);
        candidate = OptimizationAdmissionTestFactory.Admit(
            candidate, snapshot, workload, imported,
            SupportLevel.DeclaredSupported);
        OptimizationExecutionPayload payload = OptimizationExecutionPayload.ForGguf(
            GgufExecutionPayload.Create(
                "b4321", Commit40, GgufRuntimeBackend.Cpu, "CPU", 4096,
                GgufCacheType.F16, GgufCacheType.F16, 0, false, 4, 128,
                "Estimated", "profile", 256, GgufWeightFormat.Q4KM,
                quantiser, source));
        OptimizationExecutionPlan plan = OptimizationPlanIssuer.Issue(
            OptimizationPreferenceResolver.Resolve(
                [candidate], OptimizationPreferenceSelection.Manual(10))!,
            payload, snapshot,
            workload,
            imported, 32, DateTimeOffset.UnixEpoch);

        TrustedSourceContext trusted = TrustedSourceContext.ForPlan(
            plan, "C:\\private\\higher-precision.gguf");

        Assert.IsTrue(string.Equals(
            trusted.ModelInspectionRunId,
            higherPrecisionJourney.ModelInspectionRunId,
            StringComparison.Ordinal));
        Assert.IsTrue(string.Equals(
            trusted.ExpectedModelSha256,
            higherPrecisionJourney.ModelSha256,
            StringComparison.Ordinal));
        Assert.IsTrue(
            trusted.ExpectedModelLengthBytes == higherPrecisionJourney.ModelLengthBytes);
        Assert.IsFalse(string.Equals(
            trusted.ExpectedModelSha256, imported.ModelSha256,
            StringComparison.Ordinal));
    }

    private static (OptimizationSelection Selection,
        OptimizationCapabilitySnapshot Snapshot,
        OptimizationWorkload Workload) SelectedGgufCandidate(
            OptimizationJourneyBinding binding,
            GgufQuantiserIdentity quantiser)
    {
        GgufAdmittedConfiguration admitted = GgufAdmittedConfiguration.Create(
            "gguf-q2", CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
            GgufWeightFormat.Q2K, GgufKvCacheFormat.F16,
            GpuOffloadLevel.None, 512, 32768,
            SupportLevel.DeclaredSupported, requiresEvidence: true);
        GgufConversionSourceBinding source = GgufConversionSourceBinding.Create(
            WeightQuantisation.Q4_K_M, binding);
        GgufRequantisationPolicy policy = GgufRequantisationPolicy.Create(
            true, true, true, admitted, quantiser, source);
        OptimizationCapabilitySnapshot snapshot = OptimizationCapabilitySnapshot.ForGguf(
            "gguf-cap",
            Digest64,
            GgufCapabilityPayload.Create(
                "b4321",
                [
                    admitted
                ],
                hasHigherPrecisionSource: false,
                policy,
                source,
                quantiser,
                runtimeAuthority: GgufAuthority(
                    "b4321", Commit40, admitted)));
        OptimizationWorkload workload = OptimizationWorkload.Create(
            "chat", 512, OptimizationAssessment.Poor,
            [ContextTokenCount.FromTokens(4096)]);
        CrossRouteGenerationResult generated = CrossRouteCandidateGenerator.Generate(
            snapshot,
            InspectedModelFacts.Create(
                ByteCount.FromBytes(3 * Gibibyte), 32, 4096, 32, 8, 8192, 15, 2),
            workload,
            binding,
            ByteCount.FromBytes(32 * Gibibyte),
            ByteCount.FromBytes(500 * Gibibyte),
            EstimatorPolicy.ProvisionalV1(),
            new HashSet<string> { admitted.EvidenceId },
            OptimizationHardwareAuthorityTestData.AllEstablished());
        OptimizationSelection selection = OptimizationPreferenceResolver.Resolve(
            generated.Candidates, OptimizationPreferenceSelection.Manual(10))
            ?? throw new AssertFailedException("The authorized Q2_K candidate was absent.");

        return (selection, snapshot, workload);
    }

    private static OptimizationJourneyBinding GgufBinding(string hardwareDigest) =>
        OptimizationJourneyBinding.Create(
            "mi-run-1", "mi-handoff-1", Digest64, 3 * Gibibyte,
            "hw-run-1", hardwareDigest);

    private static OptimizationJourneyBinding GgufBindingWithSource(
        string modelDigest, ulong modelLength) =>
        OptimizationJourneyBinding.Create(
            "mi-run-1", "mi-handoff-1", modelDigest, modelLength,
            "hw-run-1", Digest64);

    private static GgufQuantiserIdentity GgufQuantiser(string executableDigest) =>
        GgufQuantiserIdentity.Create("llama-quantize", "b4321", executableDigest);

    private static OptimizationExecutionPayload GgufPayload(
        GgufQuantiserIdentity quantiser,
        OptimizationCapabilitySnapshot? snapshot,
        GgufConversionSourceBinding? conversionSource = null,
        bool overrideConversionSource = false) =>
        OptimizationExecutionPayload.ForGguf(GgufExecutionPayload.Create(
            "b4321", Commit40, GgufRuntimeBackend.Cpu, "CPU", 4096,
            GgufCacheType.F16, GgufCacheType.F16, 0, false, 4, 128,
            "Estimated", "profile", 256, GgufWeightFormat.Q2K, quantiser,
            overrideConversionSource
                ? conversionSource
                : snapshot?.Gguf?.ConversionSource,
            snapshot?.Gguf?.RequantisationPolicy));

    private static OptimizationExecutionPayload WithGgufAuthority(
        GgufExecutionPayload template,
        GgufConversionSourceBinding source,
        GgufRequantisationPolicy policy) =>
        OptimizationExecutionPayload.ForGguf(GgufExecutionPayload.Create(
            template.RuntimeBuildId, template.RuntimeSourceCommit, template.Backend,
            template.DeviceId, template.ContextSize, template.KeyCacheType,
            template.ValueCacheType, template.GpuLayerCount, template.FlashAttention,
            template.ThreadCount, template.BatchSize, template.EvidenceGrade,
            template.ProfileId, template.MaximumGeneratedTokens,
            template.PersistentTargetWeightFormat, template.Quantiser, source, policy));

    private static (OptimizationSelection Selection,
        OptimizationCapabilitySnapshot Snapshot,
        OptimizationWorkload Workload) SelectedTurboCandidate(
            OpenVinoKvCacheFormat cache,
            ulong availableDiskBytes = 500 * Gibibyte)
    {
        OpenVinoAdmittedConfiguration admitted =
            OpenVinoAdmittedConfiguration.Create(
                "ov-turbo-evidence",
                DeviceRouteId.Cpu,
                OpenVinoWeightFormat.Int4,
                cache,
                OpenVinoPerformanceHint.Latency,
                Core.Routes.OpenVino.OpenVinoCompiledCachePolicy.Disabled,
                1,
                512,
                32768,
                SupportLevel.Experimental,
                requiresEvidence: true);
        OptimizationCapabilitySnapshot snapshot = OptimizationCapabilitySnapshot.ForOpenVino(
            "ov-cap",
            Digest64,
            OpenVinoCapabilityPayload.Create(
                "2026.3.0",
                [admitted],
                [OpenVinoExecutionAuthority.Create(
                    admitted.EvidenceId,
                    "openvino.experimental.cpu.int4.turbo.v3",
                    OpenVinoWeightPrecision.Fp16,
                    Build(),
                    Versions(),
                    compiledCacheIsDisposable: true,
                    turboQuantBuild: TurboBuild())]));
        OptimizationWorkload workload = OptimizationWorkload.Create(
            "chat",
            512,
            OptimizationAssessment.Poor,
            [ContextTokenCount.FromTokens(4096)]);
        CrossRouteGenerationResult generated = CrossRouteCandidateGenerator.Generate(
            snapshot,
            InspectedModelFacts.Create(
                ByteCount.FromBytes(3 * Gibibyte), 32, 4096, 32, 8, 8192, 15, 2),
            workload,
            Binding(),
            ByteCount.FromBytes(32 * Gibibyte),
            ByteCount.FromBytes(availableDiskBytes),
            EstimatorPolicy.ProvisionalV1(),
            new HashSet<string> { "ov-turbo-evidence" },
            OptimizationHardwareAuthorityTestData.AllEstablished());

        OptimizationSelection selection = OptimizationPreferenceResolver.Resolve(
            generated.Candidates,
            OptimizationPreferenceSelection.Automatic())
            ?? throw new AssertFailedException("The opted-in TurboQuant candidate was absent.");

        return (selection, snapshot, workload);
    }

    private static OpenVinoExecutionPayload Payload(
        OpenVinoKvCacheAlgorithm algorithm,
        OpenVinoKvCachePrecision precision,
        TurboQuantBuildIdentity? turboBuild,
        string runtimeBuild = "2026.3.0",
        string maturity = "Experimental candidate",
        bool createsCompletePackage = true,
        string evidenceId = "ov-turbo-evidence",
        string configurationId = "openvino.experimental.cpu.int4.turbo.v3",
        OpenVinoWeightPrecision sourceWeightPrecision = OpenVinoWeightPrecision.Fp16,
        OpenVinoBuildIdentity? buildIdentity = null,
        IReadOnlyDictionary<string, string>? optimizerVersions = null,
        bool compiledCacheIsDisposable = true) =>
        OpenVinoExecutionPayload.Create(
            configurationId,
            "CPU",
            maturity,
            evidenceId,
            sourceWeightPrecision,
            OpenVinoWeightPrecision.FourBit,
            precision,
            compiledCacheEnabled: false,
            compiledCacheIsDisposable,
            compiledCacheIsModelArtifact: false,
            createsCompletePackage,
            buildIdentity ?? OpenVinoBuildIdentity.Create(
                runtimeBuild, "2026.3.0.0", "2026.3.0", Digest64),
            optimizerVersions ?? Versions(),
            turboQuantBuild: turboBuild,
            kvCacheAlgorithm: algorithm);

    private static OptimizationJourneyBinding Binding() =>
        OptimizationJourneyBinding.Create(
            "mi-run-1", "mi-handoff-1", Digest64, 4 * Gibibyte,
            "hw-run-1", OtherDigest64);

    private static GgufRuntimeAuthority GgufAuthority(
        string runtimeBuild,
        string sourceCommit,
        params GgufAdmittedConfiguration[] admitted) =>
        GgufRuntimeAuthority.Create(
            runtimeBuild,
            sourceCommit,
            [.. admitted.Select(entry => GgufExecutionProfileAuthority.Create(
                entry.EvidenceId, EvidenceGrade.Estimated, "profile",
                false, 4, 128, 256))]);

    private static OptimizationCandidate RuntimeOnlyGgufCandidate(
        GgufAdmittedConfiguration admitted,
        GgufWeightFormat effectiveWeights) =>
        OptimizationCandidate.Create(
            GgufRouteConfiguration.Create(
                effectiveWeights, admitted.KvCache, admitted.Backend,
                admitted.Device, admitted.Offload),
            OptimizationCandidateMetrics.Create(
                EvidenceGrade.Estimated,
                OptimizationAssessment.Good,
                OptimizationAssessment.Good,
                OptimizationAssessment.Good,
                4096,
                8 * Gibibyte,
                32 * Gibibyte,
                24 * Gibibyte,
                0,
                0,
                requiresPersistentChange: false,
                availableDiskBytes: 500 * Gibibyte),
            admitted.EvidenceId,
            admitted.Level == SupportLevel.Experimental);

    private static OptimizationExecutionPayload RuntimeOnlyGgufPayload(
        string runtimeBuildId,
        string runtimeSourceCommit,
        GgufCacheType cache) =>
        OptimizationExecutionPayload.ForGguf(GgufExecutionPayload.Create(
            runtimeBuildId, runtimeSourceCommit, GgufRuntimeBackend.Cpu, "CPU",
            4096, cache, cache, 0, false, 4, 128, "Estimated", "profile",
            256, GgufWeightFormat.Imported));

    private static OpenVinoBuildIdentity Build() =>
        OpenVinoBuildIdentity.Create("2026.3.0", "2026.3.0.0", "2026.3.0", Digest64);

    private static TurboQuantBuildIdentity TurboBuild() =>
        TurboQuantBuildIdentity.Create(Commit40, Commit40, Digest64, OtherDigest64);

    private static Dictionary<string, string> Versions() => new(StringComparer.Ordinal)
    {
        ["nncf"] = "3.3.0",
        ["openvino"] = "2026.3.0"
    };

    private sealed class EmptyEnumeratingDictionary : IReadOnlyDictionary<string, string>
    {
        public int Count => 1;
        public IEnumerable<string> Keys => [];
        public IEnumerable<string> Values => [];
        public string this[string key] => throw new KeyNotFoundException();
        public bool ContainsKey(string key) => false;
        public bool TryGetValue(string key, out string value)
        {
            value = string.Empty;
            return false;
        }
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() =>
            Enumerable.Empty<KeyValuePair<string, string>>().GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
            GetEnumerator();
    }

    private sealed class EmptyEnumeratingProfileList :
        IReadOnlyList<GgufExecutionProfileAuthority>
    {
        public int Count => 1;
        public GgufExecutionProfileAuthority this[int index] =>
            throw new IndexOutOfRangeException();
        public IEnumerator<GgufExecutionProfileAuthority> GetEnumerator() =>
            Enumerable.Empty<GgufExecutionProfileAuthority>().GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
            GetEnumerator();
    }
}
