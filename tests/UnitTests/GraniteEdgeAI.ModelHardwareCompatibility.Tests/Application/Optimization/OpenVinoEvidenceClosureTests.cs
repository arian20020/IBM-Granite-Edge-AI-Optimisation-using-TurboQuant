using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Optimization;

[TestClass]
public sealed class OpenVinoEvidenceClosureTests
{
    private const string Digest = "1111111111111111111111111111111111111111111111111111111111111111";
    private const string OtherDigest = "2222222222222222222222222222222222222222222222222222222222222222";
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";
    private const string OtherCommit = "1123456789abcdef0123456789abcdef01234567";
    private const ulong GiB = 1024UL * 1024 * 1024;

    [TestMethod]
    public void MatchingStandardPackageCannotBorrowAnotherEvidenceId()
    {
        OpenVinoExecutionAuthority execution = Authority();
        Assert.AreEqual(1, Generate(execution, Catalog(execution)).Candidates.Count);
        CrossRouteGenerationResult changed = Generate(execution, Catalog(execution, evidenceId: "different-evidence"));
        Assert.AreEqual(0, changed.Candidates.Count);
        Assert.AreEqual(OptimizationExclusionReason.EvidenceBelowAdmissionLevel, changed.Exclusions.Single().Reason);
    }

    [TestMethod]
    public void ExactEvidenceDoesNotDependOnOneHardCodedRuntimeVersion()
    {
        OpenVinoExecutionAuthority execution = Authority("runtime-2026.3");

        Assert.AreEqual(1, Generate(execution, Catalog(execution)).Candidates.Count);
    }

    [TestMethod]
    public void VerifiedExperimentMethodologyCanAuthorizeAnExactClosure()
    {
        OpenVinoExecutionAuthority execution = Authority();

        Assert.AreEqual(1, Generate(execution, Catalog(
            execution,
            methodologyIdentity:
                VerifiedOpenVinoOptimizationEvidence.MethodologyIdentity,
            memoryPerformanceProtocol:
                VerifiedOpenVinoOptimizationEvidence.MemoryPerformanceProtocol))
            .Candidates.Count);
    }

    [TestMethod]
    [DataRow("runtime")]
    [DataRow("genai")]
    [DataRow("tokenizers")]
    [DataRow("worker")]
    [DataRow("optimizer-version")]
    [DataRow("optimizer-name")]
    [DataRow("optimizer-extra")]
    [DataRow("optimizer-missing")]
    public void ChangingAnyStandardExecutionClosureFieldRejectsTheOriginalEvidence(string mutation)
    {
        OptimizationEvidenceCatalog catalog = Catalog(Authority());
        Assert.AreEqual(1, Generate(Authority(), catalog).Candidates.Count);
        CrossRouteGenerationResult mutated = Generate(Authority(mutation), catalog);
        Assert.AreEqual(0, mutated.Candidates.Count, mutation);
        Assert.AreEqual(OptimizationExclusionReason.EvidenceBelowAdmissionLevel, mutated.Exclusions.Single().Reason);
    }

    [TestMethod]
    [DataRow(OpenVinoKvCacheFormat.TurboQuantTbq3, false)]
    [DataRow(OpenVinoKvCacheFormat.TurboQuantTbq3, true)]
    [DataRow(OpenVinoKvCacheFormat.TurboQuantTbq4, false)]
    [DataRow(OpenVinoKvCacheFormat.TurboQuantTbq4, true)]
    public void TurboQuantRequiresRepresentableActualActivationAuthority(OpenVinoKvCacheFormat cache, bool buildPresent)
    {
        OpenVinoExecutionAuthority execution = Authority(cache: cache, tqPresent: buildPresent);
        if (!buildPresent)
        {
            Assert.Throws<ArgumentException>(() => Generate(execution, Catalog(execution, cache), cache));
            return;
        }
        CrossRouteGenerationResult generated = Generate(execution, Catalog(execution, cache), cache);
        Assert.AreEqual(1, generated.Candidates.Count);
        Assert.AreEqual(cache,
            ((OpenVinoRouteConfiguration)generated.Candidates.Single().Configuration).KvCache);
    }

    [TestMethod]
    [DataRow("tq-source")]
    [DataRow("tq-implementation")]
    [DataRow("tq-patch")]
    [DataRow("tq-runtime")]
    public void PackageIdentitySeparatesEveryTurboQuantBuildField(string mutation)
    {
        Assert.AreNotEqual(CrossRouteCandidateGenerator.OpenVinoEvidencePackageIdentity(Authority(tqPresent: true)),
            CrossRouteCandidateGenerator.OpenVinoEvidencePackageIdentity(Authority(mutation, tqPresent: true)));
    }

    [TestMethod]
    public void PackageIdentitySortsOptimizerNamesAndSeparatesTurboQuantPresence()
    {
        OpenVinoExecutionAuthority original = Authority();
        OpenVinoExecutionAuthority reordered = OpenVinoExecutionAuthority.Create(original.EvidenceId,
            original.ConfigurationId, original.SourceWeightPrecision, original.BuildIdentity,
            original.OptimizerVersions.Reverse().ToDictionary(pair => pair.Key, pair => pair.Value), true);
        Assert.AreEqual(CrossRouteCandidateGenerator.OpenVinoEvidencePackageIdentity(original),
            CrossRouteCandidateGenerator.OpenVinoEvidencePackageIdentity(reordered));
        Assert.AreNotEqual(CrossRouteCandidateGenerator.OpenVinoEvidencePackageIdentity(original),
            CrossRouteCandidateGenerator.OpenVinoEvidencePackageIdentity(Authority(tqPresent: true)));
    }

    internal static OpenVinoExecutionAuthority Authority(string mutation = "",
        OpenVinoKvCacheFormat cache = OpenVinoKvCacheFormat.U8, bool tqPresent = false)
    {
        Dictionary<string, string> versions = new(StringComparer.Ordinal)
        {
            [mutation == "optimizer-name" ? "other-optimizer" : "openvino"] = mutation == "optimizer-version" ? "different" : "2026.5.0",
            ["nncf"] = "3.0.0"
        };
        if (mutation == "optimizer-extra") versions.Add("extra", "1.0");
        if (mutation == "optimizer-missing") versions.Remove("nncf");
        string runtimeBuild = mutation switch
        {
            "runtime" => "other-runtime",
            "runtime-2026.3" => "2026.3.0-22451-8a17657b995-releases/2026/3",
            _ => SyntheticQualityEvidence.OpenVinoBuild
        };
        return OpenVinoExecutionAuthority.Create("closure-evidence", "closure-config", OpenVinoWeightPrecision.FourBit,
            OpenVinoBuildIdentity.Create(runtimeBuild,
                mutation == "genai" ? "other-genai" : "2026.5.0",
                mutation == "tokenizers" ? "other-tokenizers" : "2026.5.0",
                mutation == "worker" ? OtherDigest : Digest), versions, true,
            tqPresent ? TurboQuantBuildIdentity.Create(mutation == "tq-source" ? OtherCommit : Commit,
                mutation == "tq-implementation" ? OtherCommit : Commit,
                mutation == "tq-patch" ? OtherDigest : Digest,
                mutation == "tq-runtime" ? OtherDigest : Digest) : null);
    }

    private static OptimizationEvidenceCatalog Catalog(OpenVinoExecutionAuthority execution,
        OpenVinoKvCacheFormat cache = OpenVinoKvCacheFormat.U8,
        string? evidenceId = null,
        string? methodologyIdentity = null,
        string? memoryPerformanceProtocol = null)
    {
        OptimizationEvidenceRecord template = PublishedOpenVinoOptimizationEvidence.Records().First();
        return new([template with
        {
            EvidenceId = evidenceId ?? execution.EvidenceId,
            Key = template.Key with
            {
                RuntimePackageIdentity = CrossRouteCandidateGenerator.OpenVinoEvidencePackageIdentity(execution),
                CacheConfiguration = cache switch { OpenVinoKvCacheFormat.TurboQuantTbq3 => "tbq3", OpenVinoKvCacheFormat.TurboQuantTbq4 => "tbq4", _ => "u8" },
                MethodologyIdentity = methodologyIdentity ??
                    PublishedOpenVinoOptimizationEvidence.MethodologyIdentity,
                MemoryPerformanceProtocol = memoryPerformanceProtocol ??
                    PublishedOpenVinoOptimizationEvidence.MemoryPerformanceProtocol
            }
        }]);
    }

    private static CrossRouteGenerationResult Generate(OpenVinoExecutionAuthority execution,
        OptimizationEvidenceCatalog catalog, OpenVinoKvCacheFormat cache = OpenVinoKvCacheFormat.U8)
    {
        OpenVinoAdmittedConfiguration entry = OpenVinoAdmittedConfiguration.Create(execution.EvidenceId,
            DeviceRouteId.Cpu, OpenVinoWeightFormat.Int4, cache, OpenVinoPerformanceHint.Latency,
            OpenVinoCompiledCachePolicy.Disabled, 1, 4096, 4096, SupportLevel.Experimental, true);
        OptimizationCapabilitySnapshot snapshot = OptimizationCapabilitySnapshot.ForOpenVino("closure-test", Digest,
            OpenVinoCapabilityPayload.Create(execution.BuildIdentity.RuntimeBuild, [entry], [execution]));
        OptimizationEvidenceRecord template = PublishedOpenVinoOptimizationEvidence.Records().First();
        return CrossRouteCandidateGenerator.Generate(snapshot,
            InspectedModelFacts.Create(ByteCount.FromBytes(GiB), 32, 4096, 32, 8, 4096, null, null),
            OptimizationWorkload.Create("local-chat", 4096, OptimizationAssessment.Acceptable, [ContextTokenCount.FromTokens(4096)]),
            OptimizationJourneyBinding.Create("mi-run", "mi-handoff", template.Key.ModelIdentitySha256, GiB, "hw-run", Digest),
            ByteCount.FromBytes(32 * GiB), ByteCount.FromBytes(500 * GiB), EstimatorPolicy.ProvisionalV1(),
            new HashSet<string> { execution.EvidenceId }, OptimizationHardwareAuthorityTestData.AllEstablished(), catalog, 3_000_000_000);
    }
}
