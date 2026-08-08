using GraniteEdgeAI.ModelInspection.LlamaSharp;
using GraniteEdgeAI.ModelInspection.LlamaSharp.ModelProbe;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests;

/// <summary>
/// Verifies that evidence and native logs do not expose a canonical model path.
/// </summary>
[TestClass]
[TestCategory("Deterministic")]
public sealed class SensitiveTextRedactorTests
{
    [TestMethod]
    public void Redact_OnWindows_RemovesEveryCaseVariantOfCanonicalPath()
    {
        const string modelPath = @"C:\Models\Granite.gguf";
        const string input =
            @"failed C:\MODELS\GRANITE.GGUF then C:\Models\Granite.gguf";

        string? result = SensitiveTextRedactor.Redact(
            input,
            modelPath,
            windowsCaseInsensitive: true);

        Assert.AreEqual(
            "failed <model-path> then <model-path>",
            result);
    }

    [TestMethod]
    public void Redact_WhenCaseSensitive_ReplacesOnlyExactPath()
    {
        const string modelPath = "/models/Granite.gguf";
        const string input =
            "/models/granite.gguf and /models/Granite.gguf";

        string? result = SensitiveTextRedactor.Redact(
            input,
            modelPath,
            windowsCaseInsensitive: false);

        Assert.AreEqual(
            "/models/granite.gguf and <model-path>",
            result);
    }

    [TestMethod]
    public void Redact_WithNullText_ReturnsNull()
    {
        Assert.IsNull(
            SensitiveTextRedactor.Redact(
                text: null,
                canonicalModelPath: @"C:\Models\Granite.gguf"));
    }

    [TestMethod]
    public void Redact_WithNullPath_ReturnsOriginalText()
    {
        const string text = "failure";

        Assert.AreEqual(
            text,
            SensitiveTextRedactor.Redact(
                text,
                canonicalModelPath: null));
    }

    [TestMethod]
    public void Redact_WithUnrelatedPath_DoesNotAlterText()
    {
        const string text = @"failed C:\Other\Other.gguf";

        Assert.AreEqual(
            text,
            SensitiveTextRedactor.Redact(
                text,
                @"C:\Models\Granite.gguf",
                windowsCaseInsensitive: true));
    }

    [TestMethod]
    public void Redact_WithFilenameOnly_DoesNotRemoveUsefulFilename()
    {
        const string text = "granite.gguf failed";

        Assert.AreEqual(
            text,
            SensitiveTextRedactor.Redact(
                text,
                @"C:\Models\granite.gguf",
                windowsCaseInsensitive: true));
    }

    [TestMethod]
    public void RedactLogs_RedactsEveryEntryAndReturnsIndependentCopy()
    {
        const string modelPath = @"C:\Models\Granite.gguf";
        var logs = new[]
        {
            new NativeBackendLogEntry(
                "Info",
                @"opening C:\Models\Granite.gguf"),
            new NativeBackendLogEntry(
                "Error",
                @"failed C:\MODELS\GRANITE.GGUF")
        };

        IReadOnlyList<NativeBackendLogEntry> result =
            SensitiveTextRedactor.RedactLogs(logs, modelPath);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("opening <model-path>", result[0].Message);
        Assert.AreEqual("failed <model-path>", result[1].Message);
        Assert.AreNotSame(logs[0], result[0]);
    }

    [TestMethod]
    public void RedactLogs_WithNullCollection_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => SensitiveTextRedactor.RedactLogs(
                logs: null!,
                canonicalModelPath: null));
    }
}
