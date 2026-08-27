using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Domain;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.UnitTests.Features.HardwareInspection;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.ModelHardwareCompatibility;

[TestClass]
public sealed class GgufCompatibilityInputProjectorTests
{
    private static readonly Guid ModelRunId =
        Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid HardwareRunId =
        Guid.Parse("33333333-3333-4333-8333-333333333333");

    [TestMethod]
    public void EligiblePairedEvidence_ProjectsExactCalculationFacts()
    {
        ModelInspectionExecutionResult terminal = Terminal();
        ModelInspectionHandoff model = ModelHandoff(terminal);
        HardwareInspectionHandoff hardware = HardwareHandoff();
        AvailableMemorySnapshot fresh = new(
            21UL * 1024 * 1024 * 1024,
            DateTimeOffset.UtcNow);

        bool projected = GgufCompatibilityInputProjector.TryProject(
            model,
            terminal,
            HardwareRunId,
            hardware,
            fresh,
            out CompatibilityProductionInput? input);

        Assert.IsTrue(projected);
        Assert.IsNotNull(input);
        Assert.AreEqual(ModelRunId, input.ModelInspectionRunId);
        Assert.AreEqual(HardwareRunId, input.ProductHardwareRunId);
        Assert.AreEqual(4_096UL, input.Model.FileLengthBytes);
        Assert.AreEqual(24, input.Model.LayerCount);
        Assert.AreEqual(2_048, input.Model.EmbeddingSize);
        Assert.AreEqual(16, input.Model.AttentionHeadCount);
        Assert.AreEqual(8, input.Model.KeyValueHeadCount);
        Assert.AreEqual(4_096, input.Model.DeclaredContextLimit);
        Assert.AreEqual(15, input.Model.FileType);
        Assert.AreEqual(2, input.Model.QuantisationVersion);
        Assert.AreEqual(32UL * 1024 * 1024 * 1024, input.Hardware.InstalledSystemMemoryBytes);
        Assert.AreEqual(8UL * 1024 * 1024 * 1024, input.Hardware.InstalledDedicatedDeviceMemoryBytes);
        Assert.AreEqual(500_000_000_000UL, input.Hardware.FreeStorageBytes);
        CollectionAssert.Contains(input.Hardware.PresentDevices.ToArray(), DeviceRouteId.Cpu);
        CollectionAssert.Contains(input.Hardware.PresentDevices.ToArray(), DeviceRouteId.IntelIntegratedGpu);
        CollectionAssert.Contains(input.Hardware.PresentDevices.ToArray(), DeviceRouteId.IntelDiscreteGpu);
        CollectionAssert.Contains(input.Hardware.PresentDevices.ToArray(), DeviceRouteId.IntelNpu);
        CollectionAssert.AreEquivalent(
            new[] { CompatibilityBackend.Cpu, CompatibilityBackend.IntelSycl },
            input.Hardware.VerifiedBackends.ToArray());
        Assert.AreEqual(fresh.AvailablePhysicalBytes, input.FreshResources.AvailableSystemMemoryBytes);
        Assert.AreEqual(0UL, input.FreshResources.AvailableDedicatedDeviceMemoryBytes);
    }

    [TestMethod]
    public void MissingOptionalModelFacts_RemainAbsent()
    {
        ModelInspectionEvidence evidence = PresentationTestData.CreateEvidence(layerCount: null);
        ModelInspectionExecutionResult terminal = ModelInspectionExecutionResult.Completed(
            PresentationTestData.CreateResult(ModelInspectionOutcome.Ready, evidence: evidence));
        ModelInspectionHandoff model = ModelHandoff(terminal);

        Assert.IsTrue(GgufCompatibilityInputProjector.TryProject(
            model,
            terminal,
            HardwareRunId,
            HardwareHandoff(),
            new AvailableMemorySnapshot(1, DateTimeOffset.UtcNow),
            out CompatibilityProductionInput? input));

        Assert.IsNull(input!.Model.LayerCount);
    }

    [TestMethod]
    public void MismatchedHardwareRunOrModelLength_FailsClosed()
    {
        ModelInspectionExecutionResult terminal = Terminal();
        ModelInspectionHandoff model = ModelHandoff(terminal);
        ModelInspectionHandoff altered = new(
            model.SchemaVersion,
            model.ModelInspectionHandoffId,
            model.ModelInspectionRunId,
            model.Outcome,
            model.ModelSha256,
            model.ModelLengthBytes + 1);

        Assert.IsFalse(GgufCompatibilityInputProjector.TryProject(
            model,
            terminal,
            Guid.NewGuid(),
            HardwareHandoff(),
            new AvailableMemorySnapshot(1, DateTimeOffset.UtcNow),
            out _));
        Assert.IsFalse(GgufCompatibilityInputProjector.TryProject(
            altered,
            terminal,
            HardwareRunId,
            HardwareHandoff(),
            new AvailableMemorySnapshot(1, DateTimeOffset.UtcNow),
            out _));
    }

    [TestMethod]
    public void StaleFreshMemory_FailsClosed()
    {
        ModelInspectionExecutionResult terminal = Terminal();

        Assert.IsFalse(GgufCompatibilityInputProjector.TryProject(
            ModelHandoff(terminal),
            terminal,
            HardwareRunId,
            HardwareHandoff(),
            new AvailableMemorySnapshot(1, DateTimeOffset.UtcNow - TimeSpan.FromMinutes(1)),
            out _));
    }

    [TestMethod]
    public async Task VerifiedDevelopmentQuantizer_EnablesOptionalOptimizationAction()
    {
        string packagedManifest = Path.Combine(
            AppContext.BaseDirectory,
            "Tools",
            "GgufQuantizer",
            "llama-quantize.package.manifest.json");
        if (!File.Exists(packagedManifest))
        {
            Assert.Inconclusive(
                "This local integration check requires the verified GGUF quantizer package.");
        }

        ModelInspectionExecutionResult terminal = Terminal();
        Assert.IsTrue(GgufCompatibilityInputProjector.TryPrepare(
            ModelHandoff(terminal),
            terminal,
            HardwareRunId,
            HardwareHandoff(),
            out PreparedGgufCompatibilityInput? prepared), "prepare");
        Assert.IsTrue(GgufOptimizationProductionAuthority.TryCreate(
            prepared!,
            out GgufOptimizationProductionAuthority? authority), "authority create");

        DateTimeOffset now = DateTimeOffset.UtcNow;
        CompatibilityEvaluation evaluation = authority!.Evaluate(
            CompatibilityFreshResourcesInput.Create(
                21UL * 1024 * 1024 * 1024,
                0,
                500_000_000_000UL,
                now),
            new HashSet<string>(),
            now,
            CancellationToken.None);
        Assert.IsTrue(authority.TryGetOptimizationAuthority(
            evaluation.PlanningSession!.Route,
            out var composer,
            out var issuanceAuthority), "issuance authority");
        Assert.IsNotNull(evaluation.PlanningSession.Issue(
            GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization
                .OptimizationPreferenceSelection.Automatic(),
            composer!,
            issuanceAuthority!,
            TimeProvider.System));
        var viewModel = new GraniteEdgeAI.Features.ModelHardwareCompatibility
            .ViewModels.CompatibilityViewModel(
            (_, _) => Task.FromResult(evaluation),
            actionAuthority: authority);

        await viewModel.StartAsync();

        Assert.IsNotNull(evaluation.OptionalOptimization);
        Assert.IsTrue(viewModel.CanOptimiseFirst, "can optimise first");
        Assert.IsTrue(viewModel.OptionalOptimizationCommand.CanExecute(null), "command can execute");
        viewModel.OptionalOptimizationCommand.Execute(null);
        Assert.IsNotNull(viewModel.Presentation.Optimization);
        Assert.IsNotNull(viewModel.CurrentOptimizationHandoff);
        Assert.IsTrue(viewModel.Presentation.PrimaryActionEnabled);
    }

    [TestMethod]
    public async Task VerifiedDevelopmentQuantizer_EnablesRequiredOptimizationAction()
    {
        string packagedManifest = Path.Combine(
            AppContext.BaseDirectory,
            "Tools",
            "GgufQuantizer",
            "llama-quantize.package.manifest.json");
        if (!File.Exists(packagedManifest))
        {
            Assert.Inconclusive(
                "This local integration check requires the verified GGUF quantizer package.");
        }

        const ulong gib = 1024UL * 1024 * 1024;
        var prepared = new PreparedGgufCompatibilityInput(
            ModelRunId,
            Guid.Parse("44444444-4444-4444-8444-444444444444"),
            PresentationTestData.Sha256,
            HardwareRunId,
            GgufCompatibilityModelInput.Create(
                2_104_533_811UL,
                24,
                2_048,
                16,
                8,
                4_096,
                15,
                2),
            CompatibilityHardwareInput.Create(
                16 * gib,
                0,
                500_000_000_000UL,
                [DeviceRouteId.Cpu],
                [CompatibilityBackend.Cpu]));
        Assert.IsTrue(GgufOptimizationProductionAuthority.TryCreate(
            prepared,
            out GgufOptimizationProductionAuthority? authority));

        DateTimeOffset now = DateTimeOffset.UtcNow;
        CompatibilityEvaluation evaluation = authority!.Evaluate(
            CompatibilityFreshResourcesInput.Create(
                3 * gib,
                0,
                500_000_000_000UL,
                now),
            new HashSet<string>(),
            now,
            CancellationToken.None);
        var viewModel = new GraniteEdgeAI.Features.ModelHardwareCompatibility
            .ViewModels.CompatibilityViewModel(
            (_, _) => Task.FromResult(evaluation),
            actionAuthority: authority);

        await viewModel.StartAsync();

        Assert.AreEqual(
            CompatibilityScreenState.OptimisationRequired,
            evaluation.Screen.State);
        Assert.IsNotNull(viewModel.Presentation.Optimization);
        Assert.IsNotNull(viewModel.CurrentOptimizationHandoff);
        Assert.IsTrue(viewModel.Presentation.PrimaryActionEnabled);
        Assert.IsTrue(viewModel.ContinueCommand.CanExecute(null));
    }

    private static ModelInspectionExecutionResult Terminal() =>
        ModelInspectionExecutionResult.Completed(
            PresentationTestData.CreateResult(ModelInspectionOutcome.Ready));

    private static ModelInspectionHandoff ModelHandoff(ModelInspectionExecutionResult terminal)
    {
        Assert.IsTrue(ModelInspectionHandoffProjector.TryProject(
            ModelRunId,
            ModelRunId,
            terminal,
            out ModelInspectionHandoff? handoff));
        return handoff!;
    }

    private static HardwareInspectionHandoff HardwareHandoff() =>
        HardwareInspectionHandoff.Create(
            HardwareRunId,
            HardwareInspectionOutcome.Completed,
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation());
}
