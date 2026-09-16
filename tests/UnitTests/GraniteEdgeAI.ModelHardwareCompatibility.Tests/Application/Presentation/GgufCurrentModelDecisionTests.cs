using GraniteEdgeAI.Features.ModelHardwareCompatibility.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Presentation;

[TestClass]
public sealed class GgufCurrentModelDecisionTests
{
    [TestMethod]
    public void SameRuntimeSettingsAcrossInspectionsDoNotCollide()
    {
        var payload = Payload();
        Guid inspection = Guid.NewGuid();
        Guid hardware = Guid.NewGuid();
        var first = CurrentModelLaunchHandoff.CreateGgufConfiguration(inspection, hardware, payload);
        var second = CurrentModelLaunchHandoff.CreateGgufConfiguration(Guid.NewGuid(), hardware, payload);
        var refreshed = CurrentModelLaunchHandoff.CreateGgufConfiguration(inspection, Guid.NewGuid(), payload);
        var repeated = CurrentModelLaunchHandoff.CreateGgufConfiguration(inspection, hardware, payload);

        Assert.AreNotEqual(first.CompatibilityDecisionId, second.CompatibilityDecisionId);
        Assert.AreNotEqual(first.CompatibilityDecisionId, refreshed.CompatibilityDecisionId);
        Assert.AreEqual(first.CompatibilityDecisionId, repeated.CompatibilityDecisionId);
        Assert.AreEqual(first.RuntimeConfigurationSha256, second.RuntimeConfigurationSha256);
        Assert.AreSame(payload, second.ExactExecutionPayload);
        Assert.IsTrue(first.CompatibilityDecisionId.Length <= 128);
    }

    [TestMethod]
    public void MissingInspectionOrHardwareIdentityIsRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            CurrentModelLaunchHandoff.CreateGgufConfiguration(Guid.Empty, Guid.NewGuid(), Payload()));
        Assert.ThrowsExactly<ArgumentException>(() =>
            CurrentModelLaunchHandoff.CreateGgufConfiguration(Guid.NewGuid(), Guid.Empty, Payload()));
        Guid sameRun = Guid.NewGuid();
        Assert.ThrowsExactly<ArgumentException>(() =>
            CurrentModelLaunchHandoff.CreateGgufConfiguration(sameRun, sameRun, Payload()));
    }

    [TestMethod]
    public void ChangedRuntimeSettingsReceiveANewDecisionWithinTheSameJourney()
    {
        Guid inspection = Guid.NewGuid();
        Guid hardware = Guid.NewGuid();
        var first = CurrentModelLaunchHandoff.CreateGgufConfiguration(inspection, hardware, Payload());
        var changed = CurrentModelLaunchHandoff.CreateGgufConfiguration(inspection, hardware, Payload(256));
        Assert.AreNotEqual(first.CompatibilityDecisionId, changed.CompatibilityDecisionId);
        Assert.AreNotEqual(first.RuntimeConfigurationSha256, changed.RuntimeConfigurationSha256);
    }

    private static OptimizationExecutionPayload Payload(int maximumTokens = 512) =>
        OptimizationExecutionPayload.ForGguf(GgufExecutionPayload.Create(
            "runtime", "0123456789abcdef0123456789abcdef01234567",
            GgufRuntimeBackend.Cpu, "CPU", 4096,
            GgufCacheType.F16, GgufCacheType.F16, 0, false, 4, 128,
            "Estimated", "cpu-imported", maximumTokens, GgufWeightFormat.Imported));
}
