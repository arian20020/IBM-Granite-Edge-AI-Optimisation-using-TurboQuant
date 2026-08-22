using System.Collections.Generic;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.ModelHardwareCompatibility;

[TestClass]
public sealed class CompatibilityBudgetTests
{
    private const ulong Gibibyte = 1024UL * 1024 * 1024;

    private static IReadOnlyList<CompatibilityBudgetSegment> Segments(
        ulong weights = 3 * Gibibyte,
        ulong cache = 1 * Gibibyte,
        ulong reserve = 3 * Gibibyte) =>
    [
        new("Model weights", weights, false),
        new("Context", cache, false),
        new("Held back", reserve, true)
    ];

    [TestMethod]
    public void Required_CountsOnlyWhatTheModelNeeds()
    {
        // The reserve is memory held back for Windows and other apps. Counting
        // it as a requirement would say the model is bigger than it is.
        CompatibilityBudget budget = CompatibilityBudget.Create(Segments(), 16 * Gibibyte);

        Assert.AreEqual(4 * Gibibyte, budget.RequiredBytes);
    }

    [TestMethod]
    public void ItFits_WhenTheRequirementIsWithinTheSafeLimit()
    {
        Assert.IsTrue(CompatibilityBudget.Create(Segments(), 16 * Gibibyte).Fits);
    }

    [TestMethod]
    public void ItFits_AtExactlyTheSafeLimit()
    {
        // Equality counts as fitting, because every mandatory margin is already
        // inside the limit by the time it reaches here.
        Assert.IsTrue(CompatibilityBudget.Create(Segments(), 4 * Gibibyte).Fits);
    }

    [TestMethod]
    public void ItDoesNotFit_OneByteOver()
    {
        Assert.IsFalse(
            CompatibilityBudget.Create(Segments(), (4 * Gibibyte) - 1).Fits);
    }

    [TestMethod]
    public void TheBarScalesToTheSafeLimit_WhenEverythingFits()
    {
        CompatibilityBudget budget = CompatibilityBudget.Create(Segments(), 16 * Gibibyte);

        Assert.AreEqual(16 * Gibibyte, budget.ScaleBytes);
    }

    [TestMethod]
    public void TheBarScalesToTheRequirement_WhenItOverruns()
    {
        // Otherwise the overflow would be clipped, hiding the one figure that
        // tells someone whether closing an app would be enough.
        CompatibilityBudget budget = CompatibilityBudget.Create(Segments(), 2 * Gibibyte);

        Assert.AreEqual(4 * Gibibyte, budget.ScaleBytes);
    }

    [TestMethod]
    public void TheLimitingComponent_IsTheLargestThingTheModelNeeds()
    {
        CompatibilityBudget budget = CompatibilityBudget.Create(
            Segments(weights: 1 * Gibibyte, cache: 5 * Gibibyte), 16 * Gibibyte);

        Assert.AreEqual("Context", budget.LimitingComponent);
    }

    [TestMethod]
    public void TheLimitingComponent_IsNeverTheReserve()
    {
        // The reserve is often the biggest band on the bar, but naming it would
        // tell the user to shrink something they do not control.
        CompatibilityBudget budget = CompatibilityBudget.Create(
            Segments(weights: 1 * Gibibyte, cache: 1 * Gibibyte, reserve: 9 * Gibibyte),
            16 * Gibibyte);

        Assert.AreEqual("Model weights", budget.LimitingComponent);
    }

    [TestMethod]
    public void AnEmptyBudget_HasAUsableScaleRatherThanZero()
    {
        // A zero scale would divide by zero when the bar is drawn.
        Assert.IsTrue(CompatibilityBudget.Empty.ScaleBytes > 0);
        Assert.AreEqual(0, CompatibilityBudget.Empty.Segments.Count);
    }

    [TestMethod]
    public void Create_CopiesItsSegmentsSoLaterMutationCannotChangeIt()
    {
        List<CompatibilityBudgetSegment> segments =
            [new("Model weights", Gibibyte, false)];

        CompatibilityBudget budget = CompatibilityBudget.Create(segments, 8 * Gibibyte);

        segments.Add(new CompatibilityBudgetSegment("Added later", 99 * Gibibyte, false));

        Assert.AreEqual(1, budget.Segments.Count);
    }

    [TestMethod]
    public void Describe_RoundsToSomethingAPersonWouldSay()
    {
        // Precision here would imply a measurement nobody took.
        Assert.AreEqual("4 GB", CompatibilityBudget.Describe(4 * Gibibyte));
        Assert.AreEqual("512 MB", CompatibilityBudget.Describe(512UL * 1024 * 1024));
    }
}
