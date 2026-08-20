using GraniteEdgeAI.GgufRuntime.Worker.Session;

namespace GraniteEdgeAI.GgufRuntime.Worker.Tests;

[TestClass]
public sealed class GgufCliOutputParserTests
{
    [TestMethod]
    public void ParseExactControlMarkersReturnsLifecycleKindsWithoutText()
    {
        GgufCliOutput ready = GgufCliOutputParser.Parse(
            "__G1_READY__",
            GgufCliOutputSource.StandardOutput);
        GgufCliOutput started = GgufCliOutputParser.Parse(
            "__G1_RESPONSE_START__",
            GgufCliOutputSource.StandardOutput);
        GgufCliOutput completed = GgufCliOutputParser.Parse(
            "__G1_RESPONSE_DONE__",
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
            "assistant text",
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
}
