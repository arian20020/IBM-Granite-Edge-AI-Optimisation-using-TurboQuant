using System.Security.Cryptography;
using GraniteEdgeAI.ModelInspection.LlamaSharp.ModelProbe;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests;

/// <summary>
/// Proves request continuity is checked during the Stage 1 bracket before
/// native runtime configuration.
/// </summary>
[TestClass]
[TestCategory("Deterministic")]
public sealed class VocabOnlyModelProbeContinuityTests
{
    [TestMethod]
    public void TierOneWorkflowChecksOutTheContinuityFixture()
    {
        string workflow = File.ReadAllText(Path.Combine(
            RepositoryPaths.FindRoot(),
            ".github",
            "workflows",
            "llamasharp-feasibility-smoke.yml"));

        StringAssert.Contains(
            workflow,
            "tests/TestFixtures/GGUF/N-001-vocab-only-spm.gguf");
    }

    [TestMethod]
    public async Task RunAsyncWithLengthMismatchFailsAfterActiveBeforeHash()
    {
        using var directory = new TemporaryDirectory("continuity-length");
        string modelPath = await TestFileBuilder.WriteTextAsync(
            directory,
            "model.gguf",
            "model-bytes");
        FileInfo file = GetCurrentFileInfo(modelPath);
        var hasher = new TrackingHasher();
        IVocabOnlyModelProbe probe = CreateProbe(hasher);
        var progress = new CapturingProgress();

        VocabOnlyModelProbeResult result = await probe.RunAsync(
            new VocabOnlyProbeRequest(
                modelPath,
                file.Length + 1,
                ToUtcOffset(file.LastWriteTimeUtc)),
            progress,
            CancellationToken.None);

        AssertContinuityFailure(result, hasher, progress);
    }

    [TestMethod]
    public async Task RunAsyncWithTimestampMismatchFailsAfterActiveBeforeHash()
    {
        using var directory = new TemporaryDirectory("continuity-time");
        string modelPath = await TestFileBuilder.WriteTextAsync(
            directory,
            "model.gguf",
            "model-bytes");
        FileInfo file = GetCurrentFileInfo(modelPath);
        var hasher = new TrackingHasher();
        IVocabOnlyModelProbe probe = CreateProbe(hasher);
        var progress = new CapturingProgress();

        VocabOnlyModelProbeResult result = await probe.RunAsync(
            new VocabOnlyProbeRequest(
                modelPath,
                file.Length,
                ToUtcOffset(file.LastWriteTimeUtc).AddTicks(1)),
            progress,
            CancellationToken.None);

        AssertContinuityFailure(result, hasher, progress);
    }

    [TestMethod]
    public async Task RunAsyncWithMatchingIdentityReportsActiveBeforeHashCompletes()
    {
        using var directory = new TemporaryDirectory("continuity-match");
        string modelPath = await TestFileBuilder.WriteTextAsync(
            directory,
            "model.gguf",
            "model-bytes");
        FileInfo file = GetCurrentFileInfo(modelPath);
        var hasher = new BlockingHasher();
        IVocabOnlyModelProbe probe = CreateProbe(hasher);
        var progress = new CapturingProgress();
        using var cancellation = new CancellationTokenSource();

        Task<VocabOnlyModelProbeResult> pending = probe.RunAsync(
            new VocabOnlyProbeRequest(
                modelPath,
                file.Length,
                ToUtcOffset(file.LastWriteTimeUtc)),
            progress,
            cancellation.Token);

        try
        {
            await hasher.Started.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.HasCount(1, progress.Values);
            AssertProgress(
                progress.Values[0],
                VocabOnlyProbePhase.CheckModelPackage,
                VocabOnlyProbePhaseStatus.Active);
        }
        finally
        {
            cancellation.Cancel();
            _ = await pending.WaitAsync(TimeSpan.FromSeconds(2));
        }

        Assert.HasCount(1, progress.Values);
    }

    [TestMethod]
    public async Task RunAsyncWithMatchingIdentityReportsPackageCheckpointAfterHashBeforeNative()
    {
        using var directory = new TemporaryDirectory("continuity-checkpoint");
        string modelPath = await TestFileBuilder.WriteTextAsync(
            directory,
            "model.gguf",
            "model-bytes");
        FileInfo file = GetCurrentFileInfo(modelPath);
        var hasher = new TrackingHasher();
        IVocabOnlyModelProbe probe = CreateProbe(hasher);
        using var cancellation = new CancellationTokenSource();
        var progress = new CapturingProgress(
            value =>
            {
                if (value.Phase == VocabOnlyProbePhase.CheckModelPackage &&
                    value.Status == VocabOnlyProbePhaseStatus.Completed)
                {
                    cancellation.Cancel();
                }
            });

        VocabOnlyModelProbeResult result = await probe.RunAsync(
            new VocabOnlyProbeRequest(
                modelPath,
                file.Length,
                ToUtcOffset(file.LastWriteTimeUtc)),
            progress,
            cancellation.Token);

        Assert.AreEqual(2, hasher.CallCount);
        Assert.HasCount(2, progress.Values);
        AssertProgress(
            progress.Values[0],
            VocabOnlyProbePhase.CheckModelPackage,
            VocabOnlyProbePhaseStatus.Active);
        AssertProgress(
            progress.Values[1],
            VocabOnlyProbePhase.CheckModelPackage,
            VocabOnlyProbePhaseStatus.Completed);
        Assert.IsNull(result.SelectedBackend);
        Assert.AreEqual(
            VocabOnlyProbeCompletionStatus.Cancelled,
            result.CompletionStatus);
        Assert.IsNotNull(result.Integrity);
        Assert.IsTrue(result.Integrity.IsPreserved);

        var throwingHasher = new TrackingHasher();
        IVocabOnlyModelProbe throwingProbe = CreateProbe(throwingHasher);
        var throwingProgress = new CapturingProgress(
            value =>
            {
                if (value.Phase == VocabOnlyProbePhase.CheckModelPackage &&
                    value.Status == VocabOnlyProbePhaseStatus.Completed)
                {
                    throw new InvalidOperationException(
                        "Expected completed-callback failure.");
                }
            });

        VocabOnlyModelProbeResult callbackFailure =
            await throwingProbe.RunAsync(
                new VocabOnlyProbeRequest(
                    modelPath,
                    file.Length,
                    ToUtcOffset(file.LastWriteTimeUtc)),
                throwingProgress,
                CancellationToken.None);

        Assert.AreEqual(2, throwingHasher.CallCount);
        Assert.AreEqual(
            VocabOnlyProbeCompletionStatus.Failed,
            callbackFailure.CompletionStatus);
        Assert.IsNotNull(callbackFailure.BeforeSnapshot);
        Assert.IsNotNull(callbackFailure.AfterSnapshot);
        Assert.IsNotNull(callbackFailure.Integrity);
        Assert.IsTrue(callbackFailure.Integrity.IsPreserved);
        Assert.IsNull(callbackFailure.SelectedBackend);

        string fixtureSource = Path.Combine(
            RepositoryPaths.FindRoot(),
            "tests",
            "TestFixtures",
            "GGUF",
            "N-001-vocab-only-spm.gguf");
        string nativeModelPath = directory.Combine("n001-cancellation.gguf");
        File.Copy(fixtureSource, nativeModelPath);
        FileInfo nativeFile = GetCurrentFileInfo(nativeModelPath);
        var nativeRequest = new VocabOnlyProbeRequest(
            nativeModelPath,
            nativeFile.Length,
            ToUtcOffset(nativeFile.LastWriteTimeUtc));

        using var stageTwoCancellation = new CancellationTokenSource();
        var stageTwoHasher = new TrackingHasher();
        var stageTwoProgress = new CapturingProgress(
            value =>
            {
                if (value.Phase ==
                        VocabOnlyProbePhase.ReadModelConfiguration &&
                    value.Status == VocabOnlyProbePhaseStatus.Active)
                {
                    stageTwoCancellation.Cancel();
                }
            });

        VocabOnlyModelProbeResult stageTwoCancelled =
            await CreateProbe(stageTwoHasher).RunAsync(
                nativeRequest,
                stageTwoProgress,
                stageTwoCancellation.Token);

        AssertCancelledWithPreservedIntegrity(
            stageTwoCancelled,
            stageTwoHasher);
        Assert.IsNull(stageTwoCancelled.SelectedBackend);
        Assert.IsNull(stageTwoCancelled.NativeHandleClosedAfterDispose);
        AssertCoreProgress(
            stageTwoProgress.Values,
            [
                (VocabOnlyProbePhase.CheckModelPackage,
                    VocabOnlyProbePhaseStatus.Active),
                (VocabOnlyProbePhase.CheckModelPackage,
                    VocabOnlyProbePhaseStatus.Completed),
                (VocabOnlyProbePhase.ReadModelConfiguration,
                    VocabOnlyProbePhaseStatus.Active)
            ]);

        using var stageThreeCancellation = new CancellationTokenSource();
        var stageThreeHasher = new TrackingHasher();
        var stageThreeProgress = new CapturingProgress(
            value =>
            {
                if (value.Phase ==
                        VocabOnlyProbePhase.ValidateTokenizerAndChatSetup &&
                    value.Status == VocabOnlyProbePhaseStatus.Active)
                {
                    stageThreeCancellation.Cancel();
                }
            });

        VocabOnlyModelProbeResult stageThreeCancelled =
            await CreateProbe(stageThreeHasher).RunAsync(
                nativeRequest,
                stageThreeProgress,
                stageThreeCancellation.Token);

        AssertCancelledWithPreservedIntegrity(
            stageThreeCancelled,
            stageThreeHasher);
        Assert.IsNotNull(stageThreeCancelled.SelectedBackend);
        Assert.IsNotNull(stageThreeCancelled.LoadDurationMilliseconds);
        Assert.IsTrue(stageThreeCancelled.NativeHandleClosedAfterDispose);
        Assert.IsNull(stageThreeCancelled.ModelEvidence);
        AssertCoreProgress(
            stageThreeProgress.Values,
            [
                (VocabOnlyProbePhase.CheckModelPackage,
                    VocabOnlyProbePhaseStatus.Active),
                (VocabOnlyProbePhase.CheckModelPackage,
                    VocabOnlyProbePhaseStatus.Completed),
                (VocabOnlyProbePhase.ReadModelConfiguration,
                    VocabOnlyProbePhaseStatus.Active),
                (VocabOnlyProbePhase.ReadModelConfiguration,
                    VocabOnlyProbePhaseStatus.Completed),
                (VocabOnlyProbePhase.ValidateTokenizerAndChatSetup,
                    VocabOnlyProbePhaseStatus.Active)
            ]);
    }

    private static IVocabOnlyModelProbe CreateProbe(IModelFileHasher hasher) =>
        new VocabOnlyModelProbe(new ModelFileSnapshotService(hasher));

    private static FileInfo GetCurrentFileInfo(string path)
    {
        var file = new FileInfo(path);
        file.Refresh();
        return file;
    }

    private static DateTimeOffset ToUtcOffset(DateTime value) =>
        new(value, TimeSpan.Zero);

    private static void AssertContinuityFailure(
        VocabOnlyModelProbeResult result,
        TrackingHasher hasher,
        CapturingProgress progress)
    {
        Assert.AreEqual(
            VocabOnlyProbeCompletionStatus.Failed,
            result.CompletionStatus);
        Assert.AreEqual(
            "MI-OP-MODEL-CONTINUITY-MISMATCH",
            result.FailureCode);
        Assert.AreEqual(0, hasher.CallCount);
        Assert.IsNull(result.BeforeSnapshot);
        Assert.IsNull(result.SelectedBackend);
        Assert.IsNull(result.ModelEvidence);
        Assert.HasCount(0, result.ProgressSamples);
        Assert.HasCount(1, progress.Values);
        AssertProgress(
            progress.Values[0],
            VocabOnlyProbePhase.CheckModelPackage,
            VocabOnlyProbePhaseStatus.Active);
    }

    private static void AssertProgress(
        VocabOnlyProbeProgress actual,
        VocabOnlyProbePhase phase,
        VocabOnlyProbePhaseStatus status)
    {
        Assert.AreEqual(phase, actual.Phase);
        Assert.AreEqual(status, actual.Status);
        Assert.IsNull(actual.NativeFraction);
    }

    private static void AssertCancelledWithPreservedIntegrity(
        VocabOnlyModelProbeResult result,
        TrackingHasher hasher)
    {
        Assert.AreEqual(2, hasher.CallCount);
        Assert.AreEqual(
            VocabOnlyProbeCompletionStatus.Cancelled,
            result.CompletionStatus);
        Assert.AreEqual("MI-PROBE-CANCELLED", result.FailureCode);
        Assert.IsNotNull(result.BeforeSnapshot);
        Assert.IsNotNull(result.AfterSnapshot);
        Assert.IsNotNull(result.Integrity);
        Assert.IsTrue(result.Integrity.IsPreserved);
    }

    private static void AssertCoreProgress(
        IReadOnlyList<VocabOnlyProbeProgress> values,
        IReadOnlyList<(
            VocabOnlyProbePhase Phase,
            VocabOnlyProbePhaseStatus Status)> expected)
    {
        VocabOnlyProbeProgress[] actual = values
            .Where(value =>
                value.Status != VocabOnlyProbePhaseStatus.Fraction)
            .ToArray();
        Assert.AreEqual(expected.Count, actual.Length);

        for (int index = 0; index < expected.Count; index++)
        {
            AssertProgress(
                actual[index],
                expected[index].Phase,
                expected[index].Status);
        }
    }

    private sealed class CapturingProgress : IProgress<VocabOnlyProbeProgress>
    {
        private readonly Action<VocabOnlyProbeProgress>? _onReport;

        internal CapturingProgress(
            Action<VocabOnlyProbeProgress>? onReport = null)
        {
            _onReport = onReport;
        }

        internal List<VocabOnlyProbeProgress> Values { get; } = [];

        public void Report(VocabOnlyProbeProgress value)
        {
            Values.Add(value);
            _onReport?.Invoke(value);
        }
    }

    private sealed class TrackingHasher : IModelFileHasher
    {
        internal int CallCount { get; private set; }

        public async Task<byte[]> ComputeHashAsync(
            Stream stream,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return await SHA256.HashDataAsync(stream, cancellationToken);
        }
    }

    private sealed class BlockingHasher : IModelFileHasher
    {
        private readonly TaskCompletionSource _started = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task Started => _started.Task;

        public async Task<byte[]> ComputeHashAsync(
            Stream stream,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(stream);
            _started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new AssertFailedException("Hashing should end by cancellation.");
        }
    }
}
