using GraniteEdgeAI.Features.ModelHardwareCompatibility.Contracts;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.ViewModels;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.ModelHardwareCompatibility;

[TestClass]
public sealed class CompatibilityPlanIssuanceTests
{
    private const string Digest =
        "1111111111111111111111111111111111111111111111111111111111111111";
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    [TestMethod]
    public async Task CurrentFit_EmitsOnlyTheExactTypedChatHandoff()
    {
        CurrentModelLaunchHandoff expected = CurrentHandoff();
        CompatibilityScreenModel screen = CompatibilityScreenModel.ForPresentation(
            CompatibilityScreenState.EstimatedCompatible,
            [],
            [],
            BaselineExclusionReason.None,
            useCurrentModelAvailable: true,
            continueEnabled: true);
        var evaluation = new CompatibilityEvaluation(
            screen,
            PlanningSession: null,
            CurrentConfiguration: null);
        var viewModel = new CompatibilityViewModel(
            (_, _) => Task.FromResult(evaluation),
            actionAuthority: new ChatOnlyAuthority(),
            currentModelHandoffResolver: _ => expected);
        CurrentModelLaunchHandoff? observed = null;
        int optimizationRequests = 0;
        viewModel.CurrentModelChatRequested += (_, args) =>
            observed = args.Handoff;
        viewModel.OptimizationRequested += (_, _) => optimizationRequests++;

        await viewModel.StartAsync();
        viewModel.ContinueCommand.Execute(null);

        Assert.AreEqual("Chat with current model", viewModel.Presentation.PrimaryActionText);
        Assert.AreSame(expected, observed);
        Assert.AreEqual(0, optimizationRequests);
    }

    [TestMethod]
    public async Task MissingRouteAuthority_LeavesCurrentModelActionDisabled()
    {
        CurrentModelLaunchHandoff expected = CurrentHandoff();
        CompatibilityScreenModel screen = CompatibilityScreenModel.ForPresentation(
            CompatibilityScreenState.EstimatedCompatible,
            [],
            [],
            BaselineExclusionReason.None,
            useCurrentModelAvailable: true,
            continueEnabled: true);
        var viewModel = new CompatibilityViewModel(
            (_, _) => Task.FromResult(new CompatibilityEvaluation(
                screen,
                PlanningSession: null,
                CurrentConfiguration: null)),
            actionAuthority: UnavailableCompatibilityActionAuthority.Instance,
            currentModelHandoffResolver: _ => expected);

        await viewModel.StartAsync();

        Assert.AreEqual("Coming later", viewModel.Presentation.PrimaryActionText);
        Assert.IsFalse(viewModel.ContinueCommand.CanExecute(null));
    }

    private static CurrentModelLaunchHandoff CurrentHandoff()
    {
        OptimizationExecutionPayload payload = OptimizationExecutionPayload.ForGguf(
            GgufExecutionPayload.Create(
                "runtime", Commit, GgufRuntimeBackend.Cpu, "CPU", 4096,
                GgufCacheType.F16, GgufCacheType.F16, 0, false, 4, 128,
                "Estimated", "profile", 256, GgufWeightFormat.Imported));
        CurrentCompatibleConfiguration current = new(
            OptimizationRoute.Gguf,
            payload,
            payload.ComputeRuntimeConfigurationSha256(),
            "decision-1");
        return CurrentModelLaunchHandoff.Create(
            OptimizationRoute.Gguf,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Digest,
            2L * 1024 * 1024 * 1024,
            Guid.NewGuid(),
            Digest,
            current);
    }

    private sealed class ChatOnlyAuthority : ICompatibilityActionAuthority
    {
        public bool TryGetOptimizationAuthority(
            OptimizationRoute route,
            out IOptimizationExecutionPayloadComposer? composer,
            out OptimizationIssuanceAuthority? issuanceAuthority)
        {
            composer = null;
            issuanceAuthority = null;
            return false;
        }

        public bool IsCurrentModelChatAvailable(OptimizationRoute route) =>
            route == OptimizationRoute.Gguf;
    }
}
