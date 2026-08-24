using GraniteEdgeAI.Features.OpenVinoRoute.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using ContractCompiledCachePolicy = GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino.OpenVinoCompiledCachePolicy;
using RouteCompiledCachePolicy = GraniteEdgeAI.Features.OpenVinoRoute.Optimization.OpenVinoCompiledCachePolicy;

namespace GraniteEdgeAI.OpenVino.Tests.Optimization;

[TestClass]
[TestCategory("OpenVinoRoute")]
public sealed class OpenVinoOptimizationCapabilityProjectorTests
{
    [TestMethod]
    public void ProjectorReportsAdmittedEvidenceWithoutSelectingAMode()
    {
        OpenVinoCapabilityPayload payload =
            OpenVinoOptimizationCapabilityProjector.Project(
                OpenVinoCapabilityTestData.StandardCpuEvidence());

        Assert.AreEqual(5, payload.Admitted.Count);
        Assert.IsTrue(payload.Admitted.All(static admission =>
            !string.IsNullOrWhiteSpace(admission.EvidenceId)));
        Assert.IsNull(typeof(OpenVinoOptimizationCapabilityAdmission)
            .GetProperty("Objective"));
        Assert.IsNull(typeof(OpenVinoOptimizationCapabilityAdmission)
            .GetProperty("Preference"));
    }

    [TestMethod]
    public void ProjectorPreservesExactReleasedCpuCapabilityEvidence()
    {
        OpenVinoCapabilityPayload payload =
            OpenVinoOptimizationCapabilityProjector.Project(
                OpenVinoCapabilityTestData.StandardCpuEvidence());

        Assert.AreEqual(
            "openvino-2026.3.0_openvino-genai-2026.3.0.0_nncf-3.3.0_"
            + "optimum-2.3.0_optimum-intel-2.1.0_transformers-5.5.4",
            payload.RuntimeVersion);

        (string EvidenceId, OpenVinoWeightFormat Weights,
            OpenVinoKvCacheFormat KvCache,
            ContractCompiledCachePolicy CompiledCache)[] expected =
        [
            ("OV-STD-CPU-ORIGINAL-01", OpenVinoWeightFormat.Original,
                OpenVinoKvCacheFormat.RouteDefault,
                ContractCompiledCachePolicy.Disabled),
            ("OV-STD-CPU-FP16-01", OpenVinoWeightFormat.Fp16,
                OpenVinoKvCacheFormat.RouteDefault,
                ContractCompiledCachePolicy.Disabled),
            ("OV-STD-CPU-AUTO-01", OpenVinoWeightFormat.Int8,
                OpenVinoKvCacheFormat.RouteDefault,
                ContractCompiledCachePolicy.Disabled),
            ("OV-STD-CPU-INT8-U8-01", OpenVinoWeightFormat.Int8,
                OpenVinoKvCacheFormat.U8,
                ContractCompiledCachePolicy.Disabled),
            ("OV-STD-CPU-INT4-U8-01", OpenVinoWeightFormat.Int4,
                OpenVinoKvCacheFormat.U8,
                ContractCompiledCachePolicy.Disabled)
        ];

        for (int index = 0; index < expected.Length; index++)
        {
            OpenVinoAdmittedConfiguration actual = payload.Admitted[index];
            Assert.AreEqual(expected[index].EvidenceId, actual.EvidenceId);
            Assert.AreEqual(expected[index].Weights, actual.Weights);
            Assert.AreEqual(expected[index].KvCache, actual.KvCache);
            Assert.AreEqual(expected[index].CompiledCache, actual.CompiledCache);
            Assert.AreEqual(DeviceRouteId.Cpu, actual.Device);
            Assert.AreEqual(OpenVinoPerformanceHint.Latency, actual.PerformanceHint);
            Assert.AreEqual(1, actual.Streams);
            Assert.AreEqual(4_096, actual.MinimumContextTokens);
            Assert.AreEqual(4_096, actual.MaximumContextTokens);
            Assert.AreEqual(SupportLevel.DeclaredSupported, actual.Level);
            Assert.IsFalse(actual.RequiresEvidence);
        }
    }

    [TestMethod]
    public void ProjectorNarrowsReleasedContextEvidenceToTheExecutorContext()
    {
        OpenVinoOptimizationCapabilityEvidence evidence =
            OpenVinoCapabilityTestData.StandardCpuEvidence();

        OpenVinoCapabilityPayload payload =
            OpenVinoOptimizationCapabilityProjector.Project(evidence);

        Assert.IsTrue(payload.Admitted.All(static admission =>
            admission.MinimumContextTokens == 4_096 &&
            admission.MaximumContextTokens == 4_096));
    }

    [TestMethod]
    [DataRow("compiled-cache")]
    [DataRow("streams")]
    [DataRow("context")]
    public void ProjectorOmitsReleasedAdmissionsOutsideTheExecutorEnvelope(
        string field)
    {
        OpenVinoOptimizationCapabilityEvidence evidence =
            OpenVinoCapabilityTestData.WithMutatedFirstAdmission(
                admission => field switch
                {
                    "compiled-cache" => admission with
                    {
                        Runtime = new OpenVinoRuntimeOptimization(
                            admission.Runtime.KvCachePrecision,
                            RouteCompiledCachePolicy.Disposable)
                    },
                    "streams" => admission with { Streams = 2 },
                    "context" => admission with
                    {
                        MinimumContextTokens = 512,
                        MaximumContextTokens = 2_048
                    },
                    _ => throw new AssertFailedException(field)
                });

        OpenVinoCapabilityPayload payload =
            OpenVinoOptimizationCapabilityProjector.Project(evidence);

        Assert.AreEqual(4, payload.Admitted.Count);
        Assert.IsFalse(payload.Admitted.Any(static admission =>
            admission.EvidenceId == "OV-STD-CPU-ORIGINAL-01"));
    }

    [TestMethod]
    public void EveryReleasedProjectionIsAcceptedByTheSealedExecutorEnvelope()
    {
        OpenVinoCapabilityPayload payload =
            OpenVinoOptimizationCapabilityProjector.Project(
                OpenVinoCapabilityTestData.StandardCpuEvidence());

        foreach (OpenVinoAdmittedConfiguration admission in payload.Admitted)
        {
            OpenVinoOptimizationCandidate candidate =
                OpenVinoCapabilityTestData.AdaptedCandidate(payload, admission);

            Assert.IsTrue(OpenVinoRuntimeTechnicalConfiguration
                .From(candidate).IsSupported, admission.EvidenceId);
        }
    }

    [TestMethod]
    public void OriginalEvidenceDoesNotWidenThePersistentExecutor()
    {
        Assert.ThrowsExactly<OpenVinoOptimizationException>(() =>
            OpenVinoPersistentArtifact.Create(OpenVinoWeightPrecision.Original));
    }

    [TestMethod]
    [DataRow(nameof(OpenVinoOptimizationToolVersions.OpenVino))]
    [DataRow(nameof(OpenVinoOptimizationToolVersions.OpenVinoGenAi))]
    [DataRow(nameof(OpenVinoOptimizationToolVersions.Nncf))]
    [DataRow(nameof(OpenVinoOptimizationToolVersions.Optimum))]
    [DataRow(nameof(OpenVinoOptimizationToolVersions.OptimumIntel))]
    [DataRow(nameof(OpenVinoOptimizationToolVersions.Transformers))]
    public void AChangedPinnedToolVersionFailsClosed(string tool)
    {
        OpenVinoOptimizationCapabilityEvidence evidence =
            OpenVinoCapabilityTestData.StandardCpuEvidence();

        Assert.ThrowsExactly<ArgumentException>(() =>
            OpenVinoOptimizationCapabilityProjector.Project(evidence with
            {
                Versions = OpenVinoCapabilityTestData.WithToolVersion(
                    evidence.Versions, tool, "9.9.9")
            }));
    }

    [TestMethod]
    [DataRow(nameof(OpenVinoOptimizationToolVersions.OpenVino), "")]
    [DataRow(nameof(OpenVinoOptimizationToolVersions.OpenVino), "2026/3")]
    [DataRow(nameof(OpenVinoOptimizationToolVersions.OpenVinoGenAi), "")]
    [DataRow(nameof(OpenVinoOptimizationToolVersions.OpenVinoGenAi), "2026/3")]
    [DataRow(nameof(OpenVinoOptimizationToolVersions.Nncf), "")]
    [DataRow(nameof(OpenVinoOptimizationToolVersions.Nncf), "3/3")]
    [DataRow(nameof(OpenVinoOptimizationToolVersions.Optimum), "")]
    [DataRow(nameof(OpenVinoOptimizationToolVersions.Optimum), "2/3")]
    [DataRow(nameof(OpenVinoOptimizationToolVersions.OptimumIntel), "")]
    [DataRow(nameof(OpenVinoOptimizationToolVersions.OptimumIntel), "2/1")]
    [DataRow(nameof(OpenVinoOptimizationToolVersions.Transformers), "")]
    [DataRow(nameof(OpenVinoOptimizationToolVersions.Transformers), "5/5")]
    public void MissingOrMalformedToolVersionFailsClosed(string tool, string value)
    {
        OpenVinoOptimizationCapabilityEvidence evidence =
            OpenVinoCapabilityTestData.StandardCpuEvidence();

        Assert.ThrowsExactly<ArgumentException>(() =>
            OpenVinoOptimizationCapabilityProjector.Project(evidence with
            {
                Versions = OpenVinoCapabilityTestData.WithToolVersion(
                    evidence.Versions, tool, value)
            }));
    }

    [TestMethod]
    [DataRow("device")]
    [DataRow("weights")]
    [DataRow("kv-cache")]
    [DataRow("performance-hint")]
    [DataRow("maturity")]
    public void UnknownAdmissionValueFailsClosed(string field)
    {
        OpenVinoOptimizationCapabilityEvidence evidence =
            OpenVinoCapabilityTestData.WithFirstAdmission(field);

        Assert.ThrowsExactly<OpenVinoOptimizationException>(() =>
            OpenVinoOptimizationCapabilityProjector.Project(evidence));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("../evidence")]
    public void MissingOrMalformedEvidenceIdFailsClosed(string evidenceId)
    {
        OpenVinoOptimizationCapabilityEvidence evidence =
            OpenVinoCapabilityTestData.WithFirstAdmission(
                admission => admission with { EvidenceId = evidenceId });

        Assert.ThrowsExactly<ArgumentException>(() =>
            OpenVinoOptimizationCapabilityProjector.Project(evidence));
    }

    [TestMethod]
    [DataRow("runtime")]
    [DataRow("compiled-cache")]
    public void MissingRuntimeEvidenceFailsClosed(string field)
    {
        OpenVinoOptimizationCapabilityEvidence evidence =
            OpenVinoCapabilityTestData.WithFirstAdmission(field);

        Assert.ThrowsExactly<OpenVinoOptimizationException>(() =>
            OpenVinoOptimizationCapabilityProjector.Project(evidence));
    }

    [TestMethod]
    public void EmptyAdmissionsFailClosed()
    {
        OpenVinoOptimizationCapabilityEvidence evidence =
            OpenVinoCapabilityTestData.StandardCpuEvidence() with { Admitted = [] };

        Assert.ThrowsExactly<ArgumentException>(() =>
            OpenVinoOptimizationCapabilityProjector.Project(evidence));
    }

    [TestMethod]
    [DataRow(0, 4_096)]
    [DataRow(4_096, 512)]
    public void InvalidContextBoundsFailClosed(int minimum, int maximum)
    {
        OpenVinoOptimizationCapabilityEvidence evidence =
            OpenVinoCapabilityTestData.WithFirstAdmission(admission => admission with
            {
                MinimumContextTokens = minimum,
                MaximumContextTokens = maximum
            });

        Assert.ThrowsExactly<ArgumentException>(() =>
            OpenVinoOptimizationCapabilityProjector.Project(evidence));
    }

    [TestMethod]
    public void ProjectedPayloadDoesNotRetainTheCallerAdmissionCollection()
    {
        OpenVinoOptimizationCapabilityEvidence standard =
            OpenVinoCapabilityTestData.StandardCpuEvidence();
        List<OpenVinoOptimizationCapabilityAdmission> callerAdmissions =
            [.. standard.Admitted];
        OpenVinoCapabilityPayload payload =
            OpenVinoOptimizationCapabilityProjector.Project(
                standard with { Admitted = callerAdmissions });

        callerAdmissions.Clear();

        Assert.AreEqual(5, payload.Admitted.Count);
        Assert.AreEqual("OV-STD-CPU-ORIGINAL-01", payload.Admitted[0].EvidenceId);
        Assert.AreEqual("OV-STD-CPU-INT4-U8-01", payload.Admitted[4].EvidenceId);
    }

    private static class OpenVinoCapabilityTestData
    {
        internal static OpenVinoOptimizationCapabilityEvidence StandardCpuEvidence() =>
            new(
                new OpenVinoOptimizationToolVersions(
                    OpenVino: "2026.3.0",
                    OpenVinoGenAi: "2026.3.0.0",
                    Nncf: "3.3.0",
                    Optimum: "2.3.0",
                    OptimumIntel: "2.1.0",
                    Transformers: "5.5.4"),
                [
                    Admission(
                        "OV-STD-CPU-ORIGINAL-01",
                        OpenVinoWeightPrecision.Original,
                        OpenVinoKvCachePrecision.ReleasedDefault,
                        compiledCache: false),
                    Admission(
                        "OV-STD-CPU-FP16-01",
                        OpenVinoWeightPrecision.Fp16,
                        OpenVinoKvCachePrecision.ReleasedDefault,
                        compiledCache: false),
                    Admission(
                        "OV-STD-CPU-AUTO-01",
                        OpenVinoWeightPrecision.EightBit,
                        OpenVinoKvCachePrecision.ReleasedDefault,
                        compiledCache: false),
                    Admission(
                        "OV-STD-CPU-INT8-U8-01",
                        OpenVinoWeightPrecision.EightBit,
                        OpenVinoKvCachePrecision.U8,
                        compiledCache: false),
                    Admission(
                        "OV-STD-CPU-INT4-U8-01",
                        OpenVinoWeightPrecision.FourBit,
                        OpenVinoKvCachePrecision.U8,
                        compiledCache: false)
                ]);

        private static OpenVinoOptimizationCapabilityAdmission Admission(
            string evidenceId,
            OpenVinoWeightPrecision weights,
            OpenVinoKvCachePrecision kvCache,
            bool compiledCache) =>
            new(
                evidenceId,
                Device: "CPU",
                weights,
                new OpenVinoRuntimeOptimization(
                    kvCache,
                    compiledCache
                        ? RouteCompiledCachePolicy.Disposable
                        : RouteCompiledCachePolicy.Disabled),
                OpenVinoCapabilityPerformanceHint.Latency,
                Streams: 1,
                MinimumContextTokens: 512,
                MaximumContextTokens: 4_096,
                OpenVinoCapabilityMaturity.Released);

        internal static OpenVinoOptimizationCandidate AdaptedCandidate(
            OpenVinoCapabilityPayload payload,
            OpenVinoAdmittedConfiguration admission)
        {
            const string sourceDigest =
                "1111111111111111111111111111111111111111111111111111111111111111";
            const ulong sourceLength = 4UL * 1024 * 1024 * 1024;
            bool persistent = admission.Weights != OpenVinoWeightFormat.Original;
            OpenVinoRouteConfiguration configuration =
                OpenVinoRouteConfiguration.Create(
                    admission.Weights,
                    admission.KvCache,
                    admission.Device,
                    admission.PerformanceHint,
                    admission.CompiledCache,
                    admission.Streams);
            OptimizationCandidate candidate = OptimizationCandidate.Create(
                configuration,
                OptimizationCandidateMetrics.Create(
                    EvidenceGrade.Estimated,
                    OptimizationAssessment.Good,
                    OptimizationAssessment.Good,
                    OptimizationAssessment.Good,
                    4_096,
                    2UL * 1024 * 1024 * 1024,
                    8UL * 1024 * 1024 * 1024,
                    6UL * 1024 * 1024 * 1024,
                    persistent ? sourceLength : 0,
                    persistent ? sourceLength / 2 : 0,
                    persistent),
                admission.EvidenceId,
                isExperimental: false);
            OptimizationCapabilitySnapshot snapshot =
                OptimizationCapabilitySnapshot.ForOpenVino(
                    "ov-projector-parity",
                    new string('3', 64),
                    payload);
            OptimizationSelection selection = OptimizationPreferenceResolver.Resolve(
                [candidate], OptimizationPreferenceSelection.Manual(50))!;
            OptimizationExecutionPlan plan = OptimizationPlanIssuer.Issue(
                selection,
                snapshot,
                OptimizationWorkload.Create(
                    "chat",
                    512,
                    OptimizationAssessment.Poor,
                    [ContextTokenCount.FromTokens(4_096)]),
                OptimizationJourneyBinding.Create(
                    "mi-run-1",
                    "mi-handoff-1",
                    sourceDigest,
                    sourceLength,
                    "hw-run-1",
                    new string('2', 64)),
                DateTimeOffset.UnixEpoch);
            OpenVinoOptimizationAdaptation adaptation =
                OpenVinoOptimizationPlanAdapter.Adapt(
                    plan, snapshot, sourceDigest, sourceLength);

            Assert.AreEqual(OpenVinoOptimizationAdaptationStatus.Ready,
                adaptation.Status, admission.EvidenceId);
            return adaptation.Candidate!;
        }

        internal static OpenVinoOptimizationToolVersions WithToolVersion(
            OpenVinoOptimizationToolVersions versions,
            string tool,
            string value) =>
            tool switch
            {
                nameof(OpenVinoOptimizationToolVersions.OpenVino) =>
                    versions with { OpenVino = value },
                nameof(OpenVinoOptimizationToolVersions.OpenVinoGenAi) =>
                    versions with { OpenVinoGenAi = value },
                nameof(OpenVinoOptimizationToolVersions.Nncf) =>
                    versions with { Nncf = value },
                nameof(OpenVinoOptimizationToolVersions.Optimum) =>
                    versions with { Optimum = value },
                nameof(OpenVinoOptimizationToolVersions.OptimumIntel) =>
                    versions with { OptimumIntel = value },
                nameof(OpenVinoOptimizationToolVersions.Transformers) =>
                    versions with { Transformers = value },
                _ => throw new AssertFailedException(tool)
            };

        internal static OpenVinoOptimizationCapabilityEvidence WithFirstAdmission(
            string field) =>
            WithFirstAdmission(admission => field switch
            {
                "device" => admission with { Device = "GPU" },
                "weights" => admission with
                {
                    WeightPrecision = (OpenVinoWeightPrecision)int.MaxValue
                },
                "kv-cache" => admission with
                {
                    Runtime = new OpenVinoRuntimeOptimization(
                        (OpenVinoKvCachePrecision)int.MaxValue,
                        RouteCompiledCachePolicy.Disabled)
                },
                "performance-hint" => admission with
                {
                    PerformanceHint = (OpenVinoCapabilityPerformanceHint)int.MaxValue
                },
                "maturity" => admission with
                {
                    Maturity = (OpenVinoCapabilityMaturity)int.MaxValue
                },
                "runtime" => admission with { Runtime = null! },
                "compiled-cache" => admission with
                {
                    Runtime = new OpenVinoRuntimeOptimization(
                        OpenVinoKvCachePrecision.ReleasedDefault, null!)
                },
                _ => throw new AssertFailedException(field)
            });

        internal static OpenVinoOptimizationCapabilityEvidence WithFirstAdmission(
            Func<OpenVinoOptimizationCapabilityAdmission,
                OpenVinoOptimizationCapabilityAdmission> mutate)
        {
            OpenVinoOptimizationCapabilityEvidence evidence = StandardCpuEvidence();
            return evidence with { Admitted = [mutate(evidence.Admitted[0])] };
        }

        internal static OpenVinoOptimizationCapabilityEvidence
            WithMutatedFirstAdmission(
                Func<OpenVinoOptimizationCapabilityAdmission,
                    OpenVinoOptimizationCapabilityAdmission> mutate)
        {
            OpenVinoOptimizationCapabilityEvidence evidence = StandardCpuEvidence();
            return evidence with
            {
                Admitted =
                [
                    mutate(evidence.Admitted[0]),
                    .. evidence.Admitted.Skip(1)
                ]
            };
        }
    }
}
