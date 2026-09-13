using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application;

[TestClass]
public sealed class ContextLadderPolicyTests
{
    private static int[] Build(
        int target, int? baseline, int modelLimit, int entryMinimum = 1024) =>
        ContextLadderPolicy.Build(
            ContextTokenCount.FromTokens(target),
            baseline is null ? null : ContextTokenCount.FromTokens(baseline.Value),
            modelLimit,
            entryMinimum)
            .Select(c => c.Tokens)
            .ToArray();

    [TestMethod]
    public void PreservationTarget_ComesFirst()
    {
        Assert.AreEqual(8192, Build(8192, null, 131072)[0]);
    }

    [TestMethod]
    public void BaselineComesSecond_WhenItDiffersFromTheTarget()
    {
        int[] ladder = Build(8192, 4096, 131072);

        Assert.AreEqual(8192, ladder[0]);
        Assert.AreEqual(4096, ladder[1]);
    }

    [TestMethod]
    public void BaselineIsNotRepeated_WhenItEqualsTheTarget()
    {
        int[] ladder = Build(8192, 8192, 131072);

        Assert.AreEqual(1, ladder.Count(t => t == 8192));
    }

    [TestMethod]
    public void LowerRungsFollow_InDescendingOrder()
    {
        int[] ladder = Build(8192, null, 131072);

        CollectionAssert.AreEqual(new[] { 8192, 4096, 2048, 1024 }, ladder);
    }

    [TestMethod]
    public void NeverExceedsTheModelLimit()
    {
        int[] ladder = Build(4096, null, 4096);

        Assert.IsTrue(ladder.All(t => t <= 4096));
    }

    [TestMethod]
    public void NeverExceedsExplicitUserIntent()
    {
        // a generous model limit must not push the ladder above the request
        int[] ladder = Build(2048, null, 131072);

        Assert.IsTrue(ladder.All(t => t <= 2048));
        CollectionAssert.AreEqual(new[] { 2048, 1024 }, ladder);
    }

    [TestMethod]
    public void NeverGoesBelowTheSupportEntryMinimum()
    {
        int[] ladder = Build(8192, null, 131072, entryMinimum: 4096);

        Assert.IsTrue(ladder.All(t => t >= 4096));
        CollectionAssert.AreEqual(new[] { 8192, 4096 }, ladder);
    }

    [TestMethod]
    public void ContainsNoDuplicates()
    {
        int[] ladder = Build(4096, 4096, 131072);

        Assert.AreEqual(ladder.Length, ladder.Distinct().Count());
    }

    [TestMethod]
    public void ATargetAboveTheModelLimit_IsStillOfferedFirst()
    {
        // the user asked for it explicitly, so it is preserved and offered;
        // whether it is admissible is a later support and fit decision
        int[] ladder = Build(16384, null, 8192);

        Assert.AreEqual(16384, ladder[0]);
        Assert.IsTrue(ladder.Skip(1).All(t => t <= 8192));
    }

    [TestMethod]
    public void ANonStandardTarget_IsOfferedAlongsideStandardRungsBelowIt()
    {
        int[] ladder = Build(6000, null, 131072);

        Assert.AreEqual(6000, ladder[0]);
        CollectionAssert.AreEqual(new[] { 6000, 4096, 2048, 1024 }, ladder);
    }

    [TestMethod]
    public void IsDeterministic()
    {
        CollectionAssert.AreEqual(Build(8192, 4096, 131072), Build(8192, 4096, 131072));
    }

    [TestMethod]
    public void RejectsNonPositiveModelLimit()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => Build(4096, null, modelLimit: 0));
    }

    [TestMethod]
    public void RejectsNonPositiveEntryMinimum()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => Build(4096, null, 131072, entryMinimum: 0));
    }
}
