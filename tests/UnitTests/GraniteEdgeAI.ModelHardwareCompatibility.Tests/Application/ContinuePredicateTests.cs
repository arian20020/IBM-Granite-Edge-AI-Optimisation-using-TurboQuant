using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application;

[TestClass]
public sealed class ContinuePredicateTests
{
    private static ContinueConditions AllTrue() => new(
        HasCurrentValidModelHandoff: true,
        HasCurrentUsableHardwareHandoff: true,
        HasRegisteredAvailableRoute: true,
        HandoffsBoundToCurrentIdentities: true,
        ModelHandoffIsFresh: true,
        NavigationTransactionCompleted: true);

    [TestMethod]
    [DataRow(nameof(HardwareOutcome.Completed), true)]
    [DataRow(nameof(HardwareOutcome.CompletedWithWarnings), true)]
    [DataRow(nameof(HardwareOutcome.Failed), false)]
    [DataRow(nameof(HardwareOutcome.Unknown), false)]
    public void IsEnabled_RequiresAUsableHardwareOutcome(string outcome, bool expected)
    {
        Assert.AreEqual(
            expected,
            ContinuePredicate.IsEnabled(Enum.Parse<HardwareOutcome>(outcome), AllTrue()));
    }

    [TestMethod]
    public void IsEnabled_WhenEveryConditionHolds()
    {
        Assert.IsTrue(ContinuePredicate.IsEnabled(HardwareOutcome.Completed, AllTrue()));
    }

    [TestMethod]
    [DataRow("HasCurrentValidModelHandoff")]
    [DataRow("HasCurrentUsableHardwareHandoff")]
    [DataRow("HasRegisteredAvailableRoute")]
    [DataRow("HandoffsBoundToCurrentIdentities")]
    [DataRow("ModelHandoffIsFresh")]
    [DataRow("NavigationTransactionCompleted")]
    public void IsEnabled_IsFalseWhenAnySingleConditionFails(string condition)
    {
        // Every condition is individually load-bearing. A predicate that passed
        // with one of these false would enable a step the user cannot complete.
        ContinueConditions conditions = condition switch
        {
            "HasCurrentValidModelHandoff" =>
                AllTrue() with { HasCurrentValidModelHandoff = false },
            "HasCurrentUsableHardwareHandoff" =>
                AllTrue() with { HasCurrentUsableHardwareHandoff = false },
            "HasRegisteredAvailableRoute" =>
                AllTrue() with { HasRegisteredAvailableRoute = false },
            "HandoffsBoundToCurrentIdentities" =>
                AllTrue() with { HandoffsBoundToCurrentIdentities = false },
            "ModelHandoffIsFresh" =>
                AllTrue() with { ModelHandoffIsFresh = false },
            "NavigationTransactionCompleted" =>
                AllTrue() with { NavigationTransactionCompleted = false },
            _ => throw new ArgumentOutOfRangeException(nameof(condition))
        };

        Assert.IsFalse(ContinuePredicate.IsEnabled(HardwareOutcome.Completed, conditions));
    }

    [TestMethod]
    public void IsEnabled_CountsSixConditionsSoANewOneCannotBeForgotten()
    {
        // A condition added to the record but not to the conjunction would let
        // Continue enable on an unchecked question. This fails the day that
        // happens rather than the day a user hits it.
        Assert.AreEqual(
            6,
            typeof(ContinueConditions)
                .GetProperties()
                .Count(property => property.PropertyType == typeof(bool)),
            "A condition was added or removed; update ContinuePredicate.IsEnabled too.");
    }

    [TestMethod]
    public void IsEnabled_IsFalseWhenEverythingIsFalse()
    {
        Assert.IsFalse(ContinuePredicate.IsEnabled(
            HardwareOutcome.Unknown,
            new ContinueConditions(false, false, false, false, false, false)));
    }
}
