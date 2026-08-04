using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.ModelProbe;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests;

/// <summary>
/// Verifies that genuine native progress is captured safely without inventing
/// extra intermediate percentages.
/// </summary>
[TestClass]
[TestCategory("Deterministic")]
public sealed class NativeLoadProgressRecorderTests
{
    [TestMethod]
    public void GetSnapshot_BeforeAnyReport_ReturnsEmptyCollection()
    {
        Assert.AreEqual(
            0,
            new NativeLoadProgressRecorder().GetSnapshot().Count);
    }

    [TestMethod]
    public void Report_RecordsExactBounds()
    {
        var recorder = new NativeLoadProgressRecorder();

        recorder.Report(0f);
        recorder.Report(1f);

        IReadOnlyList<NativeLoadProgressSample> samples =
            recorder.GetSnapshot();

        Assert.AreEqual(2, samples.Count);
        Assert.AreEqual(0f, samples[0].Fraction);
        Assert.AreEqual(1f, samples[1].Fraction);
    }

    [TestMethod]
    public void Report_ClampsFiniteFractionsToSupportedRange()
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
    public void Report_WithNaN_IgnoresValue()
    {
        var recorder = new NativeLoadProgressRecorder();

        recorder.Report(float.NaN);

        Assert.AreEqual(0, recorder.GetSnapshot().Count);
    }

    [TestMethod]
    public void Report_WithInfinities_NormalisesToBounds()
    {
        var recorder = new NativeLoadProgressRecorder();

        recorder.Report(float.NegativeInfinity);
        recorder.Report(float.PositiveInfinity);

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
    public void Report_PreservesNonConsecutiveRepeatedFractions()
    {
        var recorder = new NativeLoadProgressRecorder();

        recorder.Report(0.25f);
        recorder.Report(0.50f);
        recorder.Report(0.25f);

        IReadOnlyList<NativeLoadProgressSample> samples =
            recorder.GetSnapshot();

        Assert.AreEqual(3, samples.Count);
        Assert.AreEqual(0.25f, samples[2].Fraction);
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

    [TestMethod]
    public void Report_FromMultipleThreads_RemainsValidAndSnapshotSafe()
    {
        var recorder = new NativeLoadProgressRecorder();

        Parallel.For(
            0,
            500,
            index => recorder.Report(index / 499f));

        IReadOnlyList<NativeLoadProgressSample> samples =
            recorder.GetSnapshot();

        Assert.IsTrue(samples.Count > 0);
        Assert.IsTrue(
            samples.All(
                sample =>
                    float.IsFinite(sample.Fraction) &&
                    sample.Fraction >= 0f &&
                    sample.Fraction <= 1f));

        for (int index = 1; index < samples.Count; index++)
        {
            Assert.IsTrue(
                samples[index].ElapsedMilliseconds >=
                samples[index - 1].ElapsedMilliseconds);
        }
    }
}
