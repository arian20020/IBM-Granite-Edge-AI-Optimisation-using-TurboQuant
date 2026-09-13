using System.Reflection;
using GraniteEdgeAI.Features.ModelOptimization.Export;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.ModelOptimization;

[TestClass]
public sealed class OptimizationExportDestinationNamePolicyTests
{
    private const string Digest =
        "1111111111111111111111111111111111111111111111111111111111111111";
    private const string OtherDigest =
        "2222222222222222222222222222222222222222222222222222222222222222";
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    [TestMethod]
    [DataRow(OpenVinoWeightPrecision.Fp16, "FP16")]
    [DataRow(OpenVinoWeightPrecision.EightBit, "INT8")]
    [DataRow(OpenVinoWeightPrecision.FourBit, "INT4")]
    [DataRow(OpenVinoWeightPrecision.MxFp4, "MXFP4")]
    public void OpenVinoWeightUsesStableExecutionToken(
        OpenVinoWeightPrecision weight, string token)
    {
        OptimizationExecutionPlan plan = OpenVinoPlan(
            weight, OpenVinoKvCachePrecision.U8);
        string root = AbsentRoot();

        string? actual = OptimizationExportDestinationNamePolicy
            .CreateAbsentDestination(
                root, OptimizationRoute.OpenVino, plan, Result(plan));

        Assert.AreEqual(Path.Combine(root, $"OpenVINO-{token}-U8"), actual);
        Assert.IsFalse(Directory.Exists(root));
    }

    [TestMethod]
    [DataRow(OpenVinoKvCachePrecision.ReleasedDefault, "DEFAULT")]
    [DataRow(OpenVinoKvCachePrecision.U8, "U8")]
    [DataRow(OpenVinoKvCachePrecision.F16, "F16")]
    [DataRow(OpenVinoKvCachePrecision.Bf16, "BF16")]
    [DataRow(OpenVinoKvCachePrecision.U4, "U4")]
    [DataRow(OpenVinoKvCachePrecision.Tbq4, "TBQ4")]
    [DataRow(OpenVinoKvCachePrecision.Tbq3, "TBQ3")]
    public void OpenVinoCacheUsesStableExecutionToken(
        OpenVinoKvCachePrecision cache, string token)
    {
        OptimizationExecutionPlan plan = OpenVinoPlan(
            OpenVinoWeightPrecision.FourBit, cache);
        string root = AbsentRoot();

        string? actual = OptimizationExportDestinationNamePolicy
            .CreateAbsentDestination(
                root, OptimizationRoute.OpenVino, plan, Result(plan));

        Assert.AreEqual(Path.Combine(root, $"OpenVINO-INT4-{token}"), actual);
        Assert.IsFalse(Directory.Exists(root));
    }

    [TestMethod]
    public void ExistingFileAndDirectoryReceiveNextNumericSuffixWithoutMutation()
    {
        string root = AbsentRoot();
        Directory.CreateDirectory(root);
        string first = Path.Combine(root, "OpenVINO-INT4-TBQ3");
        string second = Path.Combine(root, "OpenVINO-INT4-TBQ3-2");
        Directory.CreateDirectory(first);
        File.WriteAllText(second, "existing export");
        OptimizationExecutionPlan plan = OpenVinoPlan(
            OpenVinoWeightPrecision.FourBit, OpenVinoKvCachePrecision.Tbq3);
        try
        {
            string? actual = OptimizationExportDestinationNamePolicy
                .CreateAbsentDestination(
                    root, OptimizationRoute.OpenVino, plan, Result(plan));

            Assert.AreEqual(Path.Combine(root, "OpenVINO-INT4-TBQ3-3"), actual);
            Assert.IsTrue(Directory.Exists(first));
            Assert.AreEqual("existing export", File.ReadAllText(second));
            Assert.IsFalse(File.Exists(actual));
            Assert.IsFalse(Directory.Exists(actual));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void CaseInsensitiveCollisionsAndAttemptBoundAreDeterministic()
    {
        OptimizationExecutionPlan plan = OpenVinoPlan(
            OpenVinoWeightPrecision.FourBit, OpenVinoKvCachePrecision.Tbq3);
        HashSet<string> existing = new(StringComparer.OrdinalIgnoreCase)
        {
            "openvino-int4-tbq3", "OPENVINO-INT4-TBQ3-2"
        };
        string? third = OptimizationExportDestinationNamePolicy
            .CreateAbsentDestination(
                "C:\\Exports", OptimizationRoute.OpenVino, plan, Result(plan),
                path => existing.Contains(Path.GetFileName(path)));
        int probes = 0;
        string? exhausted = OptimizationExportDestinationNamePolicy
            .CreateAbsentDestination(
                "C:\\Exports", OptimizationRoute.OpenVino, plan, Result(plan),
                _ =>
                {
                    probes++;
                    return true;
                });

        Assert.AreEqual(
            Path.Combine("C:\\Exports", "OpenVINO-INT4-TBQ3-3"), third);
        Assert.IsNull(exhausted);
        Assert.AreEqual(1000, probes);
    }

    [TestMethod]
    public void WrongRouteUnionOrResultIdentityFailsClosed()
    {
        string root = AbsentRoot();
        OptimizationExecutionPlan plan = OpenVinoPlan(
            OpenVinoWeightPrecision.FourBit, OpenVinoKvCachePrecision.Tbq3);
        OptimizationExecutionPlan other = OpenVinoPlan(
            OpenVinoWeightPrecision.FourBit, OpenVinoKvCachePrecision.Tbq3);
        OptimizationExecutionPlan gguf = GgufPlan();

        Assert.IsFalse(OptimizationExportDestinationNamePolicy.MatchesIssuedResult(
            OptimizationRoute.Gguf, plan, Result(plan)));
        Assert.IsNull(OptimizationExportDestinationNamePolicy.CreateAbsentDestination(
            root, OptimizationRoute.Gguf, plan, Result(plan)));
        Assert.IsNull(OptimizationExportDestinationNamePolicy.CreateAbsentDestination(
            root, OptimizationRoute.OpenVino, plan, Result(other)));
        Assert.IsNull(OptimizationExportDestinationNamePolicy.CreateAbsentDestination(
            root, OptimizationRoute.OpenVino, gguf, Result(gguf)));
        Assert.IsNull(OptimizationExportDestinationNamePolicy.CreateAbsentDestination(
            root, OptimizationRoute.OpenVino, null!, Result(plan)));
        Assert.IsNull(OptimizationExportDestinationNamePolicy.CreateAbsentDestination(
            root, OptimizationRoute.OpenVino, plan, null!));
        Assert.IsFalse(Directory.Exists(root));
    }

    [TestMethod]
    public void UndefinedExecutionFormatsFailClosed()
    {
        string root = AbsentRoot();
        OptimizationExecutionPlan weight = OpenVinoPlan(
            (OpenVinoWeightPrecision)999,
            OpenVinoKvCachePrecision.U8,
            malformed: true);
        OptimizationExecutionPlan cache = OpenVinoPlan(
            OpenVinoWeightPrecision.FourBit,
            (OpenVinoKvCachePrecision)999,
            malformed: true);

        Assert.IsNull(OptimizationExportDestinationNamePolicy.CreateAbsentDestination(
            root, OptimizationRoute.OpenVino, weight, Result(weight)));
        Assert.IsNull(OptimizationExportDestinationNamePolicy.CreateAbsentDestination(
            root, OptimizationRoute.OpenVino, cache, Result(cache)));
        Assert.IsFalse(Directory.Exists(root));
    }

    private static string AbsentRoot() => Path.Combine(
        Path.GetTempPath(), "granite-export-name-test-" + Guid.NewGuid().ToString("N"));

    private static OptimizationExecutionPlan OpenVinoPlan(
        OpenVinoWeightPrecision weight,
        OpenVinoKvCachePrecision cache,
        bool malformed = false)
    {
        OpenVinoExecutionPayload payload = malformed
            ? MalformedPayload(weight, cache)
            : Payload(weight, cache);
        return Plan(
            OpenVinoRouteConfiguration.Create(
                PlanningWeight(weight), PlanningCache(cache), DeviceRouteId.Cpu,
                OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Disabled, 1),
            OptimizationExecutionPayload.ForOpenVino(payload));
    }

    private static OptimizationExecutionPlan GgufPlan() => Plan(
        GgufRouteConfiguration.Create(
            GgufWeightFormat.Imported, GgufKvCacheFormat.F16,
            CompatibilityBackend.Cpu, DeviceRouteId.Cpu, GpuOffloadLevel.None),
        OptimizationExecutionPayload.ForGguf(GgufExecutionPayload.Create(
            "runtime", Commit, GgufRuntimeBackend.Cpu, "CPU", 4096,
            GgufCacheType.F16, GgufCacheType.F16, 0, false, 4, 128,
            "Estimated", "profile", 256, GgufWeightFormat.Imported)));

    private static OpenVinoExecutionPayload Payload(
        OpenVinoWeightPrecision weight, OpenVinoKvCachePrecision cache)
    {
        bool turbo = cache is OpenVinoKvCachePrecision.Tbq4
            or OpenVinoKvCachePrecision.Tbq3;
        return OpenVinoExecutionPayload.Create(
            "openvino.export-name-test", "CPU",
            turbo ? "Experimental" : "Released", "export-name-test",
            OpenVinoWeightPrecision.Fp16, weight, cache,
            false, true, false, true,
            OpenVinoBuildIdentity.Create(
                "2026.3.0", "2026.3.0.0", "2026.3.0", Digest),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["openvino"] = "2026.3.0"
            },
            turbo ? TurboQuantBuildIdentity.Create(
                Commit, Commit, Digest, OtherDigest) : null,
            turbo ? OpenVinoKvCacheAlgorithm.TurboQuant
                : OpenVinoKvCacheAlgorithm.Released);
    }

    private static OpenVinoExecutionPayload MalformedPayload(
        OpenVinoWeightPrecision weight, OpenVinoKvCachePrecision cache)
    {
        ConstructorInfo constructor = typeof(OpenVinoExecutionPayload)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(value => value.GetParameters().Length == 15);
        return (OpenVinoExecutionPayload)constructor.Invoke(
        [
            "openvino.export-name-test", "CPU", "Released", "export-name-test",
            OpenVinoWeightPrecision.Fp16, weight,
            OpenVinoKvCacheAlgorithm.Released, cache,
            false, true, false, true,
            OpenVinoBuildIdentity.Create(
                "2026.3.0", "2026.3.0.0", "2026.3.0", Digest),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["openvino"] = "2026.3.0"
            },
            null
        ]);
    }

    private static OpenVinoWeightFormat PlanningWeight(
        OpenVinoWeightPrecision value) => value switch
        {
            OpenVinoWeightPrecision.Fp16 => OpenVinoWeightFormat.Fp16,
            OpenVinoWeightPrecision.EightBit => OpenVinoWeightFormat.Int8,
            OpenVinoWeightPrecision.FourBit => OpenVinoWeightFormat.Int4,
            OpenVinoWeightPrecision.MxFp4 => OpenVinoWeightFormat.MxFp4,
            _ => OpenVinoWeightFormat.Int4
        };

    private static OpenVinoKvCacheFormat PlanningCache(
        OpenVinoKvCachePrecision value) => value switch
        {
            OpenVinoKvCachePrecision.ReleasedDefault => OpenVinoKvCacheFormat.RouteDefault,
            OpenVinoKvCachePrecision.U8 => OpenVinoKvCacheFormat.U8,
            OpenVinoKvCachePrecision.F16 => OpenVinoKvCacheFormat.F16,
            OpenVinoKvCachePrecision.Bf16 => OpenVinoKvCacheFormat.Bf16,
            OpenVinoKvCachePrecision.U4 => OpenVinoKvCacheFormat.U4,
            OpenVinoKvCachePrecision.Tbq4 => OpenVinoKvCacheFormat.TurboQuantTbq4,
            OpenVinoKvCachePrecision.Tbq3 => OpenVinoKvCacheFormat.TurboQuantTbq3,
            _ => OpenVinoKvCacheFormat.U8
        };

    private static OptimizationExecutionResult Result(OptimizationExecutionPlan plan) =>
        OptimizationExecutionResult.Succeeded(
            plan, "openvino-export-name-test", OtherDigest, 1024,
            sourceUnchanged: true, DateTimeOffset.UnixEpoch);

    private static OptimizationExecutionPlan Plan(
        RouteConfiguration configuration, OptimizationExecutionPayload payload)
    {
        OptimizationCandidate candidate = OptimizationCandidate.Create(
            configuration,
            OptimizationCandidateMetrics.Create(
                EvidenceGrade.Estimated, OptimizationAssessment.Good,
                OptimizationAssessment.Good, OptimizationAssessment.Good,
                4096, 2UL << 30, 4UL << 30, 2UL << 30, 0, 1024,
                requiresPersistentChange: true),
            "export-name-test", isExperimental: false);
        OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
            "22222222222242228222222222222222",
            "33333333333343338333333333333333",
            Digest, 2UL << 30,
            "44444444444444448444444444444444", Digest);
        OptimizationCapabilitySnapshot capability = configuration switch
        {
            OpenVinoRouteConfiguration => OptimizationCapabilitySnapshot.ForOpenVino(
                "openvino-capability", Digest,
                OpenVinoCapabilityPayload.Create(
                    "2026.3.0",
                    [OpenVinoAdmittedConfiguration.Create(
                        "openvino-standard-test", DeviceRouteId.Cpu,
                        OpenVinoWeightFormat.Int4, OpenVinoKvCacheFormat.U8,
                        OpenVinoPerformanceHint.Latency,
                        OpenVinoCompiledCachePolicy.Disabled, 1, 512, 32768,
                        SupportLevel.DeclaredSupported, false)])),
            GgufRouteConfiguration => OptimizationCapabilitySnapshot.ForGguf(
                "gguf-capability", Digest,
                GgufCapabilityPayload.Create(
                    "runtime",
                    [GgufAdmittedConfiguration.Create(
                        "gguf-standard-test", CompatibilityBackend.Cpu,
                        DeviceRouteId.Cpu, GgufWeightFormat.Imported,
                        GgufKvCacheFormat.F16, GpuOffloadLevel.None,
                        512, 32768, SupportLevel.DeclaredSupported, false)])),
            _ => throw new ArgumentOutOfRangeException(nameof(configuration))
        };
        OptimizationWorkload workload = OptimizationWorkload.Create(
            "chat", 512, OptimizationAssessment.Poor,
            [ContextTokenCount.FromTokens(4096)]);
        ConstructorInfo constructor = typeof(OptimizationExecutionPlan)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(value => value.GetParameters().Length == 11);
        return (OptimizationExecutionPlan)constructor.Invoke(
        [
            OptimizationExecutionPlan.CurrentContractVersion, Guid.NewGuid(), binding,
            capability, workload, candidate, payload,
            OptimizationPreferenceSelection.Automatic(), false, Digest,
            DateTimeOffset.UnixEpoch
        ]);
    }
}
