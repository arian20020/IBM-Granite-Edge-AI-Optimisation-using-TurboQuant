using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.PlanningContext;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application;

[TestClass]
public sealed class PlanningContextPolicyTests
{
    [TestMethod]
    public void ContextTokenCount_RejectsZeroAndNegative()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ContextTokenCount.FromTokens(0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ContextTokenCount.FromTokens(-1));
    }

    [TestMethod]
    public void ApplicationDefault_CarriesNoExplicitCount()
    {
        CompatibilityContextRequest request = CompatibilityContextRequest.ApplicationDefault();

        Assert.AreEqual(CompatibilityContextMode.ApplicationDefault, request.Mode);
        Assert.IsNull(request.RequestedTokens);
    }

    [TestMethod]
    public void UserRequested_CarriesTheExactCount()
    {
        CompatibilityContextRequest request =
            CompatibilityContextRequest.UserRequested(ContextTokenCount.FromTokens(16384));

        Assert.AreEqual(CompatibilityContextMode.UserRequested, request.Mode);
        Assert.AreEqual(16384, request.RequestedTokens!.Value.Tokens);
    }

    [TestMethod]
    [DataRow(131072UL, 4096)]
    [DataRow(4096UL, 4096)]
    [DataRow(2048UL, 2048)]
    public void Default_TakesMinimumOfFourThousandNinetySixAndModelLimit(
        ulong declaredLimit,
        int expected)
    {
        PlanningContextResolution resolution = PlanningContextPolicy.Resolve(
            CompatibilityContextRequest.ApplicationDefault(),
            declaredLimit);

        Assert.AreEqual(PlanningContextStatus.Resolved, resolution.Status);
        Assert.AreEqual(expected, resolution.ResolvedTokens!.Value.Tokens);
    }

    [TestMethod]
    public void UserRequested_IsPreservedExactly_EvenAboveModelLimit()
    {
        PlanningContextResolution resolution = PlanningContextPolicy.Resolve(
            CompatibilityContextRequest.UserRequested(ContextTokenCount.FromTokens(16384)),
            8192UL);

        Assert.AreEqual(PlanningContextStatus.Resolved, resolution.Status);
        Assert.AreEqual(16384, resolution.ResolvedTokens!.Value.Tokens);
        Assert.IsFalse(resolution.WithinModelLimit);
    }

    [TestMethod]
    public void UserRequested_WithinLimit_IsFlaggedWithin()
    {
        PlanningContextResolution resolution = PlanningContextPolicy.Resolve(
            CompatibilityContextRequest.UserRequested(ContextTokenCount.FromTokens(16384)),
            131072UL);

        Assert.IsTrue(resolution.WithinModelLimit);
    }

    [TestMethod]
    public void MissingModelLimit_IsNotEstablished()
    {
        PlanningContextResolution resolution = PlanningContextPolicy.Resolve(
            CompatibilityContextRequest.ApplicationDefault(),
            declaredModelContextLimit: null);

        Assert.AreEqual(PlanningContextStatus.NotEstablished, resolution.Status);
        Assert.IsNull(resolution.ResolvedTokens);
    }

    [TestMethod]
    public void ZeroModelLimit_IsNotEstablished()
    {
        PlanningContextResolution resolution = PlanningContextPolicy.Resolve(
            CompatibilityContextRequest.ApplicationDefault(),
            declaredModelContextLimit: 0UL);

        Assert.AreEqual(PlanningContextStatus.NotEstablished, resolution.Status);
        Assert.IsNull(resolution.ResolvedTokens);
    }

    [TestMethod]
    public void ModelLimitAboveIntMaxValue_IsNotEstablished()
    {
        PlanningContextResolution resolution = PlanningContextPolicy.Resolve(
            CompatibilityContextRequest.ApplicationDefault(),
            (ulong)int.MaxValue + 1);

        Assert.AreEqual(PlanningContextStatus.NotEstablished, resolution.Status);
    }

    [TestMethod]
    public void SameInput_ProducesValueEquivalentOutput()
    {
        PlanningContextResolution first = PlanningContextPolicy.Resolve(
            CompatibilityContextRequest.ApplicationDefault(), 131072UL);
        PlanningContextResolution second = PlanningContextPolicy.Resolve(
            CompatibilityContextRequest.ApplicationDefault(), 131072UL);

        Assert.AreEqual(first, second);
    }

    [TestMethod]
    public void Resolve_RejectsNullRequest()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => PlanningContextPolicy.Resolve(null!, 4096UL));
    }
}
