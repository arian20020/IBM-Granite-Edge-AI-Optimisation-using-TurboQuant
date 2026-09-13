using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

namespace GraniteEdgeAI.CrossFeature.IntegrationTests;

[TestClass]
public sealed class ProductionCompatibilityBindingIntegrationTests
{
    private const ulong GiB = 1024UL * 1024 * 1024;
    private const string Digest =
        "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
    private const string OtherDigest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string SyntheticRuntimeBuild = "2026.5.0-22950-f5f594dc0c9";

    [TestMethod]
    public void CurrentFitRetainsAnExecutableOptionalOptimization()
    {
        DateTimeOffset now = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

        CompatibilityEvaluation evaluation = CompatibilityEngine.EvaluateProduction(
            CreateInput(now), new FixedTimeProvider(now));

        Assert.AreEqual(
            CompatibilityScreenState.EstimatedCompatible,
            evaluation.Screen.State);
        Assert.IsTrue(evaluation.Screen.UseCurrentModelAvailable);
        Assert.IsNotNull(evaluation.OptionalOptimization);
        Assert.IsNotNull(evaluation.PlanningSession);
        Assert.AreEqual(OptimizationRoute.OpenVino, evaluation.PlanningSession.Route);
    }

    [TestMethod]
    [DataRow("model")]
    [DataRow("hardware")]
    public void CompatibilityAuthorityRejectsCurrentIdentityMutation(string identity)
    {
        DateTimeOffset now = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

        Assert.ThrowsExactly<ArgumentException>(() =>
            CreateInput(
                now,
                bindingModelDigest: identity == "model" ? OtherDigest : Digest,
                bindingHardwareDigest:
                    identity == "hardware" ? OtherDigest : Digest));
    }

    [TestMethod]
    public void StaleMemoryObservationCannotIssuePlanningOrExecutionAuthority()
    {
        DateTimeOffset now = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);
        CompatibilityEvaluation evaluation = CompatibilityEngine.EvaluateProduction(
            CreateInput(now - TimeSpan.FromSeconds(30)
                - TimeSpan.FromTicks(1)),
            new FixedTimeProvider(now));

        Assert.AreEqual(CompatibilityScreenState.NotEstablished, evaluation.Screen.State);
        Assert.IsFalse(evaluation.Screen.ContinueEnabled);
        Assert.IsNull(evaluation.PlanningSession);
        Assert.IsNull(evaluation.CurrentConfiguration);
        Assert.IsNull(evaluation.MachineMemory);
    }

    internal static CompatibilityProductionInput CreateInput(
        DateTimeOffset observedAtUtc,
        string bindingModelDigest = Digest,
        string bindingHardwareDigest = Digest,
        ulong installedSystemMemoryBytes = 16 * GiB,
        ulong availableSystemMemoryBytes = 4 * GiB)
    {
        Guid modelRun = Guid.Parse("11111111-1111-4111-8111-111111111111");
        Guid hardwareRun = Guid.Parse("22222222-2222-4222-8222-222222222222");
        Guid handoff = Guid.Parse("33333333-3333-4333-8333-333333333333");
        OpenVinoAdmittedConfiguration current = Admitted(
            "ov-current", OpenVinoWeightFormat.Original);
        OpenVinoAdmittedConfiguration alternative = Admitted(
            "ov-int4", OpenVinoWeightFormat.Int4);
        OptimizationCapabilitySnapshot snapshot =
            OptimizationCapabilitySnapshot.ForOpenVino(
                "ov-cap", Digest, OpenVinoCapabilityPayload.Create(
                    SyntheticRuntimeBuild,
                    [current, alternative],
                    [Authority(current.EvidenceId), Authority(
                        alternative.EvidenceId,
                        OpenVinoWeightPrecision.FourBit)]));
        OpenVinoCompatibilityModelInput model =
            OpenVinoCompatibilityModelInput.Create(
                GiB, 16, 2048, 16, 4, 4096, 3_000_000_000);
        CompatibilityCurrentModelInput currentModel =
            CompatibilityCurrentModelInput.ForOpenVino(
                model,
                OpenVinoRouteConfiguration.Create(
                    OpenVinoWeightFormat.Original,
                    OpenVinoKvCacheFormat.U8,
                    DeviceRouteId.Cpu,
                    OpenVinoPerformanceHint.Latency,
                    OpenVinoCompiledCachePolicy.Disabled,
                    1),
                OpenVinoWeightPrecision.Fp16);
        CompatibilityHardwareInput hardware = CompatibilityHardwareInput.Create(
            TotalPhysicalMemory.FromBytes(installedSystemMemoryBytes),
            4 * GiB,
            500 * GiB,
            [DeviceRouteId.Cpu],
            [CompatibilityBackend.OpenVinoCpu]);
        OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
            modelRun.ToString("N"),
            handoff.ToString("N"),
            bindingModelDigest,
            model.PackageLengthBytes,
            hardwareRun.ToString("N"),
            bindingHardwareDigest);
        CompatibilityOptimizationProductionInput optimization =
            CompatibilityOptimizationProductionInput.Create(
                snapshot,
                OptimizationWorkload.Create(
                    "chat",
                    512,
                    OptimizationAssessment.Poor,
                    [ContextTokenCount.FromTokens(4096)]),
                binding,
                new HashSet<string>(),
                SyntheticEvidenceCatalog(
                    binding.ModelSha256,
                    current.EvidenceId,
                    alternative.EvidenceId));
        CompatibilityJourneyAuthorityInput journey =
            CompatibilityJourneyAuthorityInput.Create(
                handoff,
                Digest,
                CompatibilityFactDigest.ComputeModel(currentModel),
                Digest,
                CompatibilityFactDigest.ComputeHardware(hardware));

        return CompatibilityProductionInput.Create(
            modelRun,
            hardwareRun,
            currentModel,
            journey,
            hardware,
            CompatibilityFreshResourcesInput.Create(
                CurrentlyAvailableMemory.FromBytes(availableSystemMemoryBytes),
                0,
                500 * GiB,
                observedAtUtc),
            optimization);
    }

    private static OpenVinoAdmittedConfiguration Admitted(
        string evidenceId,
        OpenVinoWeightFormat weights) =>
        OpenVinoAdmittedConfiguration.Create(
            evidenceId,
            DeviceRouteId.Cpu,
            weights,
            OpenVinoKvCacheFormat.U8,
            OpenVinoPerformanceHint.Latency,
            OpenVinoCompiledCachePolicy.Disabled,
            1,
            512,
            8192,
            SupportLevel.DeclaredSupported,
            requiresEvidence: false);

    private static OpenVinoExecutionAuthority Authority(
        string evidenceId,
        OpenVinoWeightPrecision target = OpenVinoWeightPrecision.Fp16) =>
        OpenVinoExecutionAuthority.Create(
            evidenceId,
            evidenceId,
            target,
            OpenVinoBuildIdentity.Create(
                SyntheticRuntimeBuild,
                "synthetic-genai-v1",
                "synthetic-tokenizers-v1",
                Digest),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["openvino"] = SyntheticRuntimeBuild
            },
            compiledCacheIsDisposable: true,
            turboQuantBuild: null);

    private static OptimizationEvidenceCatalog SyntheticEvidenceCatalog(
        string modelSha256,
        string currentEvidenceId,
        string alternativeEvidenceId)
    {
        OpenVinoExecutionAuthority current = Authority(currentEvidenceId);
        OpenVinoExecutionAuthority alternative = Authority(
            alternativeEvidenceId, OpenVinoWeightPrecision.FourBit);
        return new OptimizationEvidenceCatalog(
        [
            SyntheticEvidence(current, modelSha256, "fp16", currentEvidenceId),
            SyntheticEvidence(alternative, modelSha256, "int4", alternativeEvidenceId)
        ]);
    }

    private static OptimizationEvidenceRecord SyntheticEvidence(
        OpenVinoExecutionAuthority authority,
        string modelSha256,
        string weights,
        string evidenceId) => new(
            evidenceId,
            new OptimizationEvidenceKey(
                OptimizationEvidenceModelFamily.Granite,
                modelSha256,
                3_000_000_000,
                OptimizationRoute.OpenVino,
                SyntheticPackageIdentity(authority),
                weights,
                weights,
                "u8",
                OptimizationEvidenceBackend.OpenVinoCpu,
                OptimizationEvidenceDeviceClass.Cpu,
                4096,
                "local-chat-v1",
                PublishedOpenVinoOptimizationEvidence.MethodologyIdentity,
                PublishedOpenVinoOptimizationEvidence.MemoryPerformanceProtocol,
                "openvino-cpu"),
            new OptimizationQualityScore(weights == "fp16" ? 6m : 5m),
            OutputHealthPassed: true,
            StabilityPassed: true,
            ActivationPassed: true,
            IntegrityPassed: true);

    private static string SyntheticPackageIdentity(
        OpenVinoExecutionAuthority authority)
    {
        StringBuilder canonical = new();
        void Append(string value) => canonical
            .Append(value.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(value);
        Append("openvino-execution-closure-v1");
        Append(authority.BuildIdentity.RuntimeBuild);
        Append(authority.BuildIdentity.GenAiBuild);
        Append(authority.BuildIdentity.TokenizersBuild);
        Append(authority.BuildIdentity.WorkerManifestDigest);
        Append(authority.OptimizerVersions.Count.ToString(CultureInfo.InvariantCulture));
        foreach (KeyValuePair<string, string> version in
            authority.OptimizerVersions.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            Append(version.Key);
            Append(version.Value);
        }
        Append("tq-absent");
        return "openvino-closure-v1-" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
