using GraniteEdgeAI.EndToEndTests.Infrastructure;

namespace GraniteEdgeAI.EndToEndTests.Tests;

[TestClass]
public sealed class TrxStageSummaryTests
{
    [TestMethod]
    public void Load_returns_exact_totals_including_declared_skips()
    {
        using TestDirectory directory = TestDirectory.Create();
        string path = directory.WriteText("stage.trx", Trx(total: 3, executed: 2, passed: 1, failed: 1, notExecuted: 1));

        TrxStageSummary summary = TrxStageSummary.Load(path);

        Assert.AreEqual(3, summary.Discovered);
        Assert.AreEqual(3, summary.Executed);
        Assert.AreEqual(1, summary.Passed);
        Assert.AreEqual(1, summary.Failed);
        Assert.AreEqual(1, summary.Skipped);
    }

    [TestMethod]
    public void Load_rejects_zero_discovery_or_inconsistent_arithmetic()
    {
        using TestDirectory directory = TestDirectory.Create();
        Assert.ThrowsExactly<InvalidDataException>(() =>
            TrxStageSummary.Load(directory.WriteText("zero.trx", Trx(0, 0, 0, 0, 0))));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            TrxStageSummary.Load(directory.WriteText("bad.trx", Trx(3, 2, 2, 1, 1))));
    }

    private static string Trx(int total, int executed, int passed, int failed, int notExecuted) => $$"""
        <?xml version="1.0" encoding="utf-8"?>
        <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
          <ResultSummary outcome="Completed">
            <Counters total="{{total}}" executed="{{executed}}" passed="{{passed}}" failed="{{failed}}" notExecuted="{{notExecuted}}" />
          </ResultSummary>
        </TestRun>
        """;
}
