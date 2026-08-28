using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
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
    public void CompatibilityAuthorityRejectsCurrentHardwareIdentityMutation()
    {
        DateTimeOffset now = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

        Assert.ThrowsExactly<ArgumentException>(() =>
            CreateInput(now, bindingHardwareDigest: OtherDigest));
    }

    private static CompatibilityProductionInput CreateInput(
        DateTimeOffset observedAtUtc,
        string bindingHardwareDigest = Digest)
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
                    "2026.1.0",
                    [current, alternative],
                    [Authority(current.EvidenceId), Authority(
                        alternative.EvidenceId,
                        OpenVinoWeightPrecision.FourBit)]));
        OpenVinoCompatibilityModelInput model =
            OpenVinoCompatibilityModelInput.Create(
                GiB, 16, 2048, 16, 4, 4096);
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
            16 * GiB,
            4 * GiB,
            500 * GiB,
            [DeviceRouteId.Cpu],
            [CompatibilityBackend.OpenVinoCpu]);
        OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
            modelRun.ToString("N"),
            handoff.ToString("N"),
            Digest,
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
                new HashSet<string>());
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
                16 * GiB,
                4 * GiB,
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
                "2026.1.0", "2026.1.0", "2026.1.0", Digest),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["openvino"] = "2026.1.0"
            },
            compiledCacheIsDisposable: true,
            turboQuantBuild: null);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
