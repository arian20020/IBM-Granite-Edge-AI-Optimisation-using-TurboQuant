using GraniteEdgeAI.ModelInspection.LlamaSharp.ModelProbe;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.Support;
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
    public void Report_ForwardsOnlyStoredGenuineFractionsSynchronously()
    {
        var progress = new CapturingProgress();
        var phases = new VocabOnlyProbePhaseSequence(progress);
        var recorder = new NativeLoadProgressRecorder(phases);

        phases.Run(
            VocabOnlyProbePhase.ReadModelConfiguration,
            () =>
            {
                recorder.Report(0.25f);
                Assert.HasCount(2, progress.Values);
                recorder.Report(0.25f);
                recorder.Report(float.NaN);
                recorder.Report(0.50f);
                return true;
            });

        Assert.HasCount(4, progress.Values);
        Assert.AreEqual(
            VocabOnlyProbePhaseStatus.Active,
            progress.Values[0].Status);
        Assert.AreEqual(
            VocabOnlyProbePhaseStatus.Fraction,
            progress.Values[1].Status);
        Assert.AreEqual(0.25f, progress.Values[1].NativeFraction);
        Assert.AreEqual(
            VocabOnlyProbePhaseStatus.Fraction,
            progress.Values[2].Status);
        Assert.AreEqual(0.50f, progress.Values[2].NativeFraction);
        Assert.AreEqual(
            VocabOnlyProbePhaseStatus.Completed,
            progress.Values[3].Status);
        Assert.IsTrue(
            progress.Values.All(
                value => value.Phase ==
                    VocabOnlyProbePhase.ReadModelConfiguration));
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

        string source = File.ReadAllText(
            Path.Combine(
                RepositoryPaths.FindRoot(),
                "runtime",
                "GraniteEdgeAI.ModelInspection.LlamaSharp",
                "ModelProbe",
                "NativeLoadProgressRecorder.cs"));
        int reportLock = source.IndexOf(
            "lock (_sync)",
            source.IndexOf("public void Report(float value)",
                StringComparison.Ordinal),
            StringComparison.Ordinal);
        int forwarding = source.IndexOf(
            "_phaseSequence?.ReportNativeFraction(fraction);",
            reportLock,
            StringComparison.Ordinal);
        int reportLockEnd = FindMatchingBrace(
            source,
            source.IndexOf('{', reportLock));

        Assert.IsTrue(reportLock >= 0);
        Assert.IsTrue(
            forwarding > reportLock && forwarding < reportLockEnd,
            "Stored native samples and emitted phase facts must be atomic.");
    }

    private static int FindMatchingBrace(string source, int openingBrace)
    {
        Assert.IsTrue(openingBrace >= 0);
        int depth = 0;

        for (int index = openingBrace; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}' && --depth == 0)
            {
                return index;
            }
        }

        Assert.Fail("The recorder lock has no matching closing brace.");
        return -1;
    }

    private sealed class CapturingProgress : IProgress<VocabOnlyProbeProgress>
    {
        internal List<VocabOnlyProbeProgress> Values { get; } = [];

        public void Report(VocabOnlyProbeProgress value) => Values.Add(value);
    }
}
