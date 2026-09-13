using GraniteEdgeAI.EndToEndTests.Automation;

namespace GraniteEdgeAI.EndToEndTests.Tests;

[TestClass]
public sealed class ConditionWaitTests
{
    [TestMethod]
    public void Until_returns_after_observable_condition_becomes_true()
    {
        int probes = 0;

        bool result = ConditionWait.Until(
            () => ++probes == 3,
            TimeSpan.FromSeconds(1),
            TimeSpan.Zero,
            CancellationToken.None);

        Assert.IsTrue(result);
        Assert.AreEqual(3, probes);
    }

    [TestMethod]
    public void Until_times_out_without_an_arbitrary_terminal_sleep()
    {
        bool result = ConditionWait.Until(
            () => false,
            TimeSpan.Zero,
            TimeSpan.Zero,
            CancellationToken.None);

        Assert.IsFalse(result);
    }
}
