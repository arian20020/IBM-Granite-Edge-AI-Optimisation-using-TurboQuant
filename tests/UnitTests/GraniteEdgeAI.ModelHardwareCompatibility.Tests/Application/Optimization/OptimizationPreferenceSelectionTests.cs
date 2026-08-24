using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Optimization;

/// <summary>
/// The preference vocabulary, pinned to the wording and the boundaries.
///
/// These labels already exist in Model Download. A second vocabulary that
/// drifted by one word, or a band boundary off by one, would put two different
/// answers in front of a user for the same slider position - so the exact
/// strings and the exact edges are asserted rather than described.
/// </summary>
[TestClass]
public sealed class OptimizationPreferenceSelectionTests
{
    [TestMethod]
    [DataRow(0, OptimizationPreferenceBand.MaximumEfficiency, "Maximum efficiency")]
    [DataRow(19, OptimizationPreferenceBand.MaximumEfficiency, "Maximum efficiency")]
    [DataRow(20, OptimizationPreferenceBand.Efficient, "Efficient")]
    [DataRow(39, OptimizationPreferenceBand.Efficient, "Efficient")]
    [DataRow(40, OptimizationPreferenceBand.Balanced, "Balanced")]
    [DataRow(59, OptimizationPreferenceBand.Balanced, "Balanced")]
    [DataRow(60, OptimizationPreferenceBand.HighCapability, "High capability")]
    [DataRow(79, OptimizationPreferenceBand.HighCapability, "High capability")]
    [DataRow(80, OptimizationPreferenceBand.MaximumCapability, "Maximum capability")]
    [DataRow(100, OptimizationPreferenceBand.MaximumCapability, "Maximum capability")]
    public void ManualUsesExactVocabulary(
        int value, OptimizationPreferenceBand band, string label)
    {
        OptimizationPreferenceSelection selection =
            OptimizationPreferenceSelection.Manual(value);

        Assert.AreEqual(band, selection.Band);
        Assert.AreEqual(label, OptimizationPreferenceLabelPolicy.GetLabel(selection));
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(101)]
    [DataRow(int.MinValue)]
    [DataRow(int.MaxValue)]
    public void ManualRejectsValuesOutsideTheSlider(int value)
    {
        // The control is 0 to 100. A value outside it did not come from the
        // slider, so clamping it would invent a preference the user never
        // expressed.
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => OptimizationPreferenceSelection.Manual(value));
    }

    [TestMethod]
    public void AutomaticCarriesNoSliderValueOrBand()
    {
        // Automatic is a separate choice, not a position on the slider. Giving
        // it a band would make it indistinguishable from a manual selection
        // that happened to land there, and the explanation shown to the user
        // differs between the two.
        OptimizationPreferenceSelection selection =
            OptimizationPreferenceSelection.Automatic();

        Assert.AreEqual(OptimizationPreferenceKind.Automatic, selection.Kind);
        Assert.IsNull(selection.PreferenceValue);
        Assert.IsNull(selection.Band);
    }

    [TestMethod]
    public void AutomaticHasItsOwnLabel()
    {
        Assert.AreEqual(
            "Automatic",
            OptimizationPreferenceLabelPolicy.GetLabel(
                OptimizationPreferenceSelection.Automatic()));
    }

    [TestMethod]
    public void ManualCarriesTheExactValueItWasGiven()
    {
        // The band is derived, but the raw value is bound into the plan. A
        // selection that reported only its band could not be reproduced.
        OptimizationPreferenceSelection selection =
            OptimizationPreferenceSelection.Manual(47);

        Assert.AreEqual(OptimizationPreferenceKind.Manual, selection.Kind);
        Assert.AreEqual(47, selection.PreferenceValue);
    }

    [TestMethod]
    public void EveryBandHasALabel()
    {
        // A band with no label would reach the page as an empty string, which
        // reads as a preference with no name rather than as a bug.
        foreach (OptimizationPreferenceBand band in
            Enum.GetValues<OptimizationPreferenceBand>())
        {
            Assert.IsFalse(
                string.IsNullOrWhiteSpace(OptimizationPreferenceLabelPolicy.GetLabel(band)),
                $"{band} has no label.");
        }
    }

    [TestMethod]
    public void EverySliderPositionResolvesToABand()
    {
        // No gap and no overlap across the whole control: every integer the
        // slider can produce names exactly one band.
        for (int value = 0; value <= 100; value++)
        {
            OptimizationPreferenceSelection selection =
                OptimizationPreferenceSelection.Manual(value);

            Assert.IsNotNull(selection.Band, $"{value} resolved to no band.");
            Assert.IsTrue(
                Enum.IsDefined(selection.Band.Value),
                $"{value} resolved to an undefined band.");
        }
    }

    [TestMethod]
    public void BandsAreContiguousAndOrderedByCapability()
    {
        // The bands must run efficiency-first to capability-last with no
        // reversal, because the frontier is ordered the same way and the two
        // orderings are compared against each other.
        int[] boundaries = [0, 20, 40, 60, 80];
        OptimizationPreferenceBand[] expected =
        [
            OptimizationPreferenceBand.MaximumEfficiency,
            OptimizationPreferenceBand.Efficient,
            OptimizationPreferenceBand.Balanced,
            OptimizationPreferenceBand.HighCapability,
            OptimizationPreferenceBand.MaximumCapability
        ];

        for (int index = 0; index < boundaries.Length; index++)
        {
            Assert.AreEqual(
                expected[index],
                OptimizationPreferenceSelection.Manual(boundaries[index]).Band);

            Assert.AreEqual(
                index + 1,
                (int)expected[index],
                $"{expected[index]} is not at ordinal {index + 1}, so band order "
                + "no longer runs from efficiency to capability.");
        }
    }

    [TestMethod]
    public void LabelPolicyRejectsAnUndefinedBand()
    {
        // Fails closed: an unnamed band must not reach a user as a blank or as
        // some neighbouring band's wording.
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => OptimizationPreferenceLabelPolicy.GetLabel((OptimizationPreferenceBand)99));
    }

    [TestMethod]
    public void SelectionsWithTheSameValueAreEqual()
    {
        // Value equality is relied on when a plan is compared against the
        // preference it was issued for.
        Assert.AreEqual(
            OptimizationPreferenceSelection.Manual(55),
            OptimizationPreferenceSelection.Manual(55));

        Assert.AreEqual(
            OptimizationPreferenceSelection.Automatic(),
            OptimizationPreferenceSelection.Automatic());

        Assert.AreNotEqual(
            OptimizationPreferenceSelection.Automatic(),
            OptimizationPreferenceSelection.Manual(55));
    }
}
