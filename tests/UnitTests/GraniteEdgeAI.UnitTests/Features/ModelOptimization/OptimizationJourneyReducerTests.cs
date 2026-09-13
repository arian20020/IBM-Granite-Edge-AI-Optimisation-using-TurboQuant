using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.Features.ModelOptimization.Journey;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.UnitTests.Features.ModelHardwareCompatibility;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.ModelOptimization;

[TestClass]
public sealed class OptimizationJourneyReducerTests
{
    [TestMethod]
    public void ProgressIsMonotonicAndWrongBindingIsIgnored()
    {
        OptimizationJourneyEntryContext entry =
            OptimizationSelectionHandoffTests.RequiredJourneyEntry();
        OptimizationJourneyState state = OptimizationJourneyReducer.Apply(
            OptimizationJourneyState.Initial(entry),
            new OptimizationStarted(1));
        state = OptimizationJourneyReducer.Apply(
            state,
            new OptimizationProgressed(new OptimizationProgress(
                1,
                entry.OptimizationHandoff.OptimizationPlanId,
                entry.OptimizationHandoff.ConfigurationSha256,
                OptimizationProgressStage.Validate,
                0.6,
                OptimizationProgressStatusKey.Active)));
        OptimizationJourneyState regressed = OptimizationJourneyReducer.Apply(
            state,
            new OptimizationProgressed(new OptimizationProgress(
                1,
                entry.OptimizationHandoff.OptimizationPlanId,
                entry.OptimizationHandoff.ConfigurationSha256,
                OptimizationProgressStage.Optimise,
                0.9,
                OptimizationProgressStatusKey.Active)));
        OptimizationJourneyState wrongPlan = OptimizationJourneyReducer.Apply(
            state,
            new OptimizationProgressed(new OptimizationProgress(
                1,
                Guid.NewGuid(),
                entry.OptimizationHandoff.ConfigurationSha256,
                OptimizationProgressStage.Publish,
                1,
                OptimizationProgressStatusKey.Completed)));

        Assert.AreSame(state, regressed);
        Assert.AreSame(state, wrongPlan);
        Assert.AreEqual(OptimizationProgressStage.Validate, state.Stage);
        Assert.AreEqual(0.6, state.Fraction);
    }

    [TestMethod]
    public void TerminalStateRejectsEveryLateEvent()
    {
        OptimizationJourneyEntryContext entry =
            OptimizationSelectionHandoffTests.RequiredJourneyEntry();
        OptimizationJourneyState running = OptimizationJourneyReducer.Apply(
            OptimizationJourneyState.Initial(entry),
            new OptimizationStarted(1));
        OptimizationJourneyState cancelled = OptimizationJourneyReducer.Apply(
            running,
            new OptimizationCancelled(1));

        Assert.AreSame(
            cancelled,
            OptimizationJourneyReducer.Apply(cancelled, new OptimizationStarted(2)));
    }
}
