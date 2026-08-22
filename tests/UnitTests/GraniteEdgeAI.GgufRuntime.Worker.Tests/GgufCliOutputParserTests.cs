using GraniteEdgeAI.GgufRuntime.Worker.Session;

namespace GraniteEdgeAI.GgufRuntime.Worker.Tests;

[TestClass]
public sealed class GgufCliOutputParserTests
{
    [TestMethod]
    public void ParseExactControlMarkersReturnsLifecycleKindsWithoutText()
    {
        GgufCliOutput ready = GgufCliOutputParser.Parse(
            "G1READY",
            GgufCliOutputSource.StandardOutput);
        GgufCliOutput started = GgufCliOutputParser.Parse(
            "G1RESPONSE",
            GgufCliOutputSource.StandardOutput);
        GgufCliOutput completed = GgufCliOutputParser.Parse(
            "G1DONE",
            GgufCliOutputSource.StandardOutput);

        Assert.AreEqual(GgufCliOutputKind.Ready, ready.Kind);
        Assert.AreEqual(GgufCliOutputKind.ResponseStarted, started.Kind);
        Assert.AreEqual(GgufCliOutputKind.ResponseCompleted, completed.Kind);
        Assert.IsNull(ready.Text);
        Assert.IsNull(started.Text);
        Assert.IsNull(completed.Text);
    }

    [TestMethod]
    public void ParseStandardOutputTextReturnsDeltaUnchanged()
    {
        GgufCliOutput result = GgufCliOutputParser.Parse(
            "G1DELTA YXNzaXN0YW50IHRleHQ=",
            GgufCliOutputSource.StandardOutput);

        Assert.AreEqual(GgufCliOutputKind.TextDelta, result.Kind);
        Assert.AreEqual("assistant text", result.Text);
    }

    [TestMethod]
    public void ParseStandardErrorNeverReturnsAssistantText()
    {
        GgufCliOutput result = GgufCliOutputParser.Parse(
            "sensitive raw runtime detail",
            GgufCliOutputSource.StandardError);

        Assert.AreEqual(GgufCliOutputKind.Diagnostic, result.Kind);
        Assert.IsNull(result.Text);
        Assert.AreEqual("cli-stderr", result.DiagnosticCode);
    }

    [TestMethod]
    public void ParseOversizedStandardOutputFrameFailsClosed()
    {
        string oversized = "G1DELTA " + new string('A', 400_004);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            GgufCliOutputParser.Parse(
                oversized,
                GgufCliOutputSource.StandardOutput));
    }
}
