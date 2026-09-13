using System.Diagnostics;
using System.Text.Json;
using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Orchestration;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using GraniteEdgeAI.Features.OpenVinoRoute;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.Features.OpenVinoRoute.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.OpenVino.WorkerClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelInspectionOutcome = GraniteEdgeAI.Features.ModelInspection.Contracts.ModelInspectionOutcome;

namespace GraniteEdgeAI.UnitTests.Features.ModelHardwareCompatibility;

[TestClass]
[DoNotParallelize]
public sealed class OpenVinoHeadlessOptimizationMatrixTests
{
    [TestMethod]
    [TestCategory("ManualRealModel")]
    [Timeout(21_600_000)]
    public async Task EveryCurrentlyOfferedFormatPublishesThroughTheApplicationBackend()
    {
        if (Environment.GetEnvironmentVariable("GRANITE_RUN_OPENVINO_MATRIX") != "1")
            Assert.Inconclusive("Opt in with GRANITE_RUN_OPENVINO_MATRIX=1.");
        string source = RequiredDirectory("GRANITE_OPENVINO_REAL_MODEL");
        string output = Path.GetFullPath(Environment.GetEnvironmentVariable("GRANITE_OPENVINO_MATRIX_OUTPUT")
            ?? throw new InvalidOperationException("An absent matrix output directory is required."));
        Assert.IsFalse(Directory.Exists(output) || File.Exists(output), "Never overwrite a previous matrix.");
        Directory.CreateDirectory(output);
        string ledger = Path.Combine(output, "results.jsonl");
        void Record(object value) => File.AppendAllText(ledger,
            JsonSerializer.Serialize(new { utc = DateTimeOffset.UtcNow, value }) + Environment.NewLine);

        OpenVinoStaticPackageEvidence evidence = new OpenVinoStaticPackageInspector().Inspect(source).Evidence
            ?? throw new AssertFailedException("Real source inspection failed.");
        Guid hardwareId = Guid.NewGuid();
        HardwareInspectionRunResult hardware = await HardwareInspectionComposition.CreateProduction().RunAsync(
            hardwareId, new InlineProgress<HardwareInspectionRunProgress>(p => Record(new { hardwareStage = p.Stage.ToString() })),
            CancellationToken.None);
        Record(new { hardwareOutcome = hardware.Outcome.ToString(), hardware.SafeDiagnosticCode });
        Assert.IsNotNull(hardware.Handoff, "Production hardware inspection must provide usable evidence.");
        ModelInspectionHandoff model = new(ModelInspectionHandoff.CurrentSchemaVersion,
            Guid.NewGuid(), Guid.NewGuid(), ModelInspectionOutcome.Ready,
            evidence.ModelSha256, evidence.ModelLengthBytes);
        Assert.IsTrue(OpenVinoCompatibilityInputProjector.TryPrepare(model, evidence, hardwareId,
            hardware.Handoff, out PreparedOpenVinoCompatibilityInput? prepared));

        OpenVinoWorkerInstallation official = OpenVinoOfficialWorkerAuthority.CreateInstallation(
            RequiredDirectory("OPENVINO_OFFICIAL_WORKER_STAGE_A"),
            VerifiedOpenVinoOptimizationEvidence.WorkerManifestSha256);
        OpenVinoWorkerInstallation turbo = OpenVinoTurboQuantWorkerAuthority.CreateInstallation(
            RequiredDirectory("OPENVINO_TURBOQUANT_WORKER_STAGE"),
            VerifiedOpenVinoOptimizationEvidence.TurboWorkerManifestSha256,
            VerifiedOpenVinoOptimizationEvidence.TurboPatchSeriesSha256,
            VerifiedOpenVinoOptimizationEvidence.TurboRuntimeManifestSha256);
        Assert.IsTrue(OpenVinoOptimizationProductionAuthority.TryCreate(prepared!, official.ExpectedBuildEvidence,
            turbo.ExpectedBuildEvidence, true, out OpenVinoOptimizationProductionAuthority? authority));
        Assert.IsNotNull(authority);
        OpenVinoRouteService Route(OpenVinoWorkerInstallation installation) => new(
            new OpenVinoWorkerClient(OpenVinoWorkerClientOptions.CreateDefault(installation)), installation.ExpectedBuildEvidence);
        OpenVinoOptimizationService service = new(new SealedOpenVinoOptimizationPipeline(
            RequiredDirectory("GRANITE_OPENVINO_CONVERTER_STAGE"),
            "8c4ed62912331be2d50b08785579950cc636d4beac6bb0f4a4284795d14b0294",
            Route(official), Route(turbo)));
        WindowsCompatibilityFreshResourcesSource resources = new();
        async Task<CompatibilityEvaluation> Evaluate()
        {
            CompatibilityFreshResourcesInput fresh = await resources.CaptureAsync(CancellationToken.None);
            return authority.Evaluate(fresh, new HashSet<string>(StringComparer.Ordinal),
                DateTimeOffset.UtcNow, CancellationToken.None);
        }
        CompatibilityEvaluation initial = await Evaluate();
        var offered = initial.Screen.Optimization!.SafeSliderModes
            .Select(item => (item.Mode.OpenVinoWeights, item.Mode.OpenVinoKvCache, item.Mode.ContextTokens))
            .Distinct()
            .OrderByDescending(item => item.OpenVinoWeights == OpenVinoWeightFormat.Int8
                && item.OpenVinoKvCache == OpenVinoKvCacheFormat.RouteDefault)
            .ToArray();
        Record(new { source, evidence.ModelSha256, evidence.PackageManifestDigest,
            offered = offered.Select(item => new { weights = item.OpenVinoWeights.ToString(), cache = item.OpenVinoKvCache.ToString(), item.ContextTokens }) });
        Assert.IsTrue(offered.Length > 0, "No configurations were offered by the real authority.");
        List<string> failures = [];
        foreach (var format in offered)
        {
            string name = $"{format.OpenVinoWeights}-{format.OpenVinoKvCache}-{format.ContextTokens}";
            try
            {
                CompatibilityEvaluation current = await Evaluate();
                CompatibilityExactOptimizationModeView? choice = current.Screen.Optimization!.SafeSliderModes.SingleOrDefault(item =>
                    item.Mode.OpenVinoWeights == format.OpenVinoWeights && item.Mode.OpenVinoKvCache == format.OpenVinoKvCache
                    && item.Mode.ContextTokens == format.ContextTokens);
                Assert.IsNotNull(choice, "Configuration no longer fits fresh resources; do not bypass admission.");
                Assert.IsTrue(authority.TryGetOptimizationAuthority(OptimizationRoute.OpenVino,
                    out IOptimizationExecutionPayloadComposer? composer, out OptimizationIssuanceAuthority? issuance));
                OptimizationExecutionPlan plan = current.PlanningSession!.Issue(
                    OptimizationPreferenceSelection.Exact(choice.CandidateIdentity), composer!, issuance!, TimeProvider.System);
                Assert.AreEqual(3, plan.ContractVersion);
                string destination = Path.Combine(output, name);
                List<string> stages = [];
                Stopwatch timer = Stopwatch.StartNew();
                Record(new { name, state = "started", plan.OptimizationPlanId, plan.ConfigurationSha256 });
                OptimizationExecutionResult result = await service.ExecuteAsync(
                    new OpenVinoOptimizationRequest(source, destination, plan, new CurrentState(authority), true),
                    new InlineProgress<OpenVinoOptimizationProgress>(p =>
                    {
                        stages.Add(p.Stage.ToString());
                        Record(new { name, stage = p.Stage.ToString(), seconds = timer.Elapsed.TotalSeconds });
                    }), CancellationToken.None);
                Record(new { name, state = result.Status.ToString(), code = result.SupportCode.ToString(),
                    seconds = timer.Elapsed.TotalSeconds, destination, result.OutputIdentity, result.OutputManifestSha256, result.OutputSizeBytes, stages });
                Assert.IsTrue(result.IsSuccessful, $"{name}: {result.SupportCode}");
                if (result.ProducedPersistentArtifact)
                {
                    OpenVinoOptimizationProvenance provenance = OpenVinoOptimizationProvenance.Read(destination);
                    Assert.AreEqual(plan.OptimizationPlanId, provenance.OptimizationPlanId);
                    Assert.AreEqual(plan.ConfigurationSha256, provenance.ConfigurationSha256);
                    Assert.AreEqual(3, provenance.ExecutorContractVersion);
                    Assert.AreEqual(evidence.ModelSha256, provenance.ModelSha256);
                    Assert.AreEqual(plan.ExecutionPayload.OpenVino!.TargetWeightPrecision.ToString(), provenance.TargetWeightPrecision.ToString());
                    Assert.AreEqual(plan.ExecutionPayload.OpenVino.KvCachePrecision.ToString(), provenance.ActualKvCachePrecision.ToString());
                    Assert.IsTrue(File.Exists(Path.Combine(destination, "openvino_model.bin")));
                    Assert.IsTrue(stages.Contains(nameof(OpenVinoOptimizationStage.ValidatingOutput)));
                    Assert.IsTrue(stages.Contains(nameof(OpenVinoOptimizationStage.SmokeTesting)));
                    Assert.IsTrue(stages.Contains(nameof(OpenVinoOptimizationStage.Reinspecting)));
                    Assert.IsTrue(stages.Contains(nameof(OpenVinoOptimizationStage.Publishing)));
                }
                else
                {
                    OpenVinoRuntimeOptimizationProfile profile = OpenVinoRuntimeOptimizationProfile.Read(destination);
                    Assert.AreEqual(plan.OptimizationPlanId, profile.OptimizationPlanId);
                    Assert.AreEqual(plan.ConfigurationSha256, profile.ConfigurationSha256);
                    Assert.AreEqual(3, profile.ExecutorContractVersion);
                    Assert.AreEqual(evidence.ModelSha256, profile.ModelSha256);
                    Assert.AreEqual(plan.ExecutionPayload.OpenVino!.KvCachePrecision.ToString(), profile.RuntimeConfiguration.KvCachePrecision.ToString());
                }
            }
            catch (Exception error)
            {
                failures.Add(name + ": " + error.Message);
                Record(new { name, state = "failed", error = error.ToString() });
            }
        }
        Record(new { state = "matrix-complete", count = offered.Length, failures });
        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    private static string RequiredDirectory(string name)
    {
        string path = Path.GetFullPath(Environment.GetEnvironmentVariable(name)
            ?? throw new InvalidOperationException(name + " is required."));
        Assert.IsTrue(Directory.Exists(path), name + " must exist.");
        return path;
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed class CurrentState(OpenVinoOptimizationProductionAuthority authority) : IOpenVinoOptimizationCurrentStateProvider
    {
        public ValueTask<OpenVinoOptimizationCurrentState> GetCurrentStateAsync(
            OpenVinoOptimizationCheckpoint checkpoint, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(authority.CurrentState);
        }
    }
}
