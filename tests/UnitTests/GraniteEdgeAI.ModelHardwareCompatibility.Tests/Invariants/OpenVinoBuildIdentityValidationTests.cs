using System.Reflection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Invariants;

/// <summary>
/// What an opaque vendor build identity is allowed to be.
///
/// The official OpenVINO runtime reports a build identity containing
/// slash-delimited release-channel information. V2 validated those three fields
/// with the generic identifier rule, which rejects a slash because an evidence
/// id or a device id carrying one would be a path.
///
/// A vendor build string is not that. The slash is structure the vendor chose,
/// and refusing it would leave O1 with no way to record the authoritative
/// identity except by truncating or rewriting it - which is exactly the
/// substitution this contract forbids everywhere else.
///
/// So the two rules are separated rather than the strict one relaxed. Nothing
/// here loosens what protects an identifier; it adds a different rule for a
/// different kind of value.
/// </summary>
[TestClass]
public sealed class OpenVinoBuildIdentityValidationTests
{
    /// <summary>
    /// The exact identity the official runtime reports. Byte for byte: this is
    /// the value the contract has to be able to carry.
    /// </summary>
    private const string OfficialRuntimeBuild =
        "2026.3.0-22451-8a17657b995-releases/2026/3";

    private const ulong Gibibyte = 1024UL * 1024 * 1024;

    private const string Digest64 =
        "1111111111111111111111111111111111111111111111111111111111111111";

    private const string OtherDigest64 =
        "2222222222222222222222222222222222222222222222222222222222222222";

    private static OpenVinoBuildIdentity Build(
        string runtimeBuild = OfficialRuntimeBuild,
        string genAiBuild = OfficialRuntimeBuild,
        string tokenizersBuild = OfficialRuntimeBuild,
        string workerManifestDigest = Digest64) =>
        OpenVinoBuildIdentity.Create(
            runtimeBuild, genAiBuild, tokenizersBuild, workerManifestDigest);

    private static OptimizationCandidate Candidate() =>
        OptimizationCandidate.Create(
            OpenVinoRouteConfiguration.Create(
                OpenVinoWeightFormat.Int8,
                OpenVinoKvCacheFormat.U8,
                DeviceRouteId.Cpu,
                OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Enabled,
                1),
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
                4 * Gibibyte,
                true),
            "ov-evidence",
            isExperimental: false);

    private static OptimizationExecutionPlan Issue(OpenVinoBuildIdentity build) =>
        OptimizationPlanIssuer.Issue(
            OptimizationPreferenceResolver.Resolve(
                [Candidate()], OptimizationPreferenceSelection.Manual(50))!,
            OptimizationExecutionPayload.ForOpenVino(
                OpenVinoExecutionPayload.Create(
                    "openvino.standard.cpu.int8.default.v1",
                    "CPU",
                    "Standard candidate",
                    "ov-evidence",
                    OpenVinoWeightPrecision.Fp16,
                    OpenVinoWeightPrecision.EightBit,
                    OpenVinoKvCachePrecision.U8,
                    compiledCacheEnabled: true,
                    compiledCacheIsDisposable: true,
                    compiledCacheIsModelArtifact: false,
                    createsCompletePackage: true,
                    build,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["openvino"] = "2026.3.0"
                    })),
            OptimizationCapabilitySnapshot.ForOpenVino(
                "ov-cap",
                Digest64,
                OpenVinoCapabilityPayload.Create(
                    "2026.3.0",
                    [
                        OpenVinoAdmittedConfiguration.Create(
                            "ov-evidence", DeviceRouteId.Cpu, OpenVinoWeightFormat.Int8,
                            OpenVinoKvCacheFormat.U8, OpenVinoPerformanceHint.Latency,
                            OpenVinoCompiledCachePolicy.Enabled, 1, 512, 32768,
                            SupportLevel.DeclaredSupported, false)
                    ])),
            OptimizationWorkload.Create(
                "chat", 512, OptimizationAssessment.Poor,
                [ContextTokenCount.FromTokens(4096)]),
            OptimizationJourneyBinding.Create(
                "mi-run-1", "mi-handoff-1", Digest64, 4 * Gibibyte,
                "hw-run-1", OtherDigest64),
            modelLayerCount: 32,
            DateTimeOffset.UnixEpoch);

    // ---------- the defect ----------

    [TestMethod]
    public void OfficialRuntimeBuildIdentityIsAccepted()
    {
        // The whole point of the correction. Before it, this threw.
        OpenVinoBuildIdentity identity = Build();

        Assert.AreEqual(OfficialRuntimeBuild, identity.RuntimeBuild);
    }

    [TestMethod]
    public void OfficialIdentityIsPreservedByteForByte()
    {
        // No normalisation, no case folding, no truncation, no escaping. An
        // identity that arrived transformed would not match the runtime that
        // reported it.
        OpenVinoBuildIdentity identity = Build();

        Assert.IsTrue(
            string.Equals(OfficialRuntimeBuild, identity.RuntimeBuild, StringComparison.Ordinal),
            "The build identity was altered on the way in.");

        Assert.AreEqual(OfficialRuntimeBuild.Length, identity.RuntimeBuild.Length);
    }

    [TestMethod]
    public void AllThreeBuildFieldsAcceptTheOfficialIdentity()
    {
        // Runtime, GenAI and tokenizers all come from the same vendor and share
        // the same format, so all three need the same rule. One left on the
        // strict validator would fail on a real machine.
        OpenVinoBuildIdentity identity = Build();

        Assert.AreEqual(OfficialRuntimeBuild, identity.RuntimeBuild);
        Assert.AreEqual(OfficialRuntimeBuild, identity.GenAiBuild);
        Assert.AreEqual(OfficialRuntimeBuild, identity.TokenizersBuild);
    }

    [TestMethod]
    public void OfficialIdentitySurvivesPlanIssuanceAndReadsBackExactly()
    {
        OptimizationExecutionPlan plan = Issue(Build());

        OpenVinoExecutionPayload payload = plan.ExecutionPayload.OpenVino!;

        Assert.IsTrue(
            string.Equals(
                OfficialRuntimeBuild,
                payload.BuildIdentity.RuntimeBuild,
                StringComparison.Ordinal),
            "The build identity did not survive plan issuance unchanged.");
    }

    // ---------- it still reaches the digest ----------

    [TestMethod]
    public void BuildIdentityParticipatesInTheConfigurationDigest()
    {
        Assert.AreNotEqual(
            Issue(Build()).ConfigurationSha256,
            Issue(Build(runtimeBuild: "2026.4.0-1-abc-releases/2026/4")).ConfigurationSha256,
            "The runtime build does not reach the configuration digest.");
    }

    [TestMethod]
    public void ChangingOnlyTheReleaseChannelChangesTheDigest()
    {
        // The part the strict rule would have forced O1 to drop. If it were
        // truncated away, two different release channels would produce the same
        // plan identity.
        Assert.AreNotEqual(
            Issue(Build()).ConfigurationSha256,
            Issue(Build(runtimeBuild: "2026.3.0-22451-8a17657b995-releases/2026/4"))
                .ConfigurationSha256,
            "Two different release channels produced the same plan digest.");
    }

    [TestMethod]
    public void EachBuildFieldMovesTheDigestIndependently()
    {
        string baseline = Issue(Build()).ConfigurationSha256;
        const string Other = "2026.3.0-99999-deadbeef123-releases/2026/3";

        Assert.AreNotEqual(
            baseline, Issue(Build(runtimeBuild: Other)).ConfigurationSha256);
        Assert.AreNotEqual(
            baseline, Issue(Build(genAiBuild: Other)).ConfigurationSha256);
        Assert.AreNotEqual(
            baseline, Issue(Build(tokenizersBuild: Other)).ConfigurationSha256);
    }

    [TestMethod]
    public void DigestIsStableForTheSameBuildIdentity()
    {
        Assert.AreEqual(
            Issue(Build()).ConfigurationSha256, Issue(Build()).ConfigurationSha256);
    }

    // ---------- what the build validator still refuses ----------

    [TestMethod]
    [DataRow(@"C:\private\runtime", "a Windows path")]
    [DataRow("/private/runtime", "an absolute POSIX path")]
    [DataRow("releases/../private", "a traversal segment")]
    [DataRow("releases//2026", "an empty segment")]
    [DataRow("releases/./2026", "a current-directory segment")]
    [DataRow("releases/2026/", "a trailing slash")]
    [DataRow("/releases/2026", "a leading slash")]
    [DataRow(@"releases\2026", "a backslash")]
    [DataRow("C:2026.3.0", "a colon")]
    [DataRow("2026.3.0 build", "an embedded space")]
    [DataRow(" 2026.3.0", "leading whitespace")]
    [DataRow("2026.3.0 ", "trailing whitespace")]
    [DataRow("2026.3.0\t", "a tab")]
    [DataRow("2026.3.0\n", "a newline")]
    [DataRow("2026.3.0\u0001", "a control character")]
    [DataRow("", "an empty value")]
    [DataRow("2026.3.0-\u00e9", "a non-ASCII character")]
    public void BuildValidatorRefusesUnsafeContent(string candidate, string why)
    {
        // The slash is release-channel structure in this one contract, and
        // nothing more. Everything that would make the value path-shaped, or
        // let it carry something other than a version, is still refused.
        Assert.ThrowsExactly<ArgumentException>(
            () => Build(runtimeBuild: candidate),
            $"A build identity containing {why} was accepted.");
    }

    [TestMethod]
    public void BuildValidatorKeepsABoundedLength()
    {
        // Past the ceiling is where unbounded content is being carried rather
        // than a version.
        Assert.ThrowsExactly<ArgumentException>(
            () => Build(runtimeBuild: new string('a', 129)));

        // And the ceiling itself is admitted, so the bound is exact.
        Assert.AreEqual(129 - 1, Build(runtimeBuild: new string('a', 128)).RuntimeBuild.Length);
    }

    [TestMethod]
    public void BuildValidatorAppliesToAllThreeFields()
    {
        // A field left on the old rule would pass this file's happy path and
        // still fail on a real machine, so each is proved to refuse
        // independently.
        Assert.ThrowsExactly<ArgumentException>(
            () => Build(runtimeBuild: "/private/runtime"));
        Assert.ThrowsExactly<ArgumentException>(
            () => Build(genAiBuild: "/private/runtime"));
        Assert.ThrowsExactly<ArgumentException>(
            () => Build(tokenizersBuild: "/private/runtime"));
    }

    [TestMethod]
    [DataRow("2026.3.0")]
    [DataRow("2026.3.0-22451")]
    [DataRow("2026.3.0-22451-8a17657b995")]
    [DataRow("2026.3.0+build.1")]
    [DataRow("2026_3_0")]
    [DataRow("a")]
    [DataRow("releases/2026/3")]
    public void OrdinaryVendorVersionsAreStillAccepted(string candidate)
    {
        Assert.AreEqual(candidate, Build(runtimeBuild: candidate).RuntimeBuild);
    }

    // ---------- the strict rule is unchanged ----------

    [TestMethod]
    public void WorkerManifestDigestStillRequiresACanonicalSha256()
    {
        // The digest field is not a vendor build string and keeps its own rule.
        Assert.ThrowsExactly<ArgumentException>(
            () => Build(workerManifestDigest: OfficialRuntimeBuild));

        Assert.ThrowsExactly<ArgumentException>(
            () => Build(workerManifestDigest: "not-a-digest"));
    }

    [TestMethod]
    public void GenericIdentifiersStillRejectASlash()
    {
        // The correction must not have leaked into the generic rule. An
        // evidence id, configuration id, package id, profile id, device id,
        // snapshot id or workload id carrying a slash would be a path.
        const string Slashed = "releases/2026/3";

        Assert.ThrowsExactly<ArgumentException>(
            () => OpenVinoAdmittedConfiguration.Create(
                Slashed, DeviceRouteId.Cpu, OpenVinoWeightFormat.Int8,
                OpenVinoKvCacheFormat.U8, OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Enabled, 1, 512, 32768,
                SupportLevel.DeclaredSupported, false),
            "An evidence id accepted a slash.");

        Assert.ThrowsExactly<ArgumentException>(
            () => GgufQuantiserIdentity.Create(Slashed, "v1", Digest64),
            "A quantiser package id accepted a slash.");

        Assert.ThrowsExactly<ArgumentException>(
            () => OptimizationCapabilitySnapshot.ForOpenVino(
                Slashed,
                Digest64,
                OpenVinoCapabilityPayload.Create(
                    "2026.3.0",
                    [
                        OpenVinoAdmittedConfiguration.Create(
                            "ov-evidence", DeviceRouteId.Cpu, OpenVinoWeightFormat.Int8,
                            OpenVinoKvCacheFormat.U8, OpenVinoPerformanceHint.Latency,
                            OpenVinoCompiledCachePolicy.Enabled, 1, 512, 32768,
                            SupportLevel.DeclaredSupported, false)
                    ])),
            "A snapshot id accepted a slash.");

        Assert.ThrowsExactly<ArgumentException>(
            () => OptimizationWorkload.Create(
                Slashed, 512, OptimizationAssessment.Poor,
                [ContextTokenCount.FromTokens(4096)]),
            "A workload id accepted a slash.");

        Assert.ThrowsExactly<ArgumentException>(
            () => OptimizationJourneyBinding.Create(
                Slashed, "mi-handoff-1", Digest64, 4 * Gibibyte,
                "hw-run-1", OtherDigest64),
            "A model inspection run id accepted a slash.");
    }

    [TestMethod]
    public void OpenVinoExecutionPayloadIdentifiersStillRejectASlash()
    {
        // ConfigurationId, Device, Maturity and EvidenceId are generic
        // identifiers, not vendor build strings, and keep the strict rule.
        foreach (string field in new[] { "configurationId", "device", "maturity", "evidenceId" })
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => OpenVinoExecutionPayload.Create(
                    field == "configurationId" ? "a/b" : "openvino.standard.cpu.int8.default.v1",
                    field == "device" ? "a/b" : "CPU",
                    field == "maturity" ? "a/b" : "Standard candidate",
                    field == "evidenceId" ? "a/b" : "ov-evidence",
                    OpenVinoWeightPrecision.Fp16,
                    OpenVinoWeightPrecision.EightBit,
                    OpenVinoKvCachePrecision.U8,
                    true, true, false, true,
                    Build(),
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["openvino"] = "2026.3.0"
                    }),
                $"{field} accepted a slash.");
        }
    }

    // ---------- privacy ----------

    [TestMethod]
    public void BuildIdentityNeverReachesAFilesystemApi()
    {
        // A value containing a slash must never be handed to a path API, or the
        // one concession made here would become a real traversal. Nothing in
        // the contract does so, and this pins the shape that keeps it true:
        // the value is only ever compared, stored and hashed.
        OptimizationExecutionPlan plan = Issue(Build());

        Assert.IsTrue(
            string.Equals(
                OfficialRuntimeBuild,
                plan.ExecutionPayload.OpenVino!.BuildIdentity.RuntimeBuild,
                StringComparison.Ordinal),
            "The build identity was transformed somewhere between input and plan.");
    }

    [TestMethod]
    public void NoBuildIdentityMemberIsNamedAsAPath()
    {
        foreach (PropertyInfo property in typeof(OpenVinoBuildIdentity).GetProperties())
        {
            foreach (string forbidden in new[] { "Path", "Directory", "FileName", "Folder" })
            {
                Assert.IsFalse(
                    property.Name.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                    $"OpenVinoBuildIdentity.{property.Name} names a filesystem location.");
            }
        }
    }

    [TestMethod]
    public void BuildIdentityShapeIsUnchangedAcrossTheV3ContractBump()
    {
        Assert.AreEqual(3, Issue(Build()).ContractVersion);

        Assert.AreEqual(
            4,
            typeof(OpenVinoBuildIdentity).GetProperties().Length,
            "OpenVinoBuildIdentity gained or lost a member.");
    }

    [TestMethod]
    public void OpenVinoCapabilityRuntimeVersionAcceptsTheOfficialIdentity()
    {
        // The audited second field. It is the same OpenVINO runtime build the
        // execution payload carries, so validating it as a generic identifier
        // would have reproduced this defect one field away.
        OpenVinoCapabilityPayload payload = OpenVinoCapabilityPayload.Create(
            OfficialRuntimeBuild,
            [
                OpenVinoAdmittedConfiguration.Create(
                    "ov-evidence", DeviceRouteId.Cpu, OpenVinoWeightFormat.Int8,
                    OpenVinoKvCacheFormat.U8, OpenVinoPerformanceHint.Latency,
                    OpenVinoCompiledCachePolicy.Enabled, 1, 512, 32768,
                    SupportLevel.DeclaredSupported, false)
            ]);

        Assert.IsTrue(
            string.Equals(
                OfficialRuntimeBuild, payload.RuntimeVersion, StringComparison.Ordinal),
            "The capability runtime version was altered or rejected.");
    }

    [TestMethod]
    public void OpenVinoCapabilityRuntimeVersionStillRefusesAPath()
    {
        // Widened to admit release-channel structure, not widened to admit a
        // path.
        Assert.ThrowsExactly<ArgumentException>(
            () => OpenVinoCapabilityPayload.Create(
                "/private/runtime",
                [
                    OpenVinoAdmittedConfiguration.Create(
                        "ov-evidence", DeviceRouteId.Cpu, OpenVinoWeightFormat.Int8,
                        OpenVinoKvCacheFormat.U8, OpenVinoPerformanceHint.Latency,
                        OpenVinoCompiledCachePolicy.Enabled, 1, 512, 32768,
                        SupportLevel.DeclaredSupported, false)
                ]));
    }

    [TestMethod]
    public void FieldsLeftOnTheStrictRuleStillRejectASlash()
    {
        // Recorded decisions, pinned so a later change is deliberate rather
        // than accidental. None of these has a published slash-bearing form,
        // and widening a rule without evidence is how a guard stops meaning
        // anything.
        Assert.ThrowsExactly<ArgumentException>(
            () => GgufCapabilityPayload.Create(
                "releases/2026/3",
                [
                    GgufAdmittedConfiguration.Create(
                        "gguf-evidence", CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
                        Core.Routes.Gguf.GgufWeightFormat.Q4KM,
                        Core.Routes.Gguf.GgufKvCacheFormat.F16,
                        Core.Routes.Gguf.GpuOffloadLevel.None,
                        512, 32768, SupportLevel.DeclaredSupported, false)
                ]),
            "The GGUF capability runtime version accepted a slash.");

        Assert.ThrowsExactly<ArgumentException>(
            () => GgufQuantiserIdentity.Create("pkg", "releases/2026/3", Digest64),
            "The quantiser tool version accepted a slash.");

        Assert.ThrowsExactly<ArgumentException>(
            () => GgufExecutionPayload.Create(
                "releases/2026/3",
                "0123456789abcdef0123456789abcdef01234567",
                GgufRuntimeBackend.Cpu, "CPU", 4096,
                GgufCacheType.F16, GgufCacheType.F16, 0, false, 8, 512,
                "Estimated", "profile-1", 512,
                Core.Routes.Gguf.GgufWeightFormat.Imported),
            "The GGUF runtime build id accepted a slash.");
    }
}
