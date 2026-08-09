using GraniteEdgeAI.Features.ModelInspection.Classification;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Runtime;
using GraniteEdgeAI.Features.ModelInspection.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class ModelInspectionServiceTests
{
    private const string Sha256 =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string LlamaCppCommit =
        "3f7c29d318e317b63f54c558bc69803963d7d88c";

    private static readonly DateTimeOffset StartedAtUtc = new(
        2026,
        8,
        9,
        8,
        0,
        0,
        TimeSpan.Zero);

    private static readonly DateTimeOffset CompletedAtUtc =
        StartedAtUtc.AddSeconds(3);

    [TestMethod]
    public async Task InspectAsync_CompletedProbe_ClassifiesOnceWithServiceTimes()
    {
        ModelInspectionRequest request = CreateRequest();
        ModelInspectionEvidence evidence = CreateEvidence(chatTemplatePresent: true);
        IProgress<ModelInspectionProgress> progress =
            new Progress<ModelInspectionProgress>();
        CancellationToken cancellationToken = new();
        DelegatingProbe probe = new((actualRequest, actualProgress, actualToken) =>
        {
            Assert.AreSame(request, actualRequest);
            Assert.AreSame(progress, actualProgress);
            Assert.AreEqual(cancellationToken, actualToken);
            return Task.FromResult(ModelInspectionProbeResult.Completed(evidence));
        });
        CapturingClassifier classifier = new();
        ModelInspectionService service = new(
            probe,
            classifier,
            new SequenceTimeProvider(StartedAtUtc, CompletedAtUtc));

        ModelInspectionExecutionResult execution = await service.InspectAsync(
            request,
            progress,
            cancellationToken);

        Assert.AreEqual(ModelInspectionExecutionStatus.Completed, execution.Status);
        Assert.IsNotNull(execution.Result);
        Assert.AreEqual(ModelInspectionOutcome.Ready, execution.Result.Outcome);
        Assert.AreSame(evidence, execution.Result.Evidence);
        Assert.AreEqual(StartedAtUtc, execution.Result.StartedAtUtc);
        Assert.AreEqual(CompletedAtUtc, execution.Result.CompletedAtUtc);
        Assert.AreEqual(1, classifier.CallCount);
        Assert.IsNull(execution.Failure);
        Assert.IsNull(execution.CancellationWasCooperative);
    }

    [TestMethod]
    public async Task InspectAsync_CancelledProbe_ReturnsCooperativeCancellation()
    {
        ModelInspectionService service = new(
            new DelegatingProbe((_, _, _) => Task.FromResult(
                ModelInspectionProbeResult.Cancelled())),
            new UnexpectedClassifier(),
            new SequenceTimeProvider(StartedAtUtc));

        ModelInspectionExecutionResult execution = await service.InspectAsync(
            CreateRequest(),
            progress: null,
            CancellationToken.None);

        Assert.AreEqual(ModelInspectionExecutionStatus.Cancelled, execution.Status);
        Assert.IsTrue(execution.CancellationWasCooperative is true);
        Assert.IsNull(execution.Result);
        Assert.IsNull(execution.Failure);
    }

    [TestMethod]
    public async Task InspectAsync_OperationalFailureProbe_PreservesFailure()
    {
        ModelInspectionOperationalFailure failure = new(
            code: "worker_unavailable",
            userMessage: "Model inspection could not start.",
            technicalDetail: "The isolated inspection worker was unavailable.");
        ModelInspectionService service = new(
            new DelegatingProbe((_, _, _) => Task.FromResult(
                ModelInspectionProbeResult.OperationalFailure(failure))),
            new UnexpectedClassifier(),
            new SequenceTimeProvider(StartedAtUtc));

        ModelInspectionExecutionResult execution = await service.InspectAsync(
            CreateRequest(),
            progress: null,
            CancellationToken.None);

        Assert.AreEqual(
            ModelInspectionExecutionStatus.OperationalFailure,
            execution.Status);
        Assert.AreSame(failure, execution.Failure);
        Assert.IsNull(execution.Result);
        Assert.IsNull(execution.CancellationWasCooperative);
    }

    [TestMethod]
    public async Task InspectAsync_PreCancelledToken_ThrowsBeforeDependenciesRun()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        ModelInspectionService service = new(
            new DelegatingProbe((_, _, _) => throw new AssertFailedException(
                "A pre-cancelled request must not invoke the probe.")),
            new UnexpectedClassifier(),
            new UnexpectedTimeProvider());

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            service.InspectAsync(
                CreateRequest(),
                progress: null,
                cancellation.Token));
    }

    private static ModelInspectionRequest CreateRequest()
    {
        return new ModelInspectionRequest(
            modelPath: @"C:\Models\granite.gguf",
            fileName: "granite.gguf",
            expectedFileIdentity: new ExpectedModelFileIdentity(
                lengthBytes: 4_096,
                lastWriteTimeUtc: StartedAtUtc),
            quickScan: ValidatedQuickScanSnapshot.CreateGguf(
                modelName: "Granite",
                architecture: "granite",
                parameterSizeLabel: "3B",
                quantisation: "Q4_K_M",
                fileSizeBytes: 4_096,
                declaredContextLength: 4_096,
                ggufVersion: 3));
    }

    private static ModelInspectionEvidence CreateEvidence(
        bool chatTemplatePresent)
    {
        return new ModelInspectionEvidence(
            file: new ModelInspectionFileEvidence(
                fileName: "granite.gguf",
                canonicalPathSha256: Sha256,
                lengthBytes: 4_096,
                lastWriteTimeUtc: StartedAtUtc,
                modelSha256: Sha256,
                integrityPreserved: true),
            configuration: new ModelInspectionConfigurationEvidence(
                format: "GGUF",
                ggufVersion: 3,
                modelName: "Granite",
                architecture: "granite",
                fileType: 15,
                quantisationVersion: 2,
                declaredContextLength: 4_096,
                embeddingSize: 2_048,
                layerCount: 24,
                attentionHeadCount: 16,
                kvHeadCount: 8,
                parameterCount: 3_000_000_000),
            tokenizer: new ModelInspectionTokenizerEvidence(
                tokenizerModel: "gpt2",
                vocabularyCount: 49_152,
                vocabularyType: "BPE",
                tokenizerSmokePassed: true,
                tokenizerSmokeTokenCount: 4,
                knownSpecialTokenIds: new Dictionary<string, int>(
                    StringComparer.Ordinal)),
            chatTemplate: chatTemplatePresent
                ? new ModelInspectionChatTemplateEvidence(
                    present: true,
                    lengthCharacters: 128,
                    sha256: Sha256)
                : new ModelInspectionChatTemplateEvidence(
                    present: false,
                    lengthCharacters: null,
                    sha256: null),
            runtime: new ModelInspectionRuntimeIdentity(
                workerId: "GraniteEdgeAI.ModelInspection.Worker",
                workerVersion: "1.0.0",
                protocolVersion: 1,
                runtimeProfile: "llamasharp-0.27.0-cpu-win-x64-vocab-only-v1",
                llamaSharpVersion: "0.27.0",
                backendPackageVersion: "0.27.0",
                mappedLlamaCppCommit: LlamaCppCommit,
                nativeLibraryName: "llama.dll",
                processArchitecture: "X64",
                inspectionMode: "VocabOnly",
                usesCuda: false,
                usesVulkan: false,
                gpuLayerCount: 0),
            observations: Array.Empty<ModelInspectionObservation>());
    }

    private sealed class DelegatingProbe : ILlamaModelProbe
    {
        private readonly Func<
            ModelInspectionRequest,
            IProgress<ModelInspectionProgress>?,
            CancellationToken,
            Task<ModelInspectionProbeResult>> inspect;

        internal DelegatingProbe(
            Func<
                ModelInspectionRequest,
                IProgress<ModelInspectionProgress>?,
                CancellationToken,
                Task<ModelInspectionProbeResult>> inspect)
        {
            this.inspect = inspect;
        }

        public Task<ModelInspectionProbeResult> InspectAsync(
            ModelInspectionRequest request,
            IProgress<ModelInspectionProgress>? progress,
            CancellationToken cancellationToken)
        {
            return inspect(request, progress, cancellationToken);
        }
    }

    private sealed class CapturingClassifier : IModelInspectionClassifier
    {
        private readonly ModelInspectionClassifier inner = new();

        internal int CallCount { get; private set; }

        public ModelInspectionResult Classify(
            ModelInspectionEvidence evidence,
            DateTimeOffset startedAtUtc,
            DateTimeOffset completedAtUtc)
        {
            CallCount++;
            return inner.Classify(evidence, startedAtUtc, completedAtUtc);
        }
    }

    private sealed class UnexpectedClassifier : IModelInspectionClassifier
    {
        public ModelInspectionResult Classify(
            ModelInspectionEvidence evidence,
            DateTimeOffset startedAtUtc,
            DateTimeOffset completedAtUtc)
        {
            throw new AssertFailedException(
                "Only completed probe evidence may be classified.");
        }
    }

    private sealed class SequenceTimeProvider : TimeProvider
    {
        private readonly Queue<DateTimeOffset> values;

        internal SequenceTimeProvider(params DateTimeOffset[] values)
        {
            this.values = new Queue<DateTimeOffset>(values);
        }

        public override DateTimeOffset GetUtcNow()
        {
            if (values.Count == 0)
            {
                throw new AssertFailedException(
                    "The service requested an unexpected UTC timestamp.");
            }

            return values.Dequeue();
        }
    }

    private sealed class UnexpectedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            throw new AssertFailedException(
                "A pre-cancelled request must not read the clock.");
        }
    }
}
