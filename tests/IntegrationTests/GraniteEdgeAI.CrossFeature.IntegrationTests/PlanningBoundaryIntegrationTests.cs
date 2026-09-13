using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.CrossFeature.IntegrationTests;

[TestClass]
public sealed class PlanningBoundaryIntegrationTests
{
    private const string ModelDigest =
        "1111111111111111111111111111111111111111111111111111111111111111";
    private const string HardwareDigest =
        "2222222222222222222222222222222222222222222222222222222222222222";

    [TestMethod]
    [DataRow(0, OptimizationPreferenceBand.MaximumEfficiency)]
    [DataRow(19, OptimizationPreferenceBand.MaximumEfficiency)]
    [DataRow(20, OptimizationPreferenceBand.Efficient)]
    [DataRow(39, OptimizationPreferenceBand.Efficient)]
    [DataRow(40, OptimizationPreferenceBand.Balanced)]
    [DataRow(59, OptimizationPreferenceBand.Balanced)]
    [DataRow(60, OptimizationPreferenceBand.HighCapability)]
    [DataRow(79, OptimizationPreferenceBand.HighCapability)]
    [DataRow(80, OptimizationPreferenceBand.MaximumCapability)]
    [DataRow(100, OptimizationPreferenceBand.MaximumCapability)]
    public void EveryVisiblePreferenceBandEdgeMapsMonotonically(
        int sliderValue,
        OptimizationPreferenceBand expectedBand)
    {
        // Characterization: changing either comparison at a band edge breaks
        // the established five-band vocabulary used by import and planning
        OptimizationPreferenceSelection selection =
            OptimizationPreferenceSelection.Manual(sliderValue);

        Assert.AreEqual(OptimizationPreferenceKind.Manual, selection.Kind);
        Assert.AreEqual(sliderValue, selection.PreferenceValue);
        Assert.AreEqual(expectedBand, selection.Band);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(101)]
    public void PreferenceValuesOutsideTheVisibleRangeAreRejected(int value)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            OptimizationPreferenceSelection.Manual(value));
    }

    [TestMethod]
    public void PlanningBindingCarriesExactModelAndHardwareIdentityWithoutPaths()
    {
        OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
            "model-run-1",
            "model-handoff-1",
            ModelDigest,
            4096,
            "hardware-run-1",
            HardwareDigest);

        Assert.AreEqual("model-run-1", binding.ModelInspectionRunId);
        Assert.AreEqual("model-handoff-1", binding.ModelInspectionHandoffId);
        Assert.AreEqual(ModelDigest, binding.ModelSha256);
        Assert.AreEqual(4096UL, binding.ModelLengthBytes);
        Assert.AreEqual("hardware-run-1", binding.ProductHardwareRunId);
        Assert.AreEqual(HardwareDigest, binding.HardwareSnapshotSha256);
    }

    [TestMethod]
    [DataRow("C:\\\\Users\\\\private\\\\run")]
    [DataRow("../private/run")]
    [DataRow("private/run")]
    public void PlanningBindingRejectsPathLikeCrossFeatureIdentifiers(string identifier)
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            OptimizationJourneyBinding.Create(
                identifier,
                "model-handoff-1",
                ModelDigest,
                4096,
                "hardware-run-1",
                HardwareDigest));
    }

    [TestMethod]
    public void PlanningBindingRejectsZeroLengthAndNoncanonicalDigests()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            OptimizationJourneyBinding.Create(
                "model-run-1",
                "model-handoff-1",
                ModelDigest,
                0,
                "hardware-run-1",
                HardwareDigest));
        Assert.ThrowsExactly<ArgumentException>(() =>
            OptimizationJourneyBinding.Create(
                "model-run-1",
                "model-handoff-1",
                "ABCDEFABCDEFABCDEFABCDEFABCDEFABCDEFABCDEFABCDEFABCDEFABCDEFABCD",
                4096,
                "hardware-run-1",
                HardwareDigest));
    }

}
