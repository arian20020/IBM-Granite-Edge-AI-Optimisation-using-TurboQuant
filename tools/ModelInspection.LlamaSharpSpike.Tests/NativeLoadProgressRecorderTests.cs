using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.ModelProbe;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests;

/// <summary>
/// Verifies that genuine native progress is captured safely without inventing
/// extra intermediate percentages.
/// </summary>
[TestClass]
public sealed class NativeLoadProgressRecorderTests
{
    [TestMethod]
    public void Report_ClampsFractionsToSupportedRange()
    {
        var recorder = new NativeLoadProgressRecorder();

        recorder.Report(-0.25f);
        recorder.Report(1.25f);

        IReadOnlyList<NativeLoadProgressSample> samples =
            recorder.GetSnapshot();

        Assert.AreEqual(2, samples.Count);
        Assert.AreEqual(0f, samples[0].Fraction);
        Assert.AreEqual(1f, samples[1].Fraction);
    }

    [TestMethod]
    public void Report_DoesNotStoreConsecutiveDuplicateFractions()
    {
        var recorder = new NativeLoadProgressRecorder();

        recorder.Report(0.25f);
        recorder.Report(0.25f);
        recorder.Report(0.50f);

        IReadOnlyList<NativeLoadProgressSample> samples =
            recorder.GetSnapshot();

        Assert.AreEqual(2, samples.Count);
        Assert.AreEqual(0.25f, samples[0].Fraction);
        Assert.AreEqual(0.50f, samples[1].Fraction);
    }

    [TestMethod]
    public void GetSnapshot_ReturnsIndependentReadOnlyCopy()
    {
        var recorder = new NativeLoadProgressRecorder();
        recorder.Report(0.10f);

        IReadOnlyList<NativeLoadProgressSample> firstSnapshot =
            recorder.GetSnapshot();

        recorder.Report(0.20f);

        Assert.AreEqual(1, firstSnapshot.Count);
        Assert.AreEqual(2, recorder.GetSnapshot().Count);
    }
}
